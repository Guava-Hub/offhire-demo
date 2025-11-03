using System.Threading;
using System.Threading.Tasks;
using OffHire.Application.Abstractions;

namespace OffHire.Infrastructure.Messaging;

public sealed class ServiceBusTopicClientStub : IServiceBusTopicClient
{
    private readonly CoreSubscriptionHandler _coreSubscription;
    private readonly DynamicsSubscriptionHandler _dynamicsSubscription;

    public ServiceBusTopicClientStub(
        CoreSubscriptionHandler coreSubscription,
        DynamicsSubscriptionHandler dynamicsSubscription)
    {
        _coreSubscription = coreSubscription;
        _dynamicsSubscription = dynamicsSubscription;
    }

    public async Task PublishAsync(OffHireOrderMessage message, CancellationToken cancellationToken)
    {
        await _coreSubscription.ProcessAsync(message, cancellationToken).ConfigureAwait(false);
        await _dynamicsSubscription.ProcessAsync(message, cancellationToken).ConfigureAwait(false);
    }
}
