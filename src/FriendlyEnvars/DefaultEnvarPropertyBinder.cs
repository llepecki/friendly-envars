using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace FriendlyEnvars;

public class DefaultEnvarPropertyBinder : IEnvarPropertyBinder
{
    [StackTraceHidden]
    public virtual object? Convert(string value, Type targetType, CultureInfo culture)
    {
        var underlyingType = Nullable.GetUnderlyingType(targetType);

        if (underlyingType != null)
        {
            targetType = underlyingType;
        }

        if (targetType.IsEnum)
        {
            var parsedEnum = Enum.Parse(targetType, value, true);

            if (!Enum.IsDefined(targetType, parsedEnum))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"Value '{value}' is not a valid member of the '{targetType.Name}' enum");
            }
        }

        return targetType switch
        {
            _ when targetType == typeof(string) => value,
            _ when targetType == typeof(char) => value.Single(),
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
            _ => ConvertUsingTypeConverter(value, targetType)
        };
    }

    [StackTraceHidden]
    private static object? ConvertUsingTypeConverter(string value, Type targetType)
    {
        var converter = TypeDescriptor.GetConverter(targetType);
        return converter.ConvertFromString(value);
    }
}
