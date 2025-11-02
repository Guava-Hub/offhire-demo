using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OffHire.Api.Contracts;

public sealed class UpdateOffHireRequest
{
    [Required]
    public DateTime DateTimeRequested { get; set; }

    [Required]
    public RequesterDto Requester { get; set; } = new();

    [Required]
    public List<OffHireOrderDto> OffHireOrders { get; set; } = new();
}

public sealed class RequesterDto
{
    public PersonDetailsDto PersonDetails { get; set; } = new();
    public ContactDetailsDto ContactDetails { get; set; } = new();
}

public sealed class PersonDetailsDto
{
    public string Title { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
}

public sealed class ContactDetailsDto
{
    public string TelephoneNumber { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string FaxNumber { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
}

public sealed class OffHireOrderDto
{
    [Required]
    public OffHireHeaderDto Header { get; set; } = new();

    [Required]
    public List<OffHireLineDto> Lines { get; set; } = new();
}

public sealed class OffHireHeaderDto
{
    public string CollectionNotes { get; set; } = string.Empty;
    public SpeedyReferencesDto SpeedyReferences { get; set; } = new();
    public CollectionDetailsDto CollectionDetails { get; set; } = new();
}

public sealed class SpeedyReferencesDto
{
    public string ContractNumber { get; set; } = string.Empty;
}

public sealed class CollectionDetailsDto
{
    public PersonDetailsDto PersonDetails { get; set; } = new();
    public CollectionAddressDto CollectionAddress { get; set; } = new();
    public ContactDetailsDto ContactDetails { get; set; } = new();
}

public sealed class CollectionAddressDto
{
    public string Name { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string AddressLine2 { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string County { get; set; } = string.Empty;
    public string PostCode { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public sealed class OffHireLineDto
{
    [Required]
    public string ExternalLineNumberRef { get; set; } = string.Empty;

    [Required]
    public List<string> LineNumberReference { get; set; } = new();

    [Range(0.01, double.MaxValue)]
    public decimal Quantity { get; set; }

    public DateTime OffHireDateTime { get; set; }

    public string CollectionInstructions { get; set; } = string.Empty;
}
