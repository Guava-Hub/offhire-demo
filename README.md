# Offhire Demo

This repository provides a Clean Architecture & DDD reference implementation for the
Off-Hire business process. It demonstrates how to:

* Accept customer update API requests.
* Persist normalized aggregates in Cosmos DB.
* Consume Recon triggers from blob storage and orchestrate Dynamics off-hire actions.
* Handle the four API scenarios (single line, quantity splits, and mixed allocations).
* Support Recon scenarios for full and partial off-hires.

## Project Structure

```
├── docs
│   └── offhire-architecture.md
├── src
│   ├── OffHire.Api
│   │   ├── Configuration
│   │   ├── Contracts
│   │   └── Controllers
│   ├── OffHire.Application
│   │   ├── Abstractions
│   │   └── Commands
│   ├── OffHire.Domain
│   │   ├── Models
│   │   ├── Services
│   │   └── ValueObjects
│   └── OffHire.Infrastructure
│       ├── Cosmos
│       └── Dynamics
└── tests
    └── OffHire.Domain.Tests
```

## Key Concepts

* **OffHireOrder aggregate** – Owns the lifecycle of off-hire lines, their allocations, and history.
* **OffHirePlanner** – Domain service responsible for orchestrating Recon scenarios and producing
  integration commands for Dynamics.
* **CosmosOffHireOrderRepository** – Materializes aggregates from Cosmos documents and persists new
  snapshots.

## Running the Example

The repository focuses on architecture and domain modeling. To run the solution you will need the
.NET SDK. After installing it, the following commands build and test the solution:

```
dotnet restore
dotnet build
dotnet test
```

## Next Steps

* Replace the Dynamics client stub with the production integration.
* Wire blob-triggered Functions to execute `ProcessReconOffHireCommand` with the recon payload.
* Extend domain tests to cover all recon and API scenarios.
