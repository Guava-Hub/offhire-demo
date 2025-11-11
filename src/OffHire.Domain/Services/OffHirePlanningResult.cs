using System.Collections.Generic;
using System.Linq;
using OffHire.Domain.Models;

namespace OffHire.Domain.Services;

public sealed class OffHirePlanningResult
{
    private readonly List<IAggregateMutation> _mutations = new();
    private readonly List<OffHireHistoryEntry> _historyEntries = new();
    private readonly List<IntegrationCommand> _integrationCommands = new();

    public IReadOnlyCollection<IAggregateMutation> AggregateMutations => _mutations;

    public IReadOnlyCollection<OffHireHistoryEntry> HistoryEntries => _historyEntries;

    public IReadOnlyCollection<IntegrationCommand> IntegrationCommands => _integrationCommands;

    public void AddMutation(IAggregateMutation mutation) => _mutations.Add(mutation);

    public void AddHistory(OffHireHistoryEntry entry) => _historyEntries.Add(entry);

    public void AddIntegrationCommand(IntegrationCommand command) => _integrationCommands.Add(command);

    public static OffHirePlanningResult Empty() => new();
}

public interface IAggregateMutation
{
    void Apply(OffHireOrder aggregate);
}

public sealed record IntegrationCommand(string Type, object Payload, string CorrelationId)
{
    public static IntegrationCommand OffHireDynamics(DynamicsOffHirePayload payload, string correlationId)
        => new("DynamicsOffHire", payload, correlationId);

    public static IntegrationCommand BackDateDynamics(BackDatePayload payload, string correlationId)
        => new("DynamicsBackDate", payload, correlationId);
}

public sealed record DynamicsOffHirePayload(string ContractNumber, string RentalNumber, string ExternalLineNumberRef, string RentalDeviceLineNumber, decimal Quantity, string CollectionInstructions, string CompanyCode, System.DateTime OffHireDateTime);

public sealed record BackDatePayload(string ContractNumber, string RentalNumber, string ExternalLineNumberRef, string RentalDeviceLineNumber, decimal Quantity, System.DateTime EffectiveDate, string CompanyCode);
