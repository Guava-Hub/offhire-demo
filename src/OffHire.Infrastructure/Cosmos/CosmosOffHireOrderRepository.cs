using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using OffHire.Application.Abstractions;
using OffHire.Domain.Models;
using OffHire.Domain.ValueObjects;

namespace OffHire.Infrastructure.Cosmos;

public sealed class CosmosOffHireOrderRepository : IOffHireOrderRepository
{
    private readonly Container _container;

    public CosmosOffHireOrderRepository(Container container)
    {
        _container = container;
    }

    public async Task<OffHireOrder?> FindAsync(string contractNumber, string companyCode, CancellationToken cancellationToken)
    {
        var id = $"{contractNumber}:{companyCode}";
        try
        {
            var response = await _container.ReadItemAsync<CosmosOffHireDocument>(id, new PartitionKey(companyCode), cancellationToken: cancellationToken);
            return MapToDomain(response.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task SaveAsync(OffHireOrder aggregate, CancellationToken cancellationToken)
    {
        var document = MapToDocument(aggregate);
        await _container.UpsertItemAsync(document, new PartitionKey(aggregate.CompanyCode), cancellationToken: cancellationToken);
    }

    private static OffHireOrder MapToDomain(CosmosOffHireDocument document)
    {
        var aggregate = OffHireOrder.Create(
            document.Id,
            document.ContractNumber,
            document.RentalNumber,
            document.AccountNumber,
            document.CompanyCode,
            new Originator(document.Origin.Source, document.Origin.ClientSystem, document.Origin.UserName),
            document.DateTimeRequested);

        var lines = document.Lines.Select(line =>
        {
            var allocations = line.Allocations.Select(allocation => new PersistedAllocation(
                allocation.RentalDeviceLineNumber,
                new Quantity(allocation.RequestedQuantity),
                new Quantity(allocation.RemainingQuantity),
                Enum.Parse<AllocationStatus>(allocation.Status, ignoreCase: true),
                allocation.PlannedOffHireDate,
                allocation.History.Select(history => new AllocationHistory(
                    history.EventType,
                    new Quantity(history.Quantity),
                    history.OccurredAt,
                    history.Reason)).ToList()));

            return OffHireLine.LoadFromPersistence(line.ExternalLineNumberRef, line.CollectionInstructions, allocations);
        }).ToList();

        aggregate.LoadLines(lines);

        var history = document.History.Select(entry => new OffHireHistoryEntry(
            entry.ExternalLineNumberRef,
            entry.EventType,
            new Quantity(entry.Quantity),
            entry.OccurredAt,
            entry.Trigger,
            entry.Notes));

        aggregate.LoadHistory(history);

        return aggregate;
    }

    private static CosmosOffHireDocument MapToDocument(OffHireOrder aggregate)
    {
        return new CosmosOffHireDocument
        {
            Id = aggregate.Id,
            ContractNumber = aggregate.ContractNumber,
            RentalNumber = aggregate.RentalNumber,
            AccountNumber = aggregate.AccountNumber,
            CompanyCode = aggregate.CompanyCode,
            DateTimeRequested = aggregate.DateTimeRequested,
            Origin = new CosmosOrigin
            {
                Source = aggregate.Originator.Id,
                ClientSystem = aggregate.Originator.ClientSystem,
                UserName = aggregate.Originator.UserName
            },
            Lines = aggregate.Lines.Select(line => new CosmosOffHireLine
            {
                ExternalLineNumberRef = line.ExternalLineNumberRef,
                CollectionInstructions = line.CollectionInstructions,
                RequestedQuantity = line.RequestedQuantity.Value,
                OffHireDateTime = line.Allocations.First().PlannedOffHireDate,
                Allocations = line.Allocations.Select(allocation => new CosmosAllocation
                {
                    RentalDeviceLineNumber = allocation.RentalDeviceLineNumber,
                    RequestedQuantity = allocation.RequestedQuantity.Value,
                    RemainingQuantity = allocation.RemainingQuantity.Value,
                    Status = allocation.Status.ToString(),
                    PlannedOffHireDate = allocation.PlannedOffHireDate,
                    History = allocation.History.Select(history => new CosmosAllocationHistory
                    {
                        EventType = history.EventType,
                        OccurredAt = history.OccurredAt,
                        Quantity = history.Quantity.Value,
                        Reason = history.Reason
                    }).ToList()
                }).ToList()
            }).ToList(),
            History = aggregate.History.Select(entry => new CosmosHistoryEntry
            {
                ExternalLineNumberRef = entry.ExternalLineNumberRef,
                EventType = entry.EventType,
                Quantity = entry.Quantity.Value,
                OccurredAt = entry.OccurredAt,
                Trigger = entry.Trigger,
                Notes = entry.Notes
            }).ToList()
        };
    }

    private sealed class CosmosOffHireDocument
    {
        public string Id { get; set; } = default!;
        public string ContractNumber { get; set; } = default!;
        public string RentalNumber { get; set; } = default!;
        public string AccountNumber { get; set; } = default!;
        public string CompanyCode { get; set; } = default!;
        public System.DateTime DateTimeRequested { get; set; }
        public CosmosOrigin Origin { get; set; } = new();
        public List<CosmosOffHireLine> Lines { get; set; } = new();
        public List<CosmosHistoryEntry> History { get; set; } = new();
    }

    private sealed class CosmosOrigin
    {
        public string Source { get; set; } = string.Empty;
        public string ClientSystem { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
    }

    private sealed class CosmosOffHireLine
    {
        public string ExternalLineNumberRef { get; set; } = default!;
        public string CollectionInstructions { get; set; } = string.Empty;
        public decimal RequestedQuantity { get; set; }
        public System.DateTime OffHireDateTime { get; set; }
        public List<CosmosAllocation> Allocations { get; set; } = new();
    }

    private sealed class CosmosAllocation
    {
        public string RentalDeviceLineNumber { get; set; } = default!;
        public decimal RequestedQuantity { get; set; }
        public decimal RemainingQuantity { get; set; }
        public string Status { get; set; } = string.Empty;
        public System.DateTime PlannedOffHireDate { get; set; }
        public List<CosmosAllocationHistory> History { get; set; } = new();
    }

    private sealed class CosmosAllocationHistory
    {
        public string EventType { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public System.DateTime OccurredAt { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    private sealed class CosmosHistoryEntry
    {
        public string ExternalLineNumberRef { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public System.DateTime OccurredAt { get; set; }
        public string Trigger { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }
}
