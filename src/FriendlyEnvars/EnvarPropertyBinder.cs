using System;
using System.ComponentModel;

namespace FriendlyEnvars;

public class EnvarPropertyBinder : IEnvarPropertyBinder
{
    public virtual object? Convert(string value, Type targetType)
    {
        if (targetType == typeof(string))
        {
            return value;
        }

        var underlyingType = Nullable.GetUnderlyingType(targetType);

        if (underlyingType != null)
        {
            targetType = underlyingType;
        }

        if (targetType.IsEnum)
        {
            return Enum.Parse(targetType, value, ignoreCase: true);
        }

        return Type.GetTypeCode(targetType) switch
        {
            TypeCode.Boolean => bool.Parse(value),
            TypeCode.Byte => byte.Parse(value),
            TypeCode.Char => value.Length == 1 ? value[0] : throw new EnvarsException($"Can't convert '{value}' to char - must be exactly one character"),
            TypeCode.Int16 => short.Parse(value),
            TypeCode.Int32 => int.Parse(value),
            TypeCode.Int64 => long.Parse(value),
            TypeCode.Double => double.Parse(value),
            TypeCode.Single => float.Parse(value),
            TypeCode.Decimal => decimal.Parse(value),
            _ => targetType switch
            {
                _ when targetType == typeof(Guid) => Guid.Parse(value),
                _ when targetType == typeof(Uri) => new Uri(value),
                _ when targetType == typeof(TimeSpan) => TimeSpan.Parse(value),
                _ when targetType == typeof(DateTime) => DateTime.Parse(value),
                _ when targetType == typeof(DateTimeOffset) => DateTimeOffset.Parse(value),
                _ when targetType == typeof(DateOnly) => DateOnly.Parse(value),
                _ when targetType == typeof(TimeOnly) => TimeOnly.Parse(value),
                _ => ConvertUsingTypeConverter(value, targetType)
            }
        };
    }

    private static object? ConvertUsingTypeConverter(string value, Type targetType)
    {
        var converter = TypeDescriptor.GetConverter(targetType);

        if (converter.CanConvertFrom(typeof(string)))
        {
            return converter.ConvertFromString(value);
        }

        throw new EnvarsException($"Can't convert string to type '{targetType.Name}'");
    }
}
