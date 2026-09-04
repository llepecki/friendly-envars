using System;
using System.Globalization;

namespace FriendlyEnvars;

/// <summary>
/// Configures value conversion for <c>BindEnvars</c>.
/// </summary>
/// <remarks>
/// Each method updates and returns the same instance. Changes made after <c>BindEnvars</c> returns have
/// no effect.
/// </remarks>
public sealed record EnvarSettings
{
    internal EnvarSettings(IEnvarPropertyBinder defaultBinder)
    {
        EnvarPropertyBinder = defaultBinder;
        Culture = CultureInfo.InvariantCulture;
    }

    /// <summary>
    /// Uses a custom value converter.
    /// </summary>
    /// <param name="binder">The converter to use.</param>
    /// <returns>This instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="binder"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <c>BindEnvars</c> captures and reuses this instance. It may call the binder concurrently.
    /// The binder receives full environment values, so it must be deterministic, thread-safe, and must
    /// not log or retain values. Binder failures are sanitized; <see cref="OperationCanceledException"/>
    /// is propagated unchanged.
    /// </remarks>
    public EnvarSettings UseCustomEnvarPropertyBinder(IEnvarPropertyBinder binder)
    {
        ArgumentNullException.ThrowIfNull(binder);

        EnvarPropertyBinder = binder;
        return this;
    }

    /// <summary>
    /// Uses a culture for value conversion.
    /// </summary>
    /// <param name="culture">The parsing culture.</param>
    /// <returns>This instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="culture"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The default is <see cref="CultureInfo.InvariantCulture"/>. <c>BindEnvars</c> captures a read-only
    /// clone and also passes it to fallback <see cref="System.ComponentModel.TypeConverter"/> instances.
    /// </remarks>
    public EnvarSettings UseCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        Culture = culture;
        return this;
    }

    internal IEnvarPropertyBinder EnvarPropertyBinder { get; private set; }

    internal CultureInfo Culture { get; private set; }
}
