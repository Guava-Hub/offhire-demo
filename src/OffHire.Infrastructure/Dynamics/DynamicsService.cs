using System.Threading;
using System.Threading.Tasks;
using OffHire.Application.Abstractions;
using OffHire.Domain.Services;

namespace OffHire.Infrastructure.Dynamics;

public sealed class DynamicsService : IDynamicsService
{
    private readonly IDynamicsClient _client;

    public DynamicsService(IDynamicsClient client)
    {
        _client = client;
    }

    public Task SendAsync(IntegrationCommand command, CancellationToken cancellationToken)
        => _client.SendAsync(command, cancellationToken);
}

public interface IDynamicsClient
{
    Task SendAsync(IntegrationCommand command, CancellationToken cancellationToken);
}
