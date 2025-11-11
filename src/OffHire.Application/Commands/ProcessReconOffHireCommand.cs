using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OffHire.Application.Abstractions;
using OffHire.Domain.Models;
using OffHire.Domain.Services;
using OffHire.Domain.ValueObjects;

namespace OffHire.Application.Commands;

public sealed record ProcessReconOffHireCommand(ReconOffHireNotification Notification);

public sealed class ProcessReconOffHireCommandHandler
{
    private readonly IOffHireOrderRepository _repository;
    private readonly IDynamicsService _dynamicsService;
    private readonly IOffHirePlanner _planner;

    public ProcessReconOffHireCommandHandler(
        IOffHireOrderRepository repository,
        IDynamicsService dynamicsService,
        IOffHirePlanner planner)
    {
        _repository = repository;
        _dynamicsService = dynamicsService;
        _planner = planner;
    }

    public async Task Handle(ProcessReconOffHireCommand command, CancellationToken cancellationToken)
    {
        var notification = command.Notification;
        var aggregate = await _repository.FindAsync(notification.RentalNumber, notification.CompanyCode, cancellationToken);

        if (aggregate is null)
        {
            aggregate = OffHireOrder.Create(
                id: $"{notification.RentalNumber}:{notification.CompanyCode}",
                contractNumber: notification.RentalNumber,
                rentalNumber: notification.RentalNumber,
                accountNumber: notification.AccountNumber,
                companyCode: notification.CompanyCode,
                originator: new Originator("Recon", "ReconService", string.Empty),
                requestedAt: notification.Lines.Min(l => l.CollectionDateTime));

            foreach (var reconLine in notification.Lines)
            {
                aggregate.UpsertLine(
                    reconLine.ExternalLineReference,
                    new[] { reconLine.RentalDeviceLineNumber },
                    reconLine.Quantity,
                    reconLine.CollectionDateTime,
                    collectionInstructions: string.Empty);
            }
        }

        var result = aggregate.PlanOffHireFromRecon(notification, _planner);

        await _repository.SaveAsync(aggregate, cancellationToken);

        foreach (var commandToSend in result.IntegrationCommands)
        {
            await _dynamicsService.SendAsync(commandToSend, cancellationToken);
        }
    }
}
