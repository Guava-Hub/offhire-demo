using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OffHire.Application.Abstractions;
using OffHire.Application.Commands;
using OffHire.Domain.Models;
using OffHire.Domain.Services;
using OffHire.Domain.ValueObjects;
using Xunit;

namespace OffHire.Application.Tests;

public class ProcessReconOffHireCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldSearchByRentalDevice_WhenNotificationContainsDevice()
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

        var planner = new FakePlanner();
        var dynamicsService = new FakeDynamicsService();
        var handler = new ProcessReconOffHireCommandHandler(repository, dynamicsService, planner);

        var notification = ReconOffHireNotification.FromPayload(
            rentalNumber: "rental",
            accountNumber: "account",
            companyCode: "sas",
            lines: new[]
            {
                new ReconOffHireLine(
                    rentalDeviceLineNumber: "RDL-1",
                    quantity: Quantity.FromInt(1),
                    collectionDateTime: DateTime.UtcNow,
                    externalLineReference: "EXT-1")
            });

        await handler.Handle(new ProcessReconOffHireCommand(notification), CancellationToken.None);

        var rentalDeviceSearch = Assert.Single(repository.FindByRentalDeviceCalls);
        Assert.Equal("RDL-1", rentalDeviceSearch.RentalDeviceLineNumber);
        Assert.Equal("sas", rentalDeviceSearch.CompanyCode);
        Assert.Equal("account", rentalDeviceSearch.AccountNumber);
        Assert.Empty(repository.FindByContractCalls);
        Assert.Same(repository.RentalDeviceResult, repository.SavedAggregate);
    }

    [Fact]
    public async Task Handle_ShouldFallbackToContractSearch_WhenRentalDeviceNotFound()
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

        var planner = new FakePlanner();
        var dynamicsService = new FakeDynamicsService();
        var handler = new ProcessReconOffHireCommandHandler(repository, dynamicsService, planner);

        var notification = ReconOffHireNotification.FromPayload(
            rentalNumber: "contract",
            accountNumber: "account",
            companyCode: "sas",
            lines: new[]
            {
                new ReconOffHireLine(
                    rentalDeviceLineNumber: "RDL-1",
                    quantity: Quantity.FromInt(1),
                    collectionDateTime: DateTime.UtcNow,
                    externalLineReference: "EXT-1")
            });

        await handler.Handle(new ProcessReconOffHireCommand(notification), CancellationToken.None);

        Assert.Single(repository.FindByRentalDeviceCalls);
        var contractSearch = Assert.Single(repository.FindByContractCalls);
        Assert.Equal("contract", contractSearch.ContractNumber);
        Assert.Equal("sas", contractSearch.CompanyCode);
        Assert.Same(repository.ContractResult, repository.SavedAggregate);
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

    private sealed class FakePlanner : IOffHirePlanner
    {
        public OffHirePlanningResult Plan(OffHirePlanningContext context) => OffHirePlanningResult.Empty();
    }

    private sealed class FakeDynamicsService : IDynamicsService
    {
        public List<IntegrationCommand> Commands { get; } = new();

        public Task SendAsync(IntegrationCommand command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.CompletedTask;
        }
    }
}
