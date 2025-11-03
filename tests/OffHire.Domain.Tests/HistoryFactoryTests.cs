using System;
using OffHire.Domain.Models;
using OffHire.Domain.ValueObjects;
using Xunit;

namespace OffHire.Domain.Tests;

public class HistoryFactoryTests
{
    [Fact]
    public void DynamicsUpdateDate_ForAllocation_ShouldReturnSucceededEvent_WhenSucceeded()
    {
        var when = new DateTime(2024, 5, 5, 12, 0, 0, DateTimeKind.Utc);
        var entry = AllocationHistory.DynamicsUpdateDate(when, succeeded: true, details: "request-123");

        Assert.Equal("DynamicsUpdateDateSucceeded", entry.EventType);
        Assert.Equal(Quantity.Zero, entry.Quantity);
        Assert.Equal(when, entry.OccurredAt);
        Assert.Equal("request-123", entry.Reason);
    }

    [Fact]
    public void DynamicsUpdateDate_ForDocument_ShouldReturnFailedEvent_WhenFailed()
    {
        var when = new DateTime(2024, 5, 6, 9, 30, 0, DateTimeKind.Utc);
        var entry = OffHireHistoryEntry.DynamicsUpdateDate("EXL-1", when, succeeded: false, details: "error: timeout");

        Assert.Equal("DynamicsUpdateDateFailed", entry.EventType);
        Assert.Equal("Dynamics", entry.Trigger);
        Assert.Equal(Quantity.Zero, entry.Quantity);
        Assert.Equal("error: timeout", entry.Notes);
        Assert.Equal("EXL-1", entry.ExternalLineNumberRef);
        Assert.Equal(when, entry.OccurredAt);
    }
}
