using System;
using OffHire.Domain.ValueObjects;

namespace OffHire.Domain.Models;

public sealed record OffHireHistoryEntry(
    string ExternalLineNumberRef,
    string EventType,
    Quantity Quantity,
    DateTime OccurredAt,
    string Trigger,
    string Notes)
{
    public static OffHireHistoryEntry RequestedFromApi(string externalLineNumberRef, Quantity quantity, DateTime requestedAt)
        => new(externalLineNumberRef, "OffHireRequested", quantity, requestedAt, "API", string.Empty);

    public static OffHireHistoryEntry PlannedFromRecon(string externalLineNumberRef, Quantity quantity, DateTime plannedAt, string notes)
        => new(externalLineNumberRef, "ReconPlanned", quantity, plannedAt, "Recon", notes);

    public static OffHireHistoryEntry DynamicsOffHire(string externalLineNumberRef, Quantity quantity, DateTime at, string requestId)
        => new(externalLineNumberRef, "DynamicsOffHire", quantity, at, "Dynamics", requestId);

    public static OffHireHistoryEntry DynamicsBackDate(string externalLineNumberRef, Quantity quantity, DateTime at, string requestId)
        => new(externalLineNumberRef, "DynamicsBackDate", quantity, at, "Dynamics", requestId);

    public static OffHireHistoryEntry DynamicsUpdateDate(
        string externalLineNumberRef,
        DateTime at,
        bool succeeded,
        string details)
        => new(
            externalLineNumberRef,
            succeeded ? "DynamicsUpdateDateSucceeded" : "DynamicsUpdateDateFailed",
            Quantity.Zero,
            at,
            "Dynamics",
            details);
}
