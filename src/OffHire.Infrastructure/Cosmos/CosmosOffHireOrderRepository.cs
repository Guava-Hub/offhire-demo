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

    public async Task<OffHireOrder?> FindByRentalDeviceAsync(
        string rentalDeviceLineNumber,
        string companyCode,
        string accountNumber,
        CancellationToken cancellationToken)
    {
        var query = new QueryDefinition(
                "SELECT * FROM c JOIN l IN c.lines JOIN a IN l.allocations " +
                "WHERE c.accountNumber = @accountNumber AND c.companyCode = @companyCode AND a.rentalDeviceLineNumber = @line")
            .WithParameter("@accountNumber", accountNumber)
            .WithParameter("@companyCode", companyCode)
            .WithParameter("@line", rentalDeviceLineNumber);

        var iterator = _container.GetItemQueryIterator<CosmosOffHireDocument>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(companyCode)
            });

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            var document = response.Resource.FirstOrDefault();

            if (document is not null)
            {
                return MapToDomain(document);
            }
        }

        return null;
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

        var requester = document.Requester is not null ? MapRequester(document.Requester) : null;
        var header = document.Header is not null ? MapHeader(document.Header) : null;
        aggregate.LoadRequestDetails(requester, header);

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
            Requester = MapRequester(aggregate.Requester),
            Header = MapHeader(aggregate.Header),
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

    private static Requester MapRequester(CosmosRequester requester)
        => new(
            new PersonDetails(
                requester.PersonDetails.Title,
                requester.PersonDetails.Name,
                requester.PersonDetails.DateOfBirth),
            new ContactDetails(
                requester.ContactDetails.TelephoneNumber,
                requester.ContactDetails.MobileNumber,
                requester.ContactDetails.FaxNumber,
                requester.ContactDetails.EmailAddress));

    private static OffHireHeader MapHeader(CosmosHeader header)
        => new(
            header.CollectionNotes,
            new SpeedyReferences(header.SpeedyReferences.ContractNumber),
            new CollectionDetails(
                new PersonDetails(
                    header.CollectionDetails.PersonDetails.Title,
                    header.CollectionDetails.PersonDetails.Name,
                    header.CollectionDetails.PersonDetails.DateOfBirth),
                new CollectionAddress(
                    header.CollectionDetails.CollectionAddress.Name,
                    header.CollectionDetails.CollectionAddress.AddressLine1,
                    header.CollectionDetails.CollectionAddress.AddressLine2,
                    header.CollectionDetails.CollectionAddress.Street,
                    header.CollectionDetails.CollectionAddress.City,
                    header.CollectionDetails.CollectionAddress.County,
                    header.CollectionDetails.CollectionAddress.PostCode,
                    header.CollectionDetails.CollectionAddress.State,
                    header.CollectionDetails.CollectionAddress.Country),
                new ContactDetails(
                    header.CollectionDetails.ContactDetails.TelephoneNumber,
                    header.CollectionDetails.ContactDetails.MobileNumber,
                    header.CollectionDetails.ContactDetails.FaxNumber,
                    header.CollectionDetails.ContactDetails.EmailAddress)));

    private static CosmosRequester? MapRequester(Requester? requester)
        => requester is null
            ? null
            : new CosmosRequester
            {
                PersonDetails = new CosmosPersonDetails
                {
                    Title = requester.PersonDetails.Title,
                    Name = requester.PersonDetails.Name,
                    DateOfBirth = requester.PersonDetails.DateOfBirth
                },
                ContactDetails = new CosmosContactDetails
                {
                    TelephoneNumber = requester.ContactDetails.TelephoneNumber,
                    MobileNumber = requester.ContactDetails.MobileNumber,
                    FaxNumber = requester.ContactDetails.FaxNumber,
                    EmailAddress = requester.ContactDetails.EmailAddress
                }
            };

    private static CosmosHeader? MapHeader(OffHireHeader? header)
        => header is null
            ? null
            : new CosmosHeader
            {
                CollectionNotes = header.CollectionNotes,
                SpeedyReferences = new CosmosSpeedyReferences
                {
                    ContractNumber = header.SpeedyReferences.ContractNumber
                },
                CollectionDetails = new CosmosCollectionDetails
                {
                    PersonDetails = new CosmosPersonDetails
                    {
                        Title = header.CollectionDetails.PersonDetails.Title,
                        Name = header.CollectionDetails.PersonDetails.Name,
                        DateOfBirth = header.CollectionDetails.PersonDetails.DateOfBirth
                    },
                    CollectionAddress = new CosmosCollectionAddress
                    {
                        Name = header.CollectionDetails.CollectionAddress.Name,
                        AddressLine1 = header.CollectionDetails.CollectionAddress.AddressLine1,
                        AddressLine2 = header.CollectionDetails.CollectionAddress.AddressLine2,
                        Street = header.CollectionDetails.CollectionAddress.Street,
                        City = header.CollectionDetails.CollectionAddress.City,
                        County = header.CollectionDetails.CollectionAddress.County,
                        PostCode = header.CollectionDetails.CollectionAddress.PostCode,
                        State = header.CollectionDetails.CollectionAddress.State,
                        Country = header.CollectionDetails.CollectionAddress.Country
                    },
                    ContactDetails = new CosmosContactDetails
                    {
                        TelephoneNumber = header.CollectionDetails.ContactDetails.TelephoneNumber,
                        MobileNumber = header.CollectionDetails.ContactDetails.MobileNumber,
                        FaxNumber = header.CollectionDetails.ContactDetails.FaxNumber,
                        EmailAddress = header.CollectionDetails.ContactDetails.EmailAddress
                    }
                }
            };

    private sealed class CosmosOffHireDocument
    {
        public string Id { get; set; } = default!;
        public string ContractNumber { get; set; } = default!;
        public string RentalNumber { get; set; } = default!;
        public string AccountNumber { get; set; } = default!;
        public string CompanyCode { get; set; } = default!;
        public System.DateTime DateTimeRequested { get; set; }
        public CosmosOrigin Origin { get; set; } = new();
        public CosmosRequester? Requester { get; set; }
        public CosmosHeader? Header { get; set; }
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

    private sealed class CosmosRequester
    {
        public CosmosPersonDetails PersonDetails { get; set; } = new();
        public CosmosContactDetails ContactDetails { get; set; } = new();
    }

    private sealed class CosmosPersonDetails
    {
        public string Title { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public System.DateTime? DateOfBirth { get; set; }
    }

    private sealed class CosmosContactDetails
    {
        public string TelephoneNumber { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public string FaxNumber { get; set; } = string.Empty;
        public string EmailAddress { get; set; } = string.Empty;
    }

    private sealed class CosmosHeader
    {
        public string CollectionNotes { get; set; } = string.Empty;
        public CosmosSpeedyReferences SpeedyReferences { get; set; } = new();
        public CosmosCollectionDetails CollectionDetails { get; set; } = new();
    }

    private sealed class CosmosSpeedyReferences
    {
        public string ContractNumber { get; set; } = string.Empty;
    }

    private sealed class CosmosCollectionDetails
    {
        public CosmosPersonDetails PersonDetails { get; set; } = new();
        public CosmosCollectionAddress CollectionAddress { get; set; } = new();
        public CosmosContactDetails ContactDetails { get; set; } = new();
    }

    private sealed class CosmosCollectionAddress
    {
        public string Name { get; set; } = string.Empty;
        public string AddressLine1 { get; set; } = string.Empty;
        public string AddressLine2 { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string County { get; set; } = string.Empty;
        public string PostCode { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
    }
}
