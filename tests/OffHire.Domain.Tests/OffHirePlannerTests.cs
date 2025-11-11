using System;
using System.Linq;
using OffHire.Domain.Models;
using OffHire.Domain.Services;
using OffHire.Domain.ValueObjects;
using Xunit;

namespace OffHire.Domain.Tests;

public class OffHirePlannerTests
{
    private readonly OffHirePlanner _planner = new();

    [Fact]
    public void Plan_ShouldOffHireFullQuantity_WhenNoApiUpdateExists()
    {
        var order = OffHireOrder.Create("order-1", "contract", "rental", "account", "sas", new Originator("Recon", "Recon", string.Empty), DateTime.UtcNow);
        var notification = ReconOffHireNotification.FromPayload("rental", "account", "sas", new[]
        {
            new ReconOffHireLine("RDL-1", Quantity.FromInt(1), DateTime.UtcNow.AddDays(2), "EXL-1")
        });

        var context = new OffHirePlanningContext(order, notification);
        var result = _planner.Plan(context);

        Assert.Single(result.IntegrationCommands);
        Assert.Equal("DynamicsOffHire", result.IntegrationCommands.Single().Type);
    }
}
