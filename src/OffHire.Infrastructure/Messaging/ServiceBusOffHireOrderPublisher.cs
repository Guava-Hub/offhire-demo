using System.Threading;
using System.Threading.Tasks;
using OffHire.Application.Abstractions;

namespace OffHire.Infrastructure.Messaging;

public interface IServiceBusTopicClient
{
    Task PublishAsync(OffHireOrderMessage message, CancellationToken cancellationToken);
}

public sealed class ServiceBusOffHireOrderPublisher : IOffHireOrderPublisher
{
    private readonly IServiceBusTopicClient _topicClient;

    public ServiceBusOffHireOrderPublisher(IServiceBusTopicClient topicClient)
    {
        _topicClient = topicClient;
    }

    public Task PublishAsync(OffHireOrderMessage message, CancellationToken cancellationToken)
        => _topicClient.PublishAsync(message, cancellationToken);
}
