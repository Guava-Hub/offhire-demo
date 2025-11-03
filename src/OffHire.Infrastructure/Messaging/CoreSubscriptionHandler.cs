using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OffHire.Application.Abstractions;
using OffHire.Application.Commands;

namespace OffHire.Infrastructure.Messaging;

public sealed class CoreSubscriptionHandler
{
    private readonly UpsertOffHireOrderCommandHandler _commandHandler;

    public CoreSubscriptionHandler(UpsertOffHireOrderCommandHandler commandHandler)
    {
        _commandHandler = commandHandler;
    }

    public Task ProcessAsync(OffHireOrderMessage message, CancellationToken cancellationToken)
    {
        var command = new UpsertOffHireOrderCommand(
            message.ContractNumber,
            message.RentalNumber,
            message.AccountNumber,
            message.CompanyCode,
            message.Originator,
            message.RequestedAt,
            MapLines(message.Lines));

        return _commandHandler.Handle(command, cancellationToken);
    }

    private static IReadOnlyCollection<LineRequest> MapLines(IReadOnlyCollection<OffHireOrderLineMessage> lines)
        => lines.Select(line => new LineRequest(
            line.ExternalLineNumberRef,
            line.LineNumberReferences,
            line.Quantity,
            line.OffHireDateTime,
            line.CollectionInstructions)).ToList();
}
