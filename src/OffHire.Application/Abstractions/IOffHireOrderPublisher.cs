using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OffHire.Domain.Models;

namespace OffHire.Application.Abstractions;

public interface IOffHireOrderPublisher
{
    Task PublishAsync(OffHireOrderMessage message, CancellationToken cancellationToken);
}

public sealed record OffHireOrderMessage(
    string ContractNumber,
    string RentalNumber,
    string AccountNumber,
    string CompanyCode,
    Originator Originator,
    DateTime RequestedAt,
    IReadOnlyCollection<OffHireOrderLineMessage> Lines);

public sealed record OffHireOrderLineMessage(
    string ExternalLineNumberRef,
    IReadOnlyCollection<string> LineNumberReferences,
    decimal Quantity,
    DateTime OffHireDateTime,
    string CollectionInstructions);

public interface IDynamicsResultQueue
{
    Task PublishAsync(DynamicsResultMessage message, CancellationToken cancellationToken);
}

public sealed record DynamicsResultMessage(
    string ContractNumber,
    string CompanyCode,
    IReadOnlyCollection<DynamicsResultLineMessage> Lines);

public sealed record DynamicsResultLineMessage(
    string ExternalLineNumberRef,
    decimal Quantity,
    DateTime OccurredAt,
    string RequestId,
    bool IsBackDate);
