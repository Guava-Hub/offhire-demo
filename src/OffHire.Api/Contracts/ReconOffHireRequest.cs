using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace OffHire.Api.Contracts;

public sealed class ReconOffHireRequest
{
    [Required]
    [JsonPropertyName("OffHireOrders")]
    public required ReconOffHireOrdersPayload OffHireOrders { get; init; }
}

public sealed class ReconOffHireOrdersPayload
{
    [Required]
    [JsonPropertyName("OffHireOrder")]
    public required ReconOffHireOrderPayload OffHireOrder { get; init; }
}

public sealed class ReconOffHireOrderPayload
{
    [Required]
    public required string RentalNumber { get; init; }

    [Required]
    public required string AccountNumber { get; init; }

    [Required]
    public required string CompanyCode { get; init; }

    [Required]
    [MinLength(1)]
    public required IReadOnlyCollection<ReconOffHireLinePayload> Line { get; init; }
}

public sealed class ReconOffHireLinePayload
{
    [Required]
    public required string RentalDeviceLineNumber { get; init; }

    [Required]
    public required string Quantity { get; init; }

    [Required]
    public required DateTime CollectionDateTime { get; init; }

    [Required]
    public required string ExternalLineReference { get; init; }
}
