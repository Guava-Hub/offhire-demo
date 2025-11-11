using System;
using System.Linq;
using OffHire.Domain.Models;
using OffHire.Domain.ValueObjects;

namespace OffHire.Domain.Services;

public sealed class OffHirePlanner : IOffHirePlanner
{
    public OffHirePlanningResult Plan(OffHirePlanningContext context)
    {
        var result = new OffHirePlanningResult();

        foreach (var reconLine in context.Notification.Lines)
        {
            var allocations = context.FindAllocations(reconLine.RentalDeviceLineNumber).ToArray();

            if (!allocations.Any())
            {
                // Scenario 1 - No update request. Create new line with full quantity.
                var line = context.EnsureLine(reconLine.ExternalLineReference);
                line.MergeRequest(new[] { reconLine.RentalDeviceLineNumber }, reconLine.Quantity, reconLine.CollectionDateTime);
                allocations = context.FindAllocations(reconLine.RentalDeviceLineNumber).ToArray();
                result.AddHistory(OffHireHistoryEntry.PlannedFromRecon(line.ExternalLineNumberRef, reconLine.Quantity, reconLine.CollectionDateTime, "Full quantity from Recon"));
            }

            foreach (var allocation in allocations)
            {
                var requestedQuantity = allocation.RequestedQuantity;
                var reconQuantity = reconLine.Quantity;

                if (requestedQuantity.Value == reconQuantity.Value)
                {
                    // Scenario 2 – full off-hire
                    var action = allocation.OffHire(reconQuantity, reconLine.CollectionDateTime, "Recon quantity matches requested");
                    result.AddIntegrationCommand(ToDynamicsOffHire(context, reconLine, allocation, action));
                    result.AddHistory(OffHireHistoryEntry.PlannedFromRecon(allocation.RentalDeviceLineNumber, reconQuantity, reconLine.CollectionDateTime, "Full off-hire"));
                }
                else if (requestedQuantity.Value < reconQuantity.Value)
                {
                    // Scenario 3 – Recon has more than requested (API partial)
                    var offHireQuantity = requestedQuantity;
                    if (!offHireQuantity.IsZero)
                    {
                        var action = allocation.OffHire(offHireQuantity, reconLine.CollectionDateTime, "Partial off-hire limited by API request");
                        result.AddIntegrationCommand(ToDynamicsOffHire(context, reconLine, allocation, action));
                        result.AddHistory(OffHireHistoryEntry.PlannedFromRecon(allocation.RentalDeviceLineNumber, offHireQuantity, reconLine.CollectionDateTime, "Partial off-hire"));
                    }

                    var residualQuantity = new Quantity(reconQuantity.Value - offHireQuantity.Value);
                    if (!residualQuantity.IsZero)
                    {
                        var action = allocation.PushToPast(residualQuantity, reconLine.CollectionDateTime, "Push residual quantity to prevent re-trigger");
                        result.AddIntegrationCommand(ToDynamicsBackDate(context, reconLine, allocation, action));
                    }
                }
                else
                {
                    // API requested more than Recon quantity – treat as partial and leave outstanding remainder
                    var action = allocation.OffHire(reconQuantity, reconLine.CollectionDateTime, "Recon quantity lower than requested - partial complete");
                    result.AddIntegrationCommand(ToDynamicsOffHire(context, reconLine, allocation, action));
                    result.AddHistory(OffHireHistoryEntry.PlannedFromRecon(allocation.RentalDeviceLineNumber, reconQuantity, reconLine.CollectionDateTime, "Partial off-hire Recon limited"));
                    if (!allocation.RemainingQuantity.IsZero)
                    {
                        result.AddHistory(OffHireHistoryEntry.PlannedFromRecon(allocation.RentalDeviceLineNumber, allocation.RemainingQuantity, reconLine.CollectionDateTime, "Outstanding quantity awaiting future Recon"));
                    }
                }
            }
        }

        return result;
    }

    private static IntegrationCommand ToDynamicsOffHire(OffHirePlanningContext context, ReconOffHireLine reconLine, LineAllocation allocation, AllocationAction action)
    {
        var owningLine = context.Aggregate.Lines.First(l => l.Allocations.Contains(allocation));
        var payload = new DynamicsOffHirePayload(
            context.Aggregate.ContractNumber,
            context.Aggregate.RentalNumber,
            owningLine.ExternalLineNumberRef,
            allocation.RentalDeviceLineNumber,
            action.Quantity.Value,
            owningLine.CollectionInstructions,
            context.Aggregate.CompanyCode,
            action.EffectiveDate);

        return IntegrationCommand.OffHireDynamics(payload, GenerateCorrelationId(context, allocation));
    }

    private static IntegrationCommand ToDynamicsBackDate(OffHirePlanningContext context, ReconOffHireLine reconLine, LineAllocation allocation, AllocationAction action)
    {
        var owningLine = context.Aggregate.Lines.First(l => l.Allocations.Contains(allocation));
        var payload = new BackDatePayload(
            context.Aggregate.ContractNumber,
            context.Aggregate.RentalNumber,
            owningLine.ExternalLineNumberRef,
            allocation.RentalDeviceLineNumber,
            action.Quantity.Value,
            action.EffectiveDate,
            context.Aggregate.CompanyCode);

        return IntegrationCommand.BackDateDynamics(payload, GenerateCorrelationId(context, allocation));
    }

    private static string GenerateCorrelationId(OffHirePlanningContext context, LineAllocation allocation)
        => $"{context.Aggregate.Id}:{allocation.RentalDeviceLineNumber}:{DateTime.UtcNow:O}";
}
