using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OffHire.Application.Abstractions;
using OffHire.Domain.Models;
using OffHire.Domain.ValueObjects;

namespace OffHire.Application.Commands;

public sealed record UpsertOffHireOrderCommand(
    string ContractNumber,
    string RentalNumber,
    string AccountNumber,
    string CompanyCode,
    Originator Originator,
    DateTime RequestedAt,
    IReadOnlyCollection<LineRequest> Lines);

public sealed record LineRequest(
    string ExternalLineNumberRef,
    IReadOnlyCollection<string> LineNumberReferences,
    decimal Quantity,
    DateTime OffHireDateTime,
    string CollectionInstructions);

public sealed class UpsertOffHireOrderCommandHandler
{
    private readonly IOffHireOrderRepository _repository;

    public UpsertOffHireOrderCommandHandler(IOffHireOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<OffHireOrder> Handle(UpsertOffHireOrderCommand command, CancellationToken cancellationToken)
    {
        var aggregate = await _repository.FindAsync(command.ContractNumber, command.CompanyCode, cancellationToken);

        if (aggregate is null)
        {
            aggregate = OffHireOrder.Create(
                id: $"{command.ContractNumber}:{command.CompanyCode}",
                command.ContractNumber,
                command.RentalNumber,
                command.AccountNumber,
                command.CompanyCode,
                command.Originator,
                command.RequestedAt);
        }

        foreach (var line in command.Lines)
        {
            aggregate.UpsertLine(
                line.ExternalLineNumberRef,
                line.LineNumberReferences,
                new Quantity(line.Quantity),
                line.OffHireDateTime,
                line.CollectionInstructions);
        }

        await _repository.SaveAsync(aggregate, cancellationToken);
        return aggregate;
    }
}
