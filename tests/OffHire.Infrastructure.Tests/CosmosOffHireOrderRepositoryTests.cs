using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Azure.Cosmos;
using OffHire.Domain.Models;
using OffHire.Domain.ValueObjects;
using OffHire.Infrastructure.Cosmos;
using Xunit;

namespace OffHire.Infrastructure.Tests;

public class CosmosOffHireOrderRepositoryTests
{
    [Fact]
    public void MapToDomain_ShouldPreserveAllocationPlannedDates()
    {
        var document = CreateDocument();
        var aggregate = InvokeMapToDomain(document);

        var line = Assert.Single(aggregate.Lines);
        var allocations = line.Allocations.OrderBy(a => a.PlannedOffHireDate).ToArray();

        Assert.Equal(2, allocations.Length);
        Assert.Equal(new DateTime(2024, 5, 1, 8, 30, 0, DateTimeKind.Utc), allocations[0].PlannedOffHireDate);
        Assert.Equal(new DateTime(2024, 5, 2, 9, 45, 0, DateTimeKind.Utc), allocations[1].PlannedOffHireDate);

        Assert.Equal(AllocationStatus.Pending, allocations[0].Status);
        Assert.Equal(AllocationStatus.OffHired, allocations[1].Status);

        Assert.Equal(Quantity.FromInt(2), allocations[0].RequestedQuantity);
        Assert.Equal(Quantity.FromInt(1), allocations[0].RemainingQuantity);
        Assert.Equal(Quantity.FromInt(3), allocations[1].RequestedQuantity);
        Assert.Equal(Quantity.Zero, allocations[1].RemainingQuantity);

        Assert.Collection(
            allocations[0].History,
            entry =>
            {
                Assert.Equal("Requested", entry.EventType);
                Assert.Equal(new DateTime(2024, 4, 30, 12, 0, 0, DateTimeKind.Utc), entry.OccurredAt);
            });

        Assert.Collection(
            allocations[1].History,
            entry =>
            {
                Assert.Equal("OffHired", entry.EventType);
                Assert.Equal(new DateTime(2024, 4, 29, 15, 30, 0, DateTimeKind.Utc), entry.OccurredAt);
            });
    }

    [Fact]
    public void RentalDeviceQuery_ShouldProjectRootDocument()
    {
        var method = typeof(CosmosOffHireOrderRepository)
            .GetMethod("CreateRentalDeviceQueryDefinition", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CreateRentalDeviceQueryDefinition not found.");

        var query = (QueryDefinition)method.Invoke(null, new object[] { new[] { "RDL-1", "RDL-2" }, "sas" })!;

        Assert.Contains("SELECT VALUE c", query.QueryText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("JOIN l IN c.lines", query.QueryText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("JOIN a IN l.allocations", query.QueryText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ARRAY_CONTAINS(@rentalDeviceLineNumbers, a.rentalDeviceLineNumber)", query.QueryText, StringComparison.OrdinalIgnoreCase);
    }

    private static OffHireOrder InvokeMapToDomain(object document)
    {
        var method = typeof(CosmosOffHireOrderRepository)
            .GetMethod("MapToDomain", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("MapToDomain not found.");

        return (OffHireOrder)method.Invoke(null, new[] { document })!;
    }

    private static object CreateDocument()
    {
        var repositoryType = typeof(CosmosOffHireOrderRepository);

        var documentType = repositoryType.GetNestedType("CosmosOffHireDocument", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Document type not found.");
        var originType = repositoryType.GetNestedType("CosmosOrigin", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Origin type not found.");
        var lineType = repositoryType.GetNestedType("CosmosOffHireLine", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Line type not found.");
        var allocationType = repositoryType.GetNestedType("CosmosAllocation", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Allocation type not found.");
        var historyType = repositoryType.GetNestedType("CosmosAllocationHistory", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Allocation history type not found.");
        var orderHistoryType = repositoryType.GetNestedType("CosmosHistoryEntry", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Order history type not found.");

        var document = Activator.CreateInstance(documentType)!;
        SetProperty(document, "Id", "contract:sas");
        SetProperty(document, "ContractNumber", "contract");
        SetProperty(document, "RentalNumber", "rental");
        SetProperty(document, "AccountNumber", "account");
        SetProperty(document, "CompanyCode", "sas");
        SetProperty(document, "DateTimeRequested", new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc));

        var origin = Activator.CreateInstance(originType)!;
        SetProperty(origin, "Source", "Recon");
        SetProperty(origin, "ClientSystem", "Recon");
        SetProperty(origin, "UserName", "system");
        SetProperty(document, "Origin", origin);

        var firstAllocationHistory = Activator.CreateInstance(historyType)!;
        SetProperty(firstAllocationHistory, "EventType", "Requested");
        SetProperty(firstAllocationHistory, "Quantity", 2m);
        SetProperty(firstAllocationHistory, "OccurredAt", new DateTime(2024, 4, 30, 12, 0, 0, DateTimeKind.Utc));
        SetProperty(firstAllocationHistory, "Reason", string.Empty);

        var secondAllocationHistory = Activator.CreateInstance(historyType)!;
        SetProperty(secondAllocationHistory, "EventType", "OffHired");
        SetProperty(secondAllocationHistory, "Quantity", 3m);
        SetProperty(secondAllocationHistory, "OccurredAt", new DateTime(2024, 4, 29, 15, 30, 0, DateTimeKind.Utc));
        SetProperty(secondAllocationHistory, "Reason", "Completed");

        var firstAllocation = Activator.CreateInstance(allocationType)!;
        SetProperty(firstAllocation, "RentalDeviceLineNumber", "RDL-1");
        SetProperty(firstAllocation, "RequestedQuantity", 2m);
        SetProperty(firstAllocation, "RemainingQuantity", 1m);
        SetProperty(firstAllocation, "Status", "Pending");
        SetProperty(firstAllocation, "PlannedOffHireDate", new DateTime(2024, 5, 1, 8, 30, 0, DateTimeKind.Utc));
        SetProperty(firstAllocation, "History", CreateList(historyType, firstAllocationHistory));

        var secondAllocation = Activator.CreateInstance(allocationType)!;
        SetProperty(secondAllocation, "RentalDeviceLineNumber", "RDL-2");
        SetProperty(secondAllocation, "RequestedQuantity", 3m);
        SetProperty(secondAllocation, "RemainingQuantity", 0m);
        SetProperty(secondAllocation, "Status", "OffHired");
        SetProperty(secondAllocation, "PlannedOffHireDate", new DateTime(2024, 5, 2, 9, 45, 0, DateTimeKind.Utc));
        SetProperty(secondAllocation, "History", CreateList(historyType, secondAllocationHistory));

        var line = Activator.CreateInstance(lineType)!;
        SetProperty(line, "ExternalLineNumberRef", "EXL-1");
        SetProperty(line, "CollectionInstructions", "Leave at dock");
        SetProperty(line, "RequestedQuantity", 5m);
        SetProperty(line, "OffHireDateTime", new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        SetProperty(line, "Allocations", CreateList(allocationType, firstAllocation, secondAllocation));

        SetProperty(document, "Lines", CreateList(lineType, line));
        SetProperty(document, "History", CreateList(orderHistoryType));

        return document;
    }

    private static object CreateList(Type elementType, params object[] elements)
    {
        var list = Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
        var addMethod = list.GetType().GetMethod("Add")!;

        foreach (var element in elements)
        {
            addMethod.Invoke(list, new[] { element });
        }

        return list;
    }

    private static void SetProperty(object target, string propertyName, object? value)
    {
        var property = target.GetType().GetProperty(propertyName)!;
        property.SetValue(target, value);
    }
}
