using System.Collections.Generic;
using System.Linq;
using OffHire.Domain.Models;

namespace OffHire.Domain.Services;

public sealed class OffHirePlanningContext
{
    public OffHirePlanningContext(OffHireOrder aggregate, ReconOffHireNotification notification)
    {
        Aggregate = aggregate;
        Notification = notification;
    }

    public OffHireOrder Aggregate { get; }

    public ReconOffHireNotification Notification { get; }

    public IEnumerable<LineAllocation> FindAllocations(string rentalDeviceLineNumber)
        => Aggregate.Lines.SelectMany(line => line.Allocations)
            .Where(allocation => allocation.RentalDeviceLineNumber == rentalDeviceLineNumber);

    public OffHireLine EnsureLine(string externalLineReference)
        => Aggregate.EnsureLine(externalLineReference);
}
