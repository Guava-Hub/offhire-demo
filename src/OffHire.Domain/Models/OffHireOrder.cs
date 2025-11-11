using System;
using System.Collections.Generic;
using System.Linq;
using OffHire.Domain.Services;
using OffHire.Domain.ValueObjects;

namespace OffHire.Domain.Models;

public sealed class OffHireOrder
{
    private readonly List<OffHireLine> _lines = new();
    private readonly List<OffHireHistoryEntry> _history = new();

    private OffHireOrder(
        string id,
        string contractNumber,
        string rentalNumber,
        string accountNumber,
        string companyCode,
        Originator originator,
        DateTime requestedAt)
    {
        Id = id;
        ContractNumber = contractNumber;
        RentalNumber = rentalNumber;
        AccountNumber = accountNumber;
        CompanyCode = companyCode;
        Originator = originator;
        DateTimeRequested = requestedAt;
        UpdatedAt = requestedAt;
    }

    public string Id { get; }

    public string ContractNumber { get; }

    public string RentalNumber { get; }

    public string AccountNumber { get; }

    public string CompanyCode { get; }

    public Originator Originator { get; }

    public DateTime DateTimeRequested { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyCollection<OffHireLine> Lines => _lines;

    public IReadOnlyCollection<OffHireHistoryEntry> History => _history;

    public Requester? Requester { get; private set; }

    public OffHireHeader? Header { get; private set; }

    public static OffHireOrder Create(
        string id,
        string contractNumber,
        string rentalNumber,
        string accountNumber,
        string companyCode,
        Originator originator,
        DateTime requestedAt)
        => new(id, contractNumber, rentalNumber, accountNumber, companyCode, originator, requestedAt);

    public OffHireLine UpsertLine(
        string externalLineNumberRef,
        IEnumerable<string> rentalDeviceLineNumbers,
        Quantity requestedQuantity,
        DateTime offHireDateTime,
        string collectionInstructions)
    {
        var line = _lines.SingleOrDefault(l => l.ExternalLineNumberRef == externalLineNumberRef);

        if (line is null)
        {
            line = OffHireLine.Create(externalLineNumberRef, collectionInstructions);
            _lines.Add(line);
        }

        line.MergeRequest(rentalDeviceLineNumbers, requestedQuantity, offHireDateTime);
        AddHistory(OffHireHistoryEntry.RequestedFromApi(externalLineNumberRef, requestedQuantity, offHireDateTime));
        Touch(offHireDateTime);
        return line;
    }

    public OffHirePlanningResult PlanOffHireFromRecon(ReconOffHireNotification notification, IOffHirePlanner planner)
    {
        var context = new OffHirePlanningContext(this, notification);
        var result = planner.Plan(context);

        foreach (var change in result.AggregateMutations)
        {
            change.Apply(this);
        }

        foreach (var entry in result.HistoryEntries)
        {
            AddHistory(entry);
        }

        if (result.AggregateMutations.Any())
        {
            Touch(DateTime.UtcNow);
        }

        return result;
    }

    internal OffHireLine EnsureLine(string externalLineNumberRef)
    {
        var line = _lines.SingleOrDefault(l => l.ExternalLineNumberRef == externalLineNumberRef);

        if (line is null)
        {
            line = OffHireLine.Create(externalLineNumberRef, collectionInstructions: string.Empty);
            _lines.Add(line);
        }

        return line;
    }

    internal void AddHistory(OffHireHistoryEntry entry) => _history.Add(entry);

    public void LoadHistory(IEnumerable<OffHireHistoryEntry> entries)
    {
        _history.Clear();
        _history.AddRange(entries);
    }

    internal void LoadLines(IEnumerable<OffHireLine> lines)
    {
        _lines.Clear();
        _lines.AddRange(lines);
    }

    internal void LoadRequestDetails(Requester? requester, OffHireHeader? header)
    {
        Requester = requester;
        Header = header;
    }

    public void UpdateRequestDetails(Requester requester, OffHireHeader header)
    {
        Requester = requester;
        Header = header;
        Touch(DateTime.UtcNow);
    }

    private void Touch(DateTime at)
    {
        UpdatedAt = at;
    }
}

public sealed record Originator(string Id, string ClientSystem, string UserName);
