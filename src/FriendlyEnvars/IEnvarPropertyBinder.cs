using System;
using System.Globalization;

namespace FriendlyEnvars;

/// <summary>
/// Converts environment-variable values to property values.
/// </summary>
public interface IEnvarPropertyBinder
{
    /// <summary>
    /// Converts a raw environment value to a property value.
    /// </summary>
    /// <param name="value">The captured value, which may be empty.</param>
    /// <param name="targetType">The property's declared type.</param>
    /// <param name="culture">The selected read-only culture.</param>
    /// <returns>A value assignable to <paramref name="targetType"/>.</returns>
    /// <remarks>
    /// One binder instance may be called concurrently. It must be deterministic and thread-safe.
    /// The value may be a secret; do not log, cache, or retain it. Failures are sanitized, except
    /// <see cref="OperationCanceledException"/>, which is propagated unchanged.
    /// </remarks>
    object? Convert(string value, Type targetType, CultureInfo culture);
}
