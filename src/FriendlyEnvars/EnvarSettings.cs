using System;
using System.Collections.Generic;
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

    /// <summary>
    /// Reads values from the given snapshot instead of the process environment.
    /// </summary>
    /// <param name="variables">Variable names and values; a <see langword="null"/> value means unset.</param>
    /// <returns>This instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="variables"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Intended for tests: consuming code can bind against fixed values without mutating the
    /// process-global environment. The snapshot is copied, so later changes to
    /// <paramref name="variables"/> have no effect.
    /// </remarks>
    public EnvarSettings UseEnvironmentSource(IReadOnlyDictionary<string, string?> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);

        EnvironmentSource = new Dictionary<string, string?>(variables);
        return this;
    }

    /// <summary>
    /// Prepends a prefix to every mapped variable name for this registration.
    /// </summary>
    /// <param name="prefix">The prefix, for example <c>"APP_"</c>.</param>
    /// <returns>This instance.</returns>
    /// <exception cref="ArgumentException"><paramref name="prefix"/> is not a valid name fragment.</exception>
    /// <remarks>
    /// With <c>UseNamePrefix("APP_")</c>, a property mapped to <c>[Envar("PORT")]</c> reads
    /// <c>APP_PORT</c>. Each combined name is validated by the usual rules.
    /// </remarks>
    public EnvarSettings UseNamePrefix(string prefix)
    {
        if (!EnvarAttribute.IsValidName(prefix))
        {
            throw new ArgumentException(
                "The prefix must be non-empty and contain no '=' or control characters.", nameof(prefix));
        }

        NamePrefix = prefix;
        return this;
    }

    internal IEnvarPropertyBinder EnvarPropertyBinder { get; private set; }

    internal CultureInfo Culture { get; private set; }

    internal Dictionary<string, string?>? EnvironmentSource { get; private set; }

    internal string? NamePrefix { get; private set; }
}
