using System.Threading;
using System.Threading.Tasks;
using OffHire.Domain.Services;

namespace OffHire.Application.Abstractions;

public interface IDynamicsService
{
    Task SendAsync(IntegrationCommand command, CancellationToken cancellationToken);
}
