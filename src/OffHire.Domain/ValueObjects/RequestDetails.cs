using System;

namespace OffHire.Domain.ValueObjects;

public sealed record PersonDetails(string Title, string Name, DateTime? DateOfBirth);

public sealed record ContactDetails(
    string TelephoneNumber,
    string MobileNumber,
    string FaxNumber,
    string EmailAddress);

public sealed record Requester(PersonDetails PersonDetails, ContactDetails ContactDetails);

public sealed record CollectionAddress(
    string Name,
    string AddressLine1,
    string AddressLine2,
    string Street,
    string City,
    string County,
    string PostCode,
    string State,
    string Country);

public sealed record CollectionDetails(
    PersonDetails PersonDetails,
    CollectionAddress CollectionAddress,
    ContactDetails ContactDetails);

public sealed record SpeedyReferences(string ContractNumber);

public sealed record OffHireHeader(
    string CollectionNotes,
    SpeedyReferences SpeedyReferences,
    CollectionDetails CollectionDetails);
