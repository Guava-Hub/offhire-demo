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
    RequesterRequest? Requester,
    OffHireHeaderRequest? Header,
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
        OffHireOrder? aggregate = null;

        var rentalDeviceLineNumber = command.Lines.FirstOrDefault()?.LineNumberReferences.FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(rentalDeviceLineNumber) && !string.IsNullOrWhiteSpace(command.AccountNumber))
        {
            aggregate = await _repository.FindByRentalDeviceAsync(
                rentalDeviceLineNumber,
                command.CompanyCode,
                command.AccountNumber,
                cancellationToken);
        }

        aggregate ??= await _repository.FindAsync(command.ContractNumber, command.CompanyCode, cancellationToken);

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

        if (command.Requester is not null && command.Header is not null)
        {
            aggregate.UpdateRequestDetails(MapRequester(command.Requester), MapHeader(command.Header));
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

    private static Requester MapRequester(RequesterRequest requester)
        => new(
            new PersonDetails(
                requester.PersonDetails.Title,
                requester.PersonDetails.Name,
                requester.PersonDetails.DateOfBirth),
            new ContactDetails(
                requester.ContactDetails.TelephoneNumber,
                requester.ContactDetails.MobileNumber,
                requester.ContactDetails.FaxNumber,
                requester.ContactDetails.EmailAddress));

    private static OffHireHeader MapHeader(OffHireHeaderRequest header)
        => new(
            header.CollectionNotes,
            new SpeedyReferences(header.SpeedyReferences.ContractNumber),
            new CollectionDetails(
                new PersonDetails(
                    header.CollectionDetails.PersonDetails.Title,
                    header.CollectionDetails.PersonDetails.Name,
                    header.CollectionDetails.PersonDetails.DateOfBirth),
                new CollectionAddress(
                    header.CollectionDetails.CollectionAddress.Name,
                    header.CollectionDetails.CollectionAddress.AddressLine1,
                    header.CollectionDetails.CollectionAddress.AddressLine2,
                    header.CollectionDetails.CollectionAddress.Street,
                    header.CollectionDetails.CollectionAddress.City,
                    header.CollectionDetails.CollectionAddress.County,
                    header.CollectionDetails.CollectionAddress.PostCode,
                    header.CollectionDetails.CollectionAddress.State,
                    header.CollectionDetails.CollectionAddress.Country),
                new ContactDetails(
                    header.CollectionDetails.ContactDetails.TelephoneNumber,
                    header.CollectionDetails.ContactDetails.MobileNumber,
                    header.CollectionDetails.ContactDetails.FaxNumber,
                    header.CollectionDetails.ContactDetails.EmailAddress)));
}

public sealed record RequesterRequest(PersonDetailsRequest PersonDetails, ContactDetailsRequest ContactDetails);

public sealed record PersonDetailsRequest(string Title, string Name, DateTime? DateOfBirth);

public sealed record ContactDetailsRequest(string TelephoneNumber, string MobileNumber, string FaxNumber, string EmailAddress);

public sealed record OffHireHeaderRequest(
    string CollectionNotes,
    SpeedyReferencesRequest SpeedyReferences,
    CollectionDetailsRequest CollectionDetails);

public sealed record SpeedyReferencesRequest(string ContractNumber);

public sealed record CollectionDetailsRequest(
    PersonDetailsRequest PersonDetails,
    CollectionAddressRequest CollectionAddress,
    ContactDetailsRequest ContactDetails);

public sealed record CollectionAddressRequest(
    string Name,
    string AddressLine1,
    string AddressLine2,
    string Street,
    string City,
    string County,
    string PostCode,
    string State,
    string Country);
