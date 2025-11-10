using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OffHire.Application.Abstractions;
using OffHire.Application.Commands;
using OffHire.Domain.Models;
using OffHire.Domain.ValueObjects;
using Xunit;

namespace OffHire.Application.Tests;

public class UpsertOffHireOrderCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldSearchByRentalDevice_WhenSingleLineHasReference()
    {
        var repository = new FakeOffHireOrderRepository
        {
            RentalDeviceResult = OffHireOrder.Create(
                "existing:sas",
                "contract",
                "rental",
                "account",
                "sas",
                new Originator("Test", "System", string.Empty),
                DateTime.UtcNow)
        };

        var handler = new UpsertOffHireOrderCommandHandler(repository);

        var command = new UpsertOffHireOrderCommand(
            ContractNumber: "contract",
            RentalNumber: "rental",
            AccountNumber: "account",
            CompanyCode: "sas",
            Originator: new Originator("Source", "Client", "User"),
            RequestedAt: DateTime.UtcNow,
            Requester: null,
            Header: null,
            Lines: new[]
            {
                new LineRequest(
                    ExternalLineNumberRef: "EXT-1",
                    LineNumberReferences: new[] { "RDL-1" },
                    Quantity: 1,
                    OffHireDateTime: DateTime.UtcNow,
                    CollectionInstructions: string.Empty)
            });

        var aggregate = await handler.Handle(command, CancellationToken.None);

        var rentalDeviceSearch = Assert.Single(repository.FindByRentalDeviceCalls);
        Assert.Equal("RDL-1", rentalDeviceSearch.RentalDeviceLineNumber);
        Assert.Equal("sas", rentalDeviceSearch.CompanyCode);
        Assert.Equal("account", rentalDeviceSearch.AccountNumber);
        Assert.Empty(repository.FindByContractCalls);
        Assert.Same(repository.RentalDeviceResult, aggregate);
        Assert.Same(aggregate, repository.SavedAggregate);
    }

    [Fact]
    public async Task Handle_ShouldFallbackToContract_WhenRentalDeviceUnavailable()
    {
        var repository = new FakeOffHireOrderRepository
        {
            ContractResult = OffHireOrder.Create(
                "contract:sas",
                "contract",
                "rental",
                "account",
                "sas",
                new Originator("Test", "System", string.Empty),
                DateTime.UtcNow)
        };

        var handler = new UpsertOffHireOrderCommandHandler(repository);

        var command = new UpsertOffHireOrderCommand(
            ContractNumber: "contract",
            RentalNumber: "rental",
            AccountNumber: string.Empty,
            CompanyCode: "sas",
            Originator: new Originator("Source", "Client", "User"),
            RequestedAt: DateTime.UtcNow,
            Requester: null,
            Header: null,
            Lines: new[]
            {
                new LineRequest(
                    ExternalLineNumberRef: "EXT-1",
                    LineNumberReferences: Array.Empty<string>(),
                    Quantity: 1,
                    OffHireDateTime: DateTime.UtcNow,
                    CollectionInstructions: string.Empty)
            });

        var aggregate = await handler.Handle(command, CancellationToken.None);

        Assert.Empty(repository.FindByRentalDeviceCalls);
        var contractSearch = Assert.Single(repository.FindByContractCalls);
        Assert.Equal("contract", contractSearch.ContractNumber);
        Assert.Equal("sas", contractSearch.CompanyCode);
        Assert.Same(repository.ContractResult, aggregate);
        Assert.Same(aggregate, repository.SavedAggregate);
    }

    private sealed class FakeOffHireOrderRepository : IOffHireOrderRepository
    {
        public List<(string RentalDeviceLineNumber, string CompanyCode, string AccountNumber)> FindByRentalDeviceCalls { get; } = new();
        public List<(string ContractNumber, string CompanyCode)> FindByContractCalls { get; } = new();
        public OffHireOrder? RentalDeviceResult { get; set; }
        public OffHireOrder? ContractResult { get; set; }
        public OffHireOrder? SavedAggregate { get; private set; }

        public Task<OffHireOrder?> FindAsync(string contractNumber, string companyCode, CancellationToken cancellationToken)
        {
            FindByContractCalls.Add((contractNumber, companyCode));
            return Task.FromResult(ContractResult);
        }

        public Task<OffHireOrder?> FindByRentalDeviceAsync(string rentalDeviceLineNumber, string companyCode, string accountNumber, CancellationToken cancellationToken)
        {
            FindByRentalDeviceCalls.Add((rentalDeviceLineNumber, companyCode, accountNumber));
            return Task.FromResult(RentalDeviceResult);
        }

        public Task SaveAsync(OffHireOrder aggregate, CancellationToken cancellationToken)
        {
            SavedAggregate = aggregate;
            return Task.CompletedTask;
        }
    }
}
