using System.Threading;
using System.Threading.Tasks;
using OffHire.Application.Abstractions;

namespace OffHire.Infrastructure.Messaging;

public sealed class DynamicsResultQueueStub : IDynamicsResultQueue
{
    private readonly DynamicsResultProcessor _processor;

    public DynamicsResultQueueStub(DynamicsResultProcessor processor)
    {
        _processor = processor;
    }

    public Task PublishAsync(DynamicsResultMessage message, CancellationToken cancellationToken)
        => _processor.ProcessAsync(message, cancellationToken);
}
