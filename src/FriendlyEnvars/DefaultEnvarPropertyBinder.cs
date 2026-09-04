using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace FriendlyEnvars;

/// <summary>
/// Converts supported .NET types and falls back to <see cref="TypeConverter"/>.
/// </summary>
/// <remarks>
/// This type is stateless and thread-safe. A fallback converter receives the full environment value
/// and must follow the security rules in <see cref="IEnvarPropertyBinder"/>.
/// </remarks>
public sealed class DefaultEnvarPropertyBinder : IEnvarPropertyBinder
{
    /// <summary>
    /// Creates a default binder.
    /// </summary>
    public DefaultEnvarPropertyBinder()
    {
    }

    /// <summary>
    /// Converts a raw environment value to <paramref name="targetType"/>.
    /// </summary>
    /// <param name="value">The captured value, which may be empty.</param>
    /// <param name="targetType">The property's declared type.</param>
    /// <param name="culture">The parsing culture.</param>
    /// <returns>A value assignable to <paramref name="targetType"/>.</returns>
    /// <exception cref="FormatException">The value has an invalid format.</exception>
    /// <exception cref="OverflowException">The value exceeds the target type's range.</exception>
    /// <exception cref="NotSupportedException">No converter is available.</exception>
    /// <remarks>
    /// A fallback <see cref="TypeConverter"/> receives the full value. It must be deterministic,
    /// thread-safe, and must not log or retain the value.
    /// </remarks>
    [StackTraceHidden]
    public object? Convert(string value, Type targetType, CultureInfo culture)
    {
        return ConvertPrecomputed(value, PrecomputedConversion.Create(targetType), culture);
    }

    [StackTraceHidden]
    internal static object? ConvertPrecomputed(string value, PrecomputedConversion conversion, CultureInfo culture)
    {
        var targetType = conversion.ConversionType;

        if (conversion.EnumMetadata is { } enumMetadata)
        {
            return EnumText.Parse(value, targetType, enumMetadata);
        }

        return targetType switch
        {
            _ when targetType == typeof(string) => value,
            _ when targetType == typeof(char) => char.Parse(value),
            _ when targetType == typeof(bool) => bool.Parse(value),
            _ when targetType == typeof(byte) => byte.Parse(value, NumberStyles.Integer, culture),
            _ when targetType == typeof(sbyte) => sbyte.Parse(value, NumberStyles.Integer, culture),
            _ when targetType == typeof(short) => short.Parse(value, NumberStyles.Integer, culture),
            _ when targetType == typeof(ushort) => ushort.Parse(value, NumberStyles.Integer, culture),
            _ when targetType == typeof(int) => int.Parse(value, NumberStyles.Integer, culture),
            _ when targetType == typeof(uint) => uint.Parse(value, NumberStyles.Integer, culture),
            _ when targetType == typeof(long) => long.Parse(value, NumberStyles.Integer, culture),
            _ when targetType == typeof(ulong) => ulong.Parse(value, NumberStyles.Integer, culture),
            _ when targetType == typeof(float) => float.Parse(value, NumberStyles.Float, culture),
            _ when targetType == typeof(double) => double.Parse(value, NumberStyles.Float, culture),
            _ when targetType == typeof(decimal) => decimal.Parse(value, NumberStyles.Float, culture),
            _ when targetType == typeof(Guid) => Guid.Parse(value),
            _ when targetType == typeof(Uri) => new Uri(value),
            _ when targetType == typeof(TimeSpan) => TimeSpan.Parse(value, culture),
            _ when targetType == typeof(DateTime) => DateTime.Parse(value, culture),
            _ when targetType == typeof(DateTimeOffset) => DateTimeOffset.Parse(value, culture),
            _ when targetType == typeof(DateOnly) => DateOnly.Parse(value, culture),
            _ when targetType == typeof(TimeOnly) => TimeOnly.Parse(value, culture),
            _ => ConvertUsingTypeConverter(value, targetType, culture)
        };
    }

    [StackTraceHidden]
    private static object? ConvertUsingTypeConverter(string value, Type targetType, CultureInfo culture)
    {
        var converter = TypeDescriptor.GetConverter(targetType);
        return converter.ConvertFrom(null, culture, value);
    }

    internal sealed class PrecomputedConversion
    {
        private PrecomputedConversion(Type conversionType, EnumText.Metadata? enumMetadata)
        {
            ConversionType = conversionType;
            EnumMetadata = enumMetadata;
        }

        internal Type ConversionType { get; }

        internal EnumText.Metadata? EnumMetadata { get; }

        internal static PrecomputedConversion Create(Type declaredType)
        {
            return Create(declaredType, enumMetadataCache: null);
        }

        /// <summary>
        /// Plan-building overload with an optional per-build cache, so many properties of the same
        /// enum share one member table. The cache is scoped to one plan build; a process-wide cache
        /// would root collectible assemblies and break unloadability.
        /// </summary>
        internal static PrecomputedConversion Create(Type declaredType, Dictionary<Type, EnumText.Metadata>? enumMetadataCache)
        {
            var conversionType = Nullable.GetUnderlyingType(declaredType) ?? declaredType;

            if (!conversionType.IsEnum)
            {
                return new PrecomputedConversion(conversionType, null);
            }

            EnumText.Metadata? metadata = null;

            if (enumMetadataCache is null || !enumMetadataCache.TryGetValue(conversionType, out metadata))
            {
                metadata = EnumText.Metadata.Create(conversionType);
                enumMetadataCache?[conversionType] = metadata;
            }

            return new PrecomputedConversion(conversionType, metadata);
        }
    }

    internal static class EnumText
    {
        public static object Parse(string value, Type enumType, Metadata metadata)
        {
            string trimmed = value.Trim();

            if (trimmed.Length == 0)
            {
                throw Invalid(enumType, "the value is empty or whitespace only");
            }

            return metadata.IsFlags
                ? ParseFlags(trimmed, enumType, metadata)
                : ParseNonFlags(trimmed, enumType, metadata);
        }

        internal sealed class Metadata
        {
            private Metadata(TypeCode typeCode, string[] names, ulong[] patterns, bool isFlags, ulong allowedMask)
            {
                TypeCode = typeCode;
                Names = names;
                Patterns = patterns;
                IsFlags = isFlags;
                AllowedMask = allowedMask;
            }

            internal TypeCode TypeCode { get; }

            internal string[] Names { get; }

            internal ulong[] Patterns { get; }

            internal bool IsFlags { get; }

            internal ulong AllowedMask { get; }

            internal static Metadata Create(Type enumType)
            {
                var typeCode = Type.GetTypeCode(Enum.GetUnderlyingType(enumType));
                string[] names = Enum.GetNames(enumType);
                ulong[] patterns = GetBitPatterns(enumType, typeCode);
                ulong allowedMask = 0;

                foreach (ulong pattern in patterns)
                {
                    allowedMask |= pattern;
                }

                return new Metadata(
                    typeCode,
                    names,
                    patterns,
                    enumType.IsDefined(typeof(FlagsAttribute), inherit: false),
                    allowedMask);
            }

            private static ulong[] GetBitPatterns(Type enumType, TypeCode typeCode)
            {
                var values = Enum.GetValuesAsUnderlyingType(enumType);
                var patterns = new ulong[values.Length];

                for (int i = 0; i < values.Length; i++)
                {
                    object? underlyingValue = values.GetValue(i);

                    if (underlyingValue is null)
                    {
                        throw new NotSupportedException($"Enum '{enumType.FullName}' exposed a null underlying value.");
                    }

                    patterns[i] = ToBitPattern(underlyingValue, typeCode);
                }

                return patterns;
            }

            private static ulong ToBitPattern(object underlyingValue, TypeCode typeCode)
            {
                return typeCode switch
                {
                    TypeCode.SByte => unchecked((ulong)(sbyte)underlyingValue) & byte.MaxValue,
                    TypeCode.Byte => (byte)underlyingValue,
                    TypeCode.Int16 => unchecked((ulong)(short)underlyingValue) & ushort.MaxValue,
                    TypeCode.UInt16 => (ushort)underlyingValue,
                    TypeCode.Int32 => unchecked((ulong)(int)underlyingValue) & uint.MaxValue,
                    TypeCode.UInt32 => (uint)underlyingValue,
                    TypeCode.Int64 => unchecked((ulong)(long)underlyingValue),
                    TypeCode.UInt64 => (ulong)underlyingValue,
                    _ => throw new NotSupportedException($"Enum underlying type code '{typeCode}' is not supported.")
                };
            }
        }

        private static object ParseFlags(string trimmed, Type enumType, Metadata metadata)
        {
            var typeCode = metadata.TypeCode;
            string[] names = metadata.Names;
            ulong[] patterns = metadata.Patterns;
            ulong allowedMask = metadata.AllowedMask;
            ulong result;

            if (trimmed.Contains(',', StringComparison.Ordinal))
            {
                result = 0;

                foreach (string token in trimmed.Split(','))
                {
                    string trimmedToken = token.Trim();

                    if (trimmedToken.Length == 0)
                    {
                        throw Invalid(enumType, "a list element is empty");
                    }

                    if (!TryMatchName(trimmedToken, names, patterns, out ulong tokenPattern))
                    {
                        throw Invalid(enumType, "a list element is not an unambiguous declared member name");
                    }

                    result |= tokenPattern;
                }
            }
            else if (TryMatchName(trimmed, names, patterns, out ulong singlePattern))
            {
                result = singlePattern;
            }
            else
            {
                result = ParseUnsignedDecimal(trimmed, enumType, typeCode);
            }

            if ((result & ~allowedMask) != 0)
            {
                throw Invalid(enumType, "the result contains bits that no declared member defines");
            }

            return ToEnum(result, enumType, typeCode);
        }

        private static object ParseNonFlags(string trimmed, Type enumType, Metadata metadata)
        {
            var typeCode = metadata.TypeCode;
            string[] names = metadata.Names;
            ulong[] patterns = metadata.Patterns;

            if (trimmed.Contains(',', StringComparison.Ordinal))
            {
                throw Invalid(enumType, "the type is not a flags enum, so a list is not accepted");
            }

            if (TryMatchName(trimmed, names, patterns, out ulong namePattern))
            {
                return ToEnum(namePattern, enumType, typeCode);
            }

            ulong numeric = ParseUnsignedDecimal(trimmed, enumType, typeCode);
            object underlyingValue = ToUnderlying(numeric, typeCode);

            if (!Enum.IsDefined(enumType, underlyingValue))
            {
                throw Invalid(enumType, "the numeric value is not a declared member");
            }

            return Enum.ToObject(enumType, underlyingValue);
        }

        private static ulong ParseUnsignedDecimal(string trimmed, Type enumType, TypeCode typeCode)
        {
            if (!IsAsciiDecimal(trimmed))
            {
                throw Invalid(enumType, "the value is neither a declared member name nor an unsigned decimal number");
            }

            ulong widthMask = GetWidthMask(typeCode);
            ulong result = 0;

            foreach (char character in trimmed)
            {
                ulong digit = (ulong)(character - '0');

                // Check before multiplication to prevent wraparound.
                if (result > (ulong.MaxValue - digit) / 10)
                {
                    throw Invalid(enumType, "the numeric value overflows the underlying type");
                }

                result = (result * 10) + digit;

                if (result > widthMask)
                {
                    throw Invalid(enumType, "the numeric value overflows the underlying type");
                }
            }

            return result;
        }

        private static bool IsAsciiDecimal(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (!char.IsAsciiDigit(text[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryMatchName(string token, string[] names, ulong[] patterns, out ulong pattern)
        {
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], token, StringComparison.Ordinal))
                {
                    pattern = patterns[i];
                    return true;
                }
            }

            bool found = false;
            ulong candidate = 0;

            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], token, StringComparison.OrdinalIgnoreCase))
                {
                    if (!found)
                    {
                        candidate = patterns[i];
                        found = true;
                    }
                    else if (patterns[i] != candidate)
                    {
                        // Never choose arbitrarily between case-insensitive aliases.
                        pattern = 0;
                        return false;
                    }
                }
            }

            pattern = candidate;
            return found;
        }

        private static object ToUnderlying(ulong pattern, TypeCode typeCode)
        {
            return typeCode switch
            {
                TypeCode.SByte => unchecked((sbyte)(byte)pattern),
                TypeCode.Byte => (byte)pattern,
                TypeCode.Int16 => unchecked((short)(ushort)pattern),
                TypeCode.UInt16 => (ushort)pattern,
                TypeCode.Int32 => unchecked((int)(uint)pattern),
                TypeCode.UInt32 => (uint)pattern,
                TypeCode.Int64 => unchecked((long)pattern),
                TypeCode.UInt64 => pattern,
                _ => throw new NotSupportedException($"Enum underlying type code '{typeCode}' is not supported.")
            };
        }

        private static object ToEnum(ulong pattern, Type enumType, TypeCode typeCode)
        {
            return Enum.ToObject(enumType, ToUnderlying(pattern, typeCode));
        }

        private static ulong GetWidthMask(TypeCode typeCode)
        {
            return typeCode switch
            {
                TypeCode.SByte or TypeCode.Byte => byte.MaxValue,
                TypeCode.Int16 or TypeCode.UInt16 => ushort.MaxValue,
                TypeCode.Int32 or TypeCode.UInt32 => uint.MaxValue,
                TypeCode.Int64 or TypeCode.UInt64 => ulong.MaxValue,
                _ => throw new NotSupportedException($"Enum underlying type code '{typeCode}' is not supported.")
            };
        }

        private static FormatException Invalid(Type enumType, string reason)
        {
            return new FormatException(string.Create(
                CultureInfo.InvariantCulture,
                $"The value is not valid for enum '{enumType.FullName}': {reason}."));
        }
    }
}
