# Off-Hire Process Architecture

## Overview
The Off-Hire platform orchestrates off-hire orders between external clients, the internal Recon
process, Cosmos DB for persistence, and Microsoft Dynamics as the system of record for rental
inventory. The solution adopts Domain-Driven Design (DDD) and Clean Architecture principles to
isolate business logic, provide testable boundaries, and facilitate long-term maintainability.

```
+-------------------+        +-------------------+        +------------------+
| Customer Systems  |        | Recon Service     |        | Backoffice Users |
+---------+---------+        +---------+---------+        +---------+--------+
          |                           |                           |
          v                           v                           v
  +-------+--------+          +------+-------+           +--------+------+
  | Update API     |          | Blob Listener|           | Dynamics 365  |
  | (OffHire.Api)  +--------->+ (FunctionApp)+---------->+ Integration  |
  +-------+--------+          +------+-------+           +--------+------+
          |                           |                           ^
          v                           v                           |
  +-------+--------+          +------+-------+           +--------+------+
  | Application    |          | Application |           | Cosmos DB     |
  | Services       |          | Services    |           | (history +    |
  +-------+--------+          +------+-------+           | aggregates)   |
          |                           |                   +--------------+
          v                           v
  +-------+--------+          +------+-------+
  | Domain Layer   |          | Domain Layer |
  +-------+--------+          +------+-------+
          |                           |
          +-------------+-------------+
                        v
                Shared Domain Model
```

## Bounded Context and Aggregates
* **OffHireOrder** is the aggregate root. It captures contract and account data, the collection
  instructions, and the set of off-hire lines. The aggregate is partitioned by contract number and
  the origin of the request (API, Recon).
* **OffHireLine** represents the external line reference on the customer side and contains one or
  more **LineAllocations** which map to the internal `RentalDeviceLineNumber` values exposed by
  Recon.
* **LineAllocation** tracks quantities, outstanding quantity, scheduling windows, and maintains a
  history of off-hire or update operations. Allocations own the canonical truth for what is pending,
  completed, or deferred for follow-up.

The aggregate ensures invariants such as:
* Quantities per allocation never drop below zero.
* Each allocation can only be off-hired once within a business event.
* History entries are written for every state transition.

## Cosmos Document Shape
Cosmos documents are deliberately normalized around the aggregate. The schema keeps a flattened view
for read efficiency and an embedded history for traceability.

```json
{
  "id": "{contractNumber}:{externalLineRef}",
  "partitionKey": "{companyCode}",
  "contractNumber": "5117-2000043",
  "rentalNumber": "0106-2007937",
  "accountNumber": "180018063",
  "companyCode": "sas",
  "origin": {
    "source": "WebAPI",
    "correlationId": "...",
    "lastUpdatedBy": "user@client.com"
  },
  "lines": [
    {
      "externalLineNumberRef": "EXL001",
      "requestedQuantity": 3,
      "allocations": [
        {
          "rentalDeviceLineNumber": "RDL-732594863",
          "totalQuantity": 1,
          "remainingQuantity": 0,
          "offHireSchedule": {
            "requested": "2025-10-30T08:00:00Z",
            "committed": "2025-10-30T08:00:00Z",
            "reason": "Full"
          },
          "status": "OffHired"
        }
      ],
      "history": [
        {
          "eventType": "OffHireRequested",
          "quantity": 1,
          "occurredAt": "2025-10-10T07:18:36.18Z",
          "trigger": "API",
          "details": {
            "correlationId": "...",
            "notes": "Use the front door"
          }
        },
        {
          "eventType": "DynamicsOffHire",
          "quantity": 1,
          "occurredAt": "2025-10-30T08:00:00Z",
          "trigger": "Recon",
          "details": {
            "dynamicsRequestId": "..."
          }
        }
      ]
    }
  ],
  "audit": {
    "createdAt": "2025-10-12T08:02:31.4410225Z",
    "updatedAt": "2025-10-12T08:02:52.2538308Z"
  }
}
```

History is append-only, enabling easy timeline reconstruction while leaving the latest state at the
line/allocation level for operational reads. Each allocation stores `remainingQuantity` to simplify
partial updates.

## Key Use Cases

### 1. Customer Update API
* Validates and normalizes incoming payloads into `OffHireOrder` aggregates.
* Merges multiple API calls by external line reference. Quantities are stored as the customer
  expectation (`requestedQuantity`) while allocations are seeded with outstanding quantity.
* Emits domain events (`LineRequestedOffHire`) to drive asynchronous integration (e.g. notify Recon
  or schedule follow-up tasks).
* Writes aggregate snapshot into Cosmos.

### 2. Recon-triggered Core Off-Hire
1. Recon uploads a JSON payload to blob storage.
2. Blob listener (Function App) deserializes into `ReconOffHireNotification` domain object.
3. Application service loads the corresponding aggregate from Cosmos.
4. `OffHirePlanner` domain service calculates the required actions per allocation using the
   configured rules:
   * **Scenario 1** – No existing update: take full quantity, mark allocation as fully off-hired,
     emit `DynamicsOffHireRequested` with quantity equal to Recon.
   * **Scenario 2** – Quantity matches: off-hire all requested units, mark line `OffHired`.
   * **Scenario 3** – Quantity mismatch: split the allocation. Off-hire the customer-requested
     quantity; for the remainder generate a `BackdateQuantityReduction` event so Dynamics receives an
     end-date change (preventing Recon re-trigger) and keep allocations pending until the customer
     submits another update.
5. Application service persists new aggregate state and dispatches integration commands to Dynamics
   via the Integration layer.

### 3. Update API after Recon
When an API update is received after a Recon trigger:
* The aggregate contains history entries that specify what has already been off-hired or pushed to
  the past. The new API request reconciles with outstanding allocations.
* If Recon already fully off-hired an allocation, the API command is idempotent and only records a
  history entry acknowledging the already completed action.
* If Recon partially completed an allocation, the remaining quantity is updated and flagged for the
  next Recon run (or manual planning) via domain events.

## Application Layer Responsibilities
* **Command Handlers** – Compose repository interfaces with domain services. They remain thin and
  orchestrate domain operations (`UpsertOffHireOrderCommandHandler`, `ProcessReconOffHireCommandHandler`).
* **Repositories** – Exposed as interfaces (`IOffHireOrderRepository`) implemented against Cosmos.
  They return aggregates with domain behavior intact.
* **Integration Ports** – `IDynamicsService` and `IMessageBus` abstractions handle side-effects.
* **Unit of Work** – Cosmos repository uses transactional batch (when available) to persist the
  aggregate and append history atomically.

## Dynamics Interaction
Dynamics commands are materialized from domain events generated during planning. Two message types
exist:

* `OffHireRequestMessage` – full or partial off-hire action. Contains contract, line allocations, and
  target quantities.
* `BackdateQuantityAdjustmentMessage` – used to push the remaining quantity into the past to prevent
  Recon from emitting duplicates.

Messages are published to Service Bus (or another message transport) by the Integration layer, which
is decoupled from the domain.

## Blob Processing Flow
1. Blob Trigger Function downloads Recon payload.
2. Uses `ProcessReconOffHireCommand` to orchestrate planning.
3. On success, emits integration messages.
4. On failure, writes a compensating history entry and emits a retry event.

## Observability
* Each aggregate stores audit metadata (`createdAt`, `updatedAt`, `lastProcessedReconCorrelation`).
* Application layer emits structured logs and metrics (e.g., `offhire.recon.partial`,
  `offhire.api.merge`).
* Dead-letter and retry queues ensure that Dynamics calls are resilient.

## Compliance With SOLID
* **Single Responsibility** – Domain objects focus on invariants and transitions; application
  services orchestrate; infrastructure handles persistence/integration.
* **Open/Closed** – Behavior is extended through domain services and event handlers; core invariants
  remain closed for modification.
* **Liskov Substitution** – Interfaces (`IOffHireOrderRepository`, `IDynamicsService`) clearly define
  contracts for infrastructure implementations.
* **Interface Segregation** – Infrastructure ports expose small, dedicated interfaces.
* **Dependency Inversion** – Application layer depends on abstractions; infrastructure references the
  domain/application to provide implementations.

## Testing Strategy
* **Domain Tests** validate `OffHirePlanner` logic for the three scenarios and the new update API
  behavior.
* **Application Tests** cover command handlers with mocked repositories and ports.
* **Infrastructure Tests** validate Cosmos serialization (using local emulator) and Dynamics API
  integration.

## Extensibility
* Additional triggers (e.g., manual off-hire) can be introduced by creating new commands that reuse
  the same aggregate.
* Schema evolution is handled by versioning the Cosmos document through projection mappers. Old
  documents can be rehydrated using upgrade functions before reaching the domain.

