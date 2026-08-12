using System.Globalization;

namespace TruthInTheFlip.Farm.Format;

public readonly record struct Count
{
    private static ReadOnlySpan<char> Places => "KMBT";

    public long Value { get; }

    public Count(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Count cannot be negative.");
        }

        Value = value;
    }

    public static Count Parse(string input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        ReadOnlySpan<char> text = input.Trim().AsSpan();
        long multiplier = 1;

        int place = Places.IndexOf(
            char.ToUpperInvariant(text[^1]));

        if (place >= 0)
        {
            for (int i = 0; i <= place; i++)
            {
                multiplier = checked(multiplier * 1_000);
            }

            text = text[..^1];

            if (text.IsEmpty)
            {
                throw new FormatException(
                    $"Count '{input}' does not contain a numeric value.");
            }
        }

        long baseValue = long.Parse(
            text,
            NumberStyles.None,
            CultureInfo.InvariantCulture);

        return new Count(checked(baseValue * multiplier));
    }

    public static bool TryParse(
        string? input,
        out Count count)
    {
        try
        {
            count = Parse(input!);
            return true;
        }
        catch (ArgumentException)
        {
            count = default;
            return false;
        }
        catch (FormatException)
        {
            count = default;
            return false;
        }
        catch (OverflowException)
        {
            count = default;
            return false;
        }
    }

    public static implicit operator long(Count count)
        => count.Value;

    public static explicit operator Count(long value)
        => new(value);

    public override string ToString()
    {
        ReadOnlySpan<(long Scale, char Suffix)> places =
        [
            (1_000_000_000_000L, 'T'),
            (1_000_000_000L, 'B'),
            (1_000_000L, 'M'),
            (1_000L, 'K')
        ];

        foreach (var (scale, suffix) in places)
        {
            if (Value >= scale && Value % scale == 0)
            {
                return $"{Value / scale}{suffix}";
            }
        }

        return Value.ToString(CultureInfo.InvariantCulture);
    }
}