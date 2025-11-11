using System.Threading;
using System.Threading.Tasks;
using OffHire.Domain.Models;

namespace OffHire.Application.Abstractions;

public interface IOffHireOrderRepository
{
    Task<OffHireOrder?> FindAsync(string contractNumber, string companyCode, CancellationToken cancellationToken);

    Task<OffHireOrder?> FindByRentalDeviceAsync(
        string rentalDeviceLineNumber,
        string companyCode,
        string accountNumber,
        CancellationToken cancellationToken);

    Task SaveAsync(OffHireOrder aggregate, CancellationToken cancellationToken);
}
