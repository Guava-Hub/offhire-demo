using System;
using System.Collections.Generic;
using System.Linq;
using OffHire.Domain.ValueObjects;

namespace OffHire.Domain.Models;

public sealed class OffHireLine
{
    private readonly List<LineAllocation> _allocations = new();

    private OffHireLine(string externalLineNumberRef, string collectionInstructions)
    {
        ExternalLineNumberRef = externalLineNumberRef;
        CollectionInstructions = collectionInstructions ?? string.Empty;
    }

    public string ExternalLineNumberRef { get; }

    public string CollectionInstructions { get; private set; }

    public Quantity RequestedQuantity => _allocations.Aggregate(Quantity.Zero, (acc, allocation) => acc + allocation.RequestedQuantity);

    public IReadOnlyCollection<LineAllocation> Allocations => _allocations;

    public static OffHireLine Create(string externalLineNumberRef, string collectionInstructions)
        => new(externalLineNumberRef, collectionInstructions);

    internal static OffHireLine LoadFromPersistence(
        string externalLineNumberRef,
        string collectionInstructions,
        IEnumerable<PersistedAllocation> allocations)
    {
        var line = new OffHireLine(externalLineNumberRef, collectionInstructions);

        foreach (var allocation in allocations ?? Enumerable.Empty<PersistedAllocation>())
        {
            line._allocations.Add(
                LineAllocation.LoadFromPersistence(
                    allocation.RentalDeviceLineNumber,
                    allocation.RequestedQuantity,
                    allocation.RemainingQuantity,
                    allocation.PlannedOffHireDate,
                    allocation.Status,
                    allocation.History ?? Enumerable.Empty<AllocationHistory>()));
        }

        return line;
    }

    public void MergeRequest(IEnumerable<string> rentalDeviceLineNumbers, Quantity requestedQuantity, DateTime offHireDateTime)
    {
        CollectionInstructions = CollectionInstructions ?? string.Empty;
        var deviceNumbers = rentalDeviceLineNumbers.ToArray();

        if (deviceNumbers.Length == 0)
        {
            throw new InvalidOperationException("At least one rental device line number must be provided.");
        }

        if (requestedQuantity.Value > deviceNumbers.Length && deviceNumbers.Length > 1)
        {
            throw new InvalidOperationException("Requested quantity cannot exceed number of allocations when splitting equally.");
        }

        if (deviceNumbers.Length == 1)
        {
            var allocation = _allocations.SingleOrDefault(a => a.RentalDeviceLineNumber == deviceNumbers[0]);
            if (allocation is null)
            {
                allocation = LineAllocation.Create(deviceNumbers[0], requestedQuantity, offHireDateTime);
                _allocations.Add(allocation);
            }
            else
            {
                allocation.UpdateRequestedQuantity(requestedQuantity, offHireDateTime);
            }
        }
        else
        {
            // Multi-line scenario: allocate quantity per device.
            EnsureAllocations(deviceNumbers, offHireDateTime);
            var perAllocationQuantity = requestedQuantity.Value switch
            {
                <= 0 => throw new InvalidOperationException("Requested quantity must be greater than zero."),
                _ when requestedQuantity.Value == deviceNumbers.Length => Quantity.FromInt(1),
                _ => new Quantity(requestedQuantity.Value / deviceNumbers.Length)
            };

            var remaining = requestedQuantity.Value;

            foreach (var allocation in _allocations.Where(a => deviceNumbers.Contains(a.RentalDeviceLineNumber)))
            {
                var quantityForAllocation = remaining >= 1 ? Quantity.FromInt(1) : new Quantity(remaining);
                allocation.UpdateRequestedQuantity(quantityForAllocation, offHireDateTime);
                remaining -= quantityForAllocation.Value;
                if (remaining <= 0)
                {
                    break;
                }
            }
        }
    }

    private void EnsureAllocations(IEnumerable<string> deviceNumbers, DateTime offHireDateTime)
    {
        foreach (var deviceNumber in deviceNumbers)
        {
            var allocation = _allocations.SingleOrDefault(a => a.RentalDeviceLineNumber == deviceNumber);
            if (allocation is null)
            {
                _allocations.Add(LineAllocation.Create(deviceNumber, Quantity.Zero, offHireDateTime));
            }
        }
    }
}

public sealed class LineAllocation
{
    private readonly List<AllocationHistory> _history = new();

    private LineAllocation(string rentalDeviceLineNumber, Quantity requestedQuantity, DateTime offHireDateTime)
    {
        RentalDeviceLineNumber = rentalDeviceLineNumber;
        RequestedQuantity = requestedQuantity;
        RemainingQuantity = requestedQuantity;
        PlannedOffHireDate = offHireDateTime;
        Status = AllocationStatus.Pending;
        AddHistory(AllocationHistory.Requested(requestedQuantity, offHireDateTime));
    }

    private LineAllocation(
        string rentalDeviceLineNumber,
        Quantity requestedQuantity,
        Quantity remainingQuantity,
        DateTime plannedOffHireDate,
        AllocationStatus status,
        IEnumerable<AllocationHistory> history)
    {
        RentalDeviceLineNumber = rentalDeviceLineNumber;
        RequestedQuantity = requestedQuantity;
        RemainingQuantity = remainingQuantity;
        PlannedOffHireDate = plannedOffHireDate;
        Status = status;
        _history.AddRange(history ?? Enumerable.Empty<AllocationHistory>());
    }

    public string RentalDeviceLineNumber { get; }

    public Quantity RequestedQuantity { get; private set; }

    public Quantity RemainingQuantity { get; private set; }

    public DateTime PlannedOffHireDate { get; private set; }

    public AllocationStatus Status { get; private set; }

    public IReadOnlyCollection<AllocationHistory> History => _history;

    public static LineAllocation Create(string rentalDeviceLineNumber, Quantity requestedQuantity, DateTime offHireDateTime)
        => new(rentalDeviceLineNumber, requestedQuantity, offHireDateTime);

    internal static LineAllocation LoadFromPersistence(
        string rentalDeviceLineNumber,
        Quantity requestedQuantity,
        Quantity remainingQuantity,
        DateTime plannedOffHireDate,
        AllocationStatus status,
        IEnumerable<AllocationHistory> history)
        => new(rentalDeviceLineNumber, requestedQuantity, remainingQuantity, plannedOffHireDate, status, history);

    public void UpdateRequestedQuantity(Quantity quantity, DateTime offHireDateTime)
    {
        RequestedQuantity = quantity;
        RemainingQuantity = quantity;
        PlannedOffHireDate = offHireDateTime;
        Status = AllocationStatus.Pending;
        AddHistory(AllocationHistory.Requested(quantity, offHireDateTime));
    }

    public AllocationAction OffHire(Quantity quantity, DateTime when, string reason)
    {
        if (quantity.Value > RemainingQuantity.Value)
        {
            throw new InvalidOperationException("Cannot off-hire more than the remaining quantity.");
        }

        RemainingQuantity -= quantity;

        Status = RemainingQuantity.IsZero ? AllocationStatus.OffHired : AllocationStatus.PartiallyOffHired;
        AddHistory(AllocationHistory.OffHired(quantity, when, reason));

        return new AllocationAction(AllocationActionType.OffHireDynamics, quantity, this, when, reason);
    }

    public AllocationAction PushToPast(Quantity quantity, DateTime when, string reason)
    {
        if (quantity.Value <= 0)
        {
            return new AllocationAction(AllocationActionType.PushQuantityPast, Quantity.Zero, this, when, reason);
        }

        Status = AllocationStatus.PushedToPast;
        AddHistory(AllocationHistory.PushedToPast(quantity, when, reason));
        return new AllocationAction(AllocationActionType.PushQuantityPast, quantity, this, when, reason);
    }

    private void AddHistory(AllocationHistory historyEntry) => _history.Add(historyEntry);
}

internal sealed record PersistedAllocation(
    string RentalDeviceLineNumber,
    Quantity RequestedQuantity,
    Quantity RemainingQuantity,
    AllocationStatus Status,
    DateTime PlannedOffHireDate,
    IEnumerable<AllocationHistory> History);

public sealed record AllocationHistory(string EventType, Quantity Quantity, DateTime OccurredAt, string Reason)
{
    public static AllocationHistory Requested(Quantity quantity, DateTime when)
        => new("Requested", quantity, when, reason: string.Empty);

    public static AllocationHistory OffHired(Quantity quantity, DateTime when, string reason)
        => new("OffHired", quantity, when, reason);

    public static AllocationHistory PushedToPast(Quantity quantity, DateTime when, string reason)
        => new("PushedToPast", quantity, when, reason);

    public static AllocationHistory DynamicsUpdateDate(DateTime when, bool succeeded, string details)
        => new(
            succeeded ? "DynamicsUpdateDateSucceeded" : "DynamicsUpdateDateFailed",
            Quantity.Zero,
            when,
            details);
}

public enum AllocationStatus
{
    Pending,
    PartiallyOffHired,
    OffHired,
    PushedToPast
}

public sealed record AllocationAction(AllocationActionType Type, Quantity Quantity, LineAllocation Allocation, DateTime EffectiveDate, string Reason);

public enum AllocationActionType
{
    OffHireDynamics,
    PushQuantityPast
}
