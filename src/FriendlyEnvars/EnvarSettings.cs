using System;
using System.Globalization;

namespace FriendlyEnvars;

/// <summary>
/// Configuration settings for environment variable binding behavior.
/// </summary>
/// <remarks>
/// Provides a fluent API for configuring how environment variables are converted: which binder to use
/// and which culture to parse with. Each method mutates and returns this same instance rather than
/// creating a new one, so calls can be chained. The object is read only while <c>BindEnvars</c> runs;
/// changing it afterwards has no effect.
/// </remarks>
/// <example>
/// <code>
/// using System.Globalization;
///
/// services.AddOptions&lt;DatabaseSettings&gt;()
///     .BindEnvars(settings =&gt;
///     {
///         settings
///             .UseCustomEnvarPropertyBinder(new CustomBinder())
///             .UseCulture(CultureInfo.GetCultureInfo("en-US"));
///     });
/// </code>
/// </example>
public sealed record EnvarSettings
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EnvarSettings"/> class with default values.
    /// </summary>
    /// <remarks>
    /// Default configuration:
    /// <list type="bullet">
    /// <item><description>Uses <see cref="DefaultEnvarPropertyBinder"/> for type conversion</description></item>
    /// <item><description>Uses <see cref="CultureInfo.InvariantCulture"/> for parsing</description></item>
    /// </list>
    /// </remarks>
    /// <param name="defaultBinder">
    /// The shared stateless binder used unless the caller supplies one of their own.
    /// </param>
    internal EnvarSettings(IEnvarPropertyBinder defaultBinder)
    {
        EnvarPropertyBinder = defaultBinder;
        Culture = CultureInfo.InvariantCulture;
    }

    /// <summary>
    /// Configures a custom property binder for type conversion.
    /// </summary>
    /// <param name="binder">The custom property binder to use for type conversion.</param>
    /// <returns>The same <see cref="EnvarSettings"/> instance, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="binder"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// The reference is captured while <c>BindEnvars</c> runs, immediately after the configuration
    /// delegate returns, and the one instance is reused for every options instance this registration
    /// produces. Whatever state the binder holds internally is the caller's responsibility: the library
    /// never copies, resets or synchronises it.
    /// </para>
    /// <para>
    /// <b>A binder is trusted code that receives secrets.</b> It is handed the complete environment
    /// value verbatim, which routinely means a password, connection string or API key. The library does
    /// not sandbox, redact or inspect it. An implementation must:
    /// </para>
    /// <list type="bullet">
    /// <item><description>be deterministic - the same input must always produce an equivalent result;</description></item>
    /// <item><description>be thread-safe, because one instance is shared by every options instance a
    /// registration produces and the library calls it concurrently without serialising;</description></item>
    /// <item><description>never log, print, cache or otherwise retain the value it is given, in memory or
    /// anywhere else.</description></item>
    /// </list>
    /// <para>
    /// Exceptions thrown by a binder are sanitised: only the exception's type name survives, never its
    /// message. The one exception is <see cref="OperationCanceledException"/>, which is propagated
    /// unchanged as the caller's own control flow, so a cancellation message must not carry a value.
    /// </para>
    /// </remarks>
    /// <example>
    /// <para>Custom binder class:</para>
    /// <code>
    /// public class CustomBinder : IEnvarPropertyBinder
    /// {
    ///     public object? Convert(string value, Type targetType, CultureInfo culture)
    ///     {
    ///         // Custom conversion logic
    ///         return value;
    ///     }
    /// }
    /// </code>
    /// <para>Usage:</para>
    /// <code>
    /// services.AddOptions&lt;MyConfig&gt;()
    ///     .BindEnvars(settings =&gt;
    ///     {
    ///         settings.UseCustomEnvarPropertyBinder(new CustomBinder());
    ///     });
    /// </code>
    /// </example>
    public EnvarSettings UseCustomEnvarPropertyBinder(IEnvarPropertyBinder binder)
    {
        ArgumentNullException.ThrowIfNull(binder);

        EnvarPropertyBinder = binder;
        return this;
    }

    /// <summary>
    /// Configures the culture used for type conversion.
    /// </summary>
    /// <param name="culture">The culture to use for parsing numeric and date values.</param>
    /// <returns>The same <see cref="EnvarSettings"/> instance, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="culture"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// <c>BindEnvars</c> captures a read-only clone of this culture, so mutating the instance you passed
    /// in afterwards cannot change how values are parsed.
    /// </para>
    /// By default, <see cref="CultureInfo.InvariantCulture"/> is used to ensure consistent
    /// parsing regardless of the system locale. Use this method when environment variables
    /// contain culture-specific formats. This culture is also applied to fallback
    /// <see cref="System.ComponentModel.TypeConverter"/> conversions in the default binder.
    /// </remarks>
    /// <example>
    /// <para>For European number formats (comma as decimal separator):</para>
    /// <code>
    /// using System.Globalization;
    /// 
    /// services.AddOptions&lt;MyConfig&gt;()
    ///     .BindEnvars(settings =&gt;
    ///     {
    ///         settings.UseCulture(CultureInfo.GetCultureInfo("de-DE"));
    ///     });
    /// </code>
    /// <para>Environment variable example:</para>
    /// <code>
    /// PRICE=123,45
    /// // Will be parsed as 123.45 with German culture
    /// </code>
    /// </example>
    public EnvarSettings UseCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        Culture = culture;
        return this;
    }

    /// <summary>
    /// Gets the property binder used for type conversion.
    /// </summary>
    internal IEnvarPropertyBinder EnvarPropertyBinder { get; private set; }

    /// <summary>
    /// Gets the culture used for type conversion.
    /// </summary>
    internal CultureInfo Culture { get; private set; }
}
