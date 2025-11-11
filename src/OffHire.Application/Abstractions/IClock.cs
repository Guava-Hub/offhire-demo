using System;

namespace OffHire.Application.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}
