using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace FriendlyEnvars;

/// <summary>
/// The binder used unless a custom one is supplied. Converts the common BCL types directly, and falls
/// back to the target type's <see cref="TypeConverter"/> for anything else.
/// </summary>
/// <remarks>
/// This type is stateless and therefore thread-safe. Note that its <see cref="TypeConverter"/> fallback
/// passes the complete environment value to code the library does not control; see
/// <see cref="IEnvarPropertyBinder"/> for what that implies.
/// </remarks>
public sealed class DefaultEnvarPropertyBinder : IEnvarPropertyBinder
{
    [StackTraceHidden]
    public object? Convert(string value, Type targetType, CultureInfo culture)
    {
        var underlyingType = Nullable.GetUnderlyingType(targetType);

        if (underlyingType != null)
        {
            targetType = underlyingType;
        }

        if (targetType.IsEnum)
        {
            return EnumText.Parse(value, targetType);
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

    /// <summary>
    /// Last resort for a type this binder has no built-in rule for: whatever
    /// <see cref="TypeDescriptor.GetConverter(Type)"/> returns for it.
    /// </summary>
    /// <remarks>
    /// <b>This hands the complete environment value to third-party code.</b> The converter is chosen by
    /// the target type - it may come from that type's own <see cref="TypeConverterAttribute"/>, from a
    /// base type, or from a converter registered elsewhere in the process - and it is as trusted as any
    /// custom binder: it receives the value verbatim, secrets included, and the library does not
    /// sandbox, redact or inspect it. A converter reached this way must be deterministic and
    /// thread-safe, and must not log or retain what it is given. Supply an
    /// <see cref="IEnvarPropertyBinder"/> instead if you need control over which code sees the value.
    /// </remarks>
    [StackTraceHidden]
    private static object? ConvertUsingTypeConverter(string value, Type targetType, CultureInfo culture)
    {
        var converter = TypeDescriptor.GetConverter(targetType);
        return converter.ConvertFrom(null, culture, value);
    }

    /// <summary>
    /// Parses enum text under an explicit grammar instead of delegating syntax decisions to
    /// <see cref="Enum.Parse(Type, string, bool)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Enum.Parse(Type, string, bool)"/> accepts comma-separated lists for every enum, accepts
    /// signed and whitespace-padded numbers, and happily produces values whose bit pattern contains bits no
    /// declared member defines. Configuration is not a place for that latitude: a typo should fail loudly at
    /// startup rather than silently become an undeclared value.
    /// </para>
    /// <para>
    /// Names are matched deterministically. An ordinal exact-case match wins outright. Otherwise every
    /// ordinal case-insensitive match is collected, and the input is accepted only when all of those
    /// candidates carry the same bit pattern, so a type declaring both <c>Read</c> and <c>READ</c> with
    /// different values rejects the ambiguous <c>read</c> rather than picking one arbitrarily.
    /// </para>
    /// </remarks>
    private static class EnumText
    {
        /// <summary>
        /// Parses <paramref name="value"/> into <paramref name="enumType"/>.
        /// </summary>
        /// <exception cref="FormatException">
        /// The text does not satisfy the grammar. The message never contains the text, because an
        /// environment value may be a secret.
        /// </exception>
        public static object Parse(string value, Type enumType)
        {
            string trimmed = value.Trim();

            if (trimmed.Length == 0)
            {
                throw Invalid(enumType, "the value is empty or whitespace only");
            }

            var underlyingType = Enum.GetUnderlyingType(enumType);
            var typeCode = Type.GetTypeCode(underlyingType);
            string[] names = Enum.GetNames(enumType);
            ulong[] patterns = GetBitPatterns(enumType, typeCode);

            return enumType.IsDefined(typeof(FlagsAttribute), inherit: false)
                ? ParseFlags(trimmed, enumType, typeCode, names, patterns)
                : ParseNonFlags(trimmed, enumType, typeCode, names, patterns);
        }

        private static object ParseFlags(string trimmed, Type enumType, TypeCode typeCode, string[] names, ulong[] patterns)
        {
            ulong allowedMask = 0;

            foreach (ulong pattern in patterns)
            {
                allowedMask |= pattern;
            }

            ulong result;

            if (trimmed.Contains(',', StringComparison.Ordinal))
            {
                // A list may contain declared names only. Numeric tokens are forbidden, which falls out of
                // requiring every token to match a name.
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
                // A single declared name is always acceptable, including one with a negative value.
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

        private static object ParseNonFlags(string trimmed, Type enumType, TypeCode typeCode, string[] names, ulong[] patterns)
        {
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

        /// <summary>
        /// Accepts only ASCII decimal digits, and only a value that fits the underlying type's width.
        /// A sign, a hexadecimal prefix, a digit separator or a non-ASCII digit is rejected outright.
        /// </summary>
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

                // Detect overflow before it happens, so a long run of digits cannot wrap into a valid value.
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

        /// <summary>
        /// True when every character is an ASCII decimal digit. Deliberately rejects a leading sign, a
        /// hexadecimal prefix, a digit separator and non-ASCII digits such as Arabic-Indic numerals.
        /// </summary>
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
                        // Two declared members differ only by case and carry different values, so the input
                        // is ambiguous and is rejected rather than resolved arbitrarily.
                        pattern = 0;
                        return false;
                    }
                }
            }

            pattern = candidate;
            return found;
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

        /// <summary>
        /// Builds the rejection. The reason describes the grammar rule that was broken and never quotes the
        /// input, so the message stays safe to log even when the value is a secret.
        /// </summary>
        private static FormatException Invalid(Type enumType, string reason)
        {
            return new FormatException(string.Create(
                CultureInfo.InvariantCulture,
                $"The value is not valid for enum '{enumType.FullName}': {reason}."));
        }
    }
}
