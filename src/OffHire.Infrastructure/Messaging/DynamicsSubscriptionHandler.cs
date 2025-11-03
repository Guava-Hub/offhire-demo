using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OffHire.Application.Abstractions;
using OffHire.Domain.Services;

namespace OffHire.Infrastructure.Messaging;

public sealed class DynamicsSubscriptionHandler
{
    private readonly IDynamicsService _dynamicsService;
    private readonly IDynamicsResultQueue _resultQueue;

    public DynamicsSubscriptionHandler(
        IDynamicsService dynamicsService,
        IDynamicsResultQueue resultQueue)
    {
        _dynamicsService = dynamicsService;
        _resultQueue = resultQueue;
    }

    public async Task ProcessAsync(OffHireOrderMessage message, CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid().ToString("N");

        var payload = new
        {
            message.ContractNumber,
            message.RentalNumber,
            message.CompanyCode,
            RequestedAt = message.RequestedAt,
            Lines = message.Lines.Select(line => new
            {
                line.ExternalLineNumberRef,
                LineNumberReferences = line.LineNumberReferences,
                line.Quantity,
                line.OffHireDateTime,
                line.CollectionInstructions
            }).ToArray()
        };

        var integrationCommand = new IntegrationCommand("OffHireOrderSubmitted", payload, correlationId);
        await _dynamicsService.SendAsync(integrationCommand, cancellationToken).ConfigureAwait(false);

        var resultLines = message.Lines
            .Select(line => new DynamicsResultLineMessage(
                line.ExternalLineNumberRef,
                line.Quantity,
                DateTime.UtcNow,
                correlationId,
                IsBackDate: false))
            .ToList();

        var resultMessage = new DynamicsResultMessage(
            message.ContractNumber,
            message.CompanyCode,
            resultLines);

        await _resultQueue.PublishAsync(resultMessage, cancellationToken).ConfigureAwait(false);
    }
}
