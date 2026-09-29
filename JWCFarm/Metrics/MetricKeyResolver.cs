using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace JWCFarm.Metrics;

/// <summary>
/// Coordinates metric value types for join operations:
/// - Validates supported key scalar/value types.
/// - Determines symmetric canonical key type across N participating child key types using existing <see cref="MetricBinder"/> rules.
/// - Canonicalizes runtime values to the resolved canonical type with strict floating-point validation.
/// </summary>
public static class MetricKeyResolver
{
    private static readonly HashSet<Type> SupportedDirectTypes = new()
    {
        typeof(sbyte),
        typeof(byte),
        typeof(short),
        typeof(ushort),
        typeof(int),
        typeof(uint),
        typeof(long),
        typeof(ulong),
        typeof(float),
        typeof(double),
        typeof(decimal),
        typeof(string),
        typeof(char),
        typeof(bool),
        typeof(Guid),
        typeof(DateTime),
        typeof(DateTimeOffset),
        typeof(TimeSpan)
    };

    /// <summary>
    /// Checks whether the given type is a supported join key scalar/value type.
    /// Intermediate metric-bearing objects, collections, and arbitrary classes are rejected.
    /// </summary>
    public static bool IsSupportedKeyType(Type? type)
    {
        if (type == null) return false;
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying.IsEnum) return true;
        return SupportedDirectTypes.Contains(underlying);
    }

    private static bool IsNumericType(Type type)
    {
        if (type.IsEnum) return false;
        var code = Type.GetTypeCode(type);
        return code is TypeCode.SByte
            or TypeCode.Byte
            or TypeCode.Int16
            or TypeCode.UInt16
            or TypeCode.Int32
            or TypeCode.UInt32
            or TypeCode.Int64
            or TypeCode.UInt64
            or TypeCode.Single
            or TypeCode.Double
            or TypeCode.Decimal;
    }

    /// <summary>
    /// Resolves a symmetric canonical comparison type across N participating child key types.
    /// </summary>
    public static bool TryResolveCanonicalKeyType(
        IReadOnlyList<Type> types,
        [NotNullWhen(true)] out Type? canonicalType,
        [NotNullWhen(false)] out string? errorMessage)
    {
        canonicalType = null;
        errorMessage = null;

        if (types == null || types.Count == 0)
        {
            errorMessage = "No key types provided for join resolution.";
            return false;
        }

        var unwrapped = new Type[types.Count];
        for (int i = 0; i < types.Count; i++)
        {
            var t = types[i];
            if (t == null)
            {
                errorMessage = $"Key type at index {i} cannot be null.";
                return false;
            }

            var u = Nullable.GetUnderlyingType(t) ?? t;
            if (!IsSupportedKeyType(u))
            {
                errorMessage = $"Type '{u.Name}' is not a supported join key type. Join keys must be supported scalar/value types.";
                return false;
            }

            unwrapped[i] = u;
        }

        // If all types are identical
        bool allSame = true;
        for (int i = 1; i < unwrapped.Length; i++)
        {
            if (unwrapped[i] != unwrapped[0])
            {
                allSame = false;
                break;
            }
        }

        if (allSame)
        {
            canonicalType = unwrapped[0];
            return true;
        }

        // Mixed types: check if all participating types are numeric
        bool allNumeric = unwrapped.All(IsNumericType);
        if (!allNumeric)
        {
            var distinctNames = string.Join(", ", unwrapped.Select(t => t.Name).Distinct());
            errorMessage = $"Cannot join incompatible non-numeric or mixed types: [{distinctNames}].";
            return false;
        }

        // If any type is float or double AND any type is decimal -> incompatible
        bool hasFloating = unwrapped.Any(t => t == typeof(float) || t == typeof(double));
        bool hasDecimal = unwrapped.Any(t => t == typeof(decimal));
        if (hasFloating && hasDecimal)
        {
            errorMessage = "Cannot join incompatible numeric types: floating-point (Single/Double) and Decimal cannot be widened losslessly.";
            return false;
        }

        // Candidate evaluation in widening preference order
        Type[] candidates;
        if (!hasFloating && !hasDecimal)
        {
            // All integer / char types
            candidates = new[]
            {
                typeof(int),
                typeof(uint),
                typeof(long),
                typeof(ulong),
                typeof(decimal)
            };
        }
        else if (hasFloating)
        {
            candidates = new[]
            {
                typeof(float),
                typeof(double)
            };
        }
        else
        {
            // hasDecimal (and no floating)
            candidates = new[]
            {
                typeof(decimal)
            };
        }

        foreach (var candidate in candidates)
        {
            bool candidateValid = true;
            foreach (var t in unwrapped)
            {
                if (t != candidate && !MetricBinder.IsNumericWidening(candidate, t))
                {
                    candidateValid = false;
                    break;
                }
            }

            if (candidateValid)
            {
                canonicalType = candidate;
                return true;
            }
        }

        var typeNames = string.Join(", ", unwrapped.Select(t => t.Name).Distinct());
        errorMessage = $"Cannot resolve a common canonical key type for numeric types: [{typeNames}].";
        return false;
    }

    /// <summary>
    /// Canonicalizes a runtime key value into the target canonical type.
    /// Rejects non-finite floating point numbers (NaN, +Infinity, -Infinity).
    /// </summary>
    public static object? CanonicalizeKey(object? key, Type targetCanonicalType)
    {
        if (key == null) return null;

        var keyType = key.GetType();
        var nonNullTarget = Nullable.GetUnderlyingType(targetCanonicalType) ?? targetCanonicalType;

        // Check for non-finite floating point values before conversion
        if (key is double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d))
            {
                throw new FarmInputException($"Floating-point join key cannot be NaN or Infinity (received {d}).");
            }
        }
        else if (key is float f)
        {
            if (float.IsNaN(f) || float.IsInfinity(f))
            {
                throw new FarmInputException($"Floating-point join key cannot be NaN or Infinity (received {f}).");
            }
        }

        if (keyType == nonNullTarget)
        {
            return key;
        }

        if (IsNumericType(nonNullTarget) && IsNumericType(keyType))
        {
            object coerced;
            if (MetricBinder.IsNumericWidening(nonNullTarget, keyType))
            {
                coerced = MetricBinder.CoerceNumericWidening(key, nonNullTarget)!;
            }
            else
            {
                coerced = Convert.ChangeType(key, nonNullTarget, CultureInfo.InvariantCulture);
            }

            if (coerced is double cd && (double.IsNaN(cd) || double.IsInfinity(cd)))
            {
                throw new FarmInputException($"Floating-point join key cannot be NaN or Infinity (received {cd}).");
            }
            if (coerced is float cf && (float.IsNaN(cf) || float.IsInfinity(cf)))
            {
                throw new FarmInputException($"Floating-point join key cannot be NaN or Infinity (received {cf}).");
            }

            return coerced;
        }

        return key;
    }
}
