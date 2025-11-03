using System.Threading;
using System.Threading.Tasks;
using OffHire.Application.Abstractions;
using OffHire.Domain.ValueObjects;

namespace OffHire.Infrastructure.Messaging;

public sealed class DynamicsResultProcessor
{
    private readonly IOffHireOrderRepository _repository;

    public DynamicsResultProcessor(IOffHireOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task ProcessAsync(DynamicsResultMessage message, CancellationToken cancellationToken)
    {
        var aggregate = await _repository.FindAsync(message.ContractNumber, message.CompanyCode, cancellationToken).ConfigureAwait(false);

        if (aggregate is null)
        {
            return;
        }

        foreach (var line in message.Lines)
        {
            aggregate.RecordDynamicsOutcome(
                line.ExternalLineNumberRef,
                new Quantity(line.Quantity),
                line.OccurredAt,
                line.RequestId,
                line.IsBackDate);
        }

        await _repository.SaveAsync(aggregate, cancellationToken).ConfigureAwait(false);
    }
}
