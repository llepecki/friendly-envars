using System;
using System.Globalization;

namespace FriendlyEnvars;

/// <summary>
/// Defines a contract for converting environment variable string values to target types.
/// </summary>
/// <remarks>
/// This interface allows custom type conversion logic beyond the built-in support 
/// provided by <see cref="DefaultEnvarPropertyBinder"/>.
/// </remarks>
/// <example>
/// <code>
/// using System;
/// using System.Globalization;
/// 
/// public class CustomEnvarPropertyBinder : IEnvarPropertyBinder
/// {
///     private readonly DefaultEnvarPropertyBinder _defaultBinder = new();
///
///     public object? Convert(string value, Type targetType, CultureInfo culture)
///     {
///         // Handle custom connection string parsing
///         if (targetType == typeof(ConnectionString))
///         {
///             return ParseConnectionString(value);
///         }
///
///         // Handle TimeSpan from seconds
///         if (targetType == typeof(TimeSpan) && int.TryParse(value, out int seconds))
///         {
///             return TimeSpan.FromSeconds(seconds);
///         }
///
///         // Fall back to default conversion
///         return _defaultBinder.Convert(value, targetType, culture);
///     }
///
///     private static ConnectionString ParseConnectionString(string value)
///     {
///         // Custom parsing logic here
///         return new ConnectionString { /* ... */ };
///     }
/// }
/// </code>
/// </example>
public interface IEnvarPropertyBinder
{
    /// <summary>
    /// Converts a string value from an environment variable to the specified target type.
    /// </summary>
    /// <param name="value">The environment variable value to convert.</param>
    /// <param name="targetType">The target type to convert to.</param>
    /// <param name="culture">The culture to use for conversion (e.g., for numeric and date parsing).</param>
    /// <returns>The converted value, or null if the conversion results in a null value.</returns>
    /// <exception cref="ArgumentException">Thrown when the value can't be converted to the target type.</exception>
    /// <exception cref="FormatException">Thrown when the value format is invalid for the target type.</exception>
    /// <exception cref="OverflowException">Thrown when the value is outside the range of the target type.</exception>
    /// <remarks>
    /// <para>
    /// This method is called for each property marked with <see cref="EnvarAttribute"/> 
    /// when the corresponding environment variable has a non-null value (empty values
    /// may be passed).
    /// </para>
    /// <para>
    /// The implementation should handle nullable types by checking if the target type 
    /// is nullable using <see cref="Nullable.GetUnderlyingType"/>.
    /// </para>
    /// </remarks>
    object? Convert(string value, Type targetType, CultureInfo culture);
}
