using System;

namespace OffHire.Domain.ValueObjects;

public readonly record struct Quantity
{
    public static readonly Quantity Zero = new(0m);

    public decimal Value { get; }

    public Quantity(decimal value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Quantity cannot be negative.");
        }

        Value = value;
    }

    public static Quantity FromInt(int value) => new(value);

    public static Quantity FromString(string value)
    {
        if (!decimal.TryParse(value, out var parsed))
        {
            throw new FormatException($"Unable to parse quantity '{value}'.");
        }

        return new Quantity(parsed);
    }

    public bool IsZero => Value == 0m;

    public static Quantity operator +(Quantity left, Quantity right) => new(left.Value + right.Value);

    public static Quantity operator -(Quantity left, Quantity right)
    {
        if (left.Value < right.Value)
        {
            throw new InvalidOperationException("Resulting quantity would be negative.");
        }

        return new Quantity(left.Value - right.Value);
    }

    public override string ToString() => Value.ToString("0.######");
}
