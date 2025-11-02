using System;
using System.Collections.Generic;
using System.Linq;
using OffHire.Domain.ValueObjects;

namespace OffHire.Domain.Models;

public sealed record ReconOffHireNotification(
    string RentalNumber,
    string AccountNumber,
    string CompanyCode,
    IReadOnlyCollection<ReconOffHireLine> Lines)
{
    public static ReconOffHireNotification FromPayload(string rentalNumber, string accountNumber, string companyCode, IEnumerable<ReconOffHireLine> lines)
        => new(rentalNumber, accountNumber, companyCode, lines.ToArray());
}

public sealed record ReconOffHireLine(
    string RentalDeviceLineNumber,
    Quantity Quantity,
    DateTime CollectionDateTime,
    string ExternalLineReference);
