using System.Globalization;

namespace FriendlyEnvars;

/// <summary>
/// Configuration settings for environment variable binding behavior.
/// </summary>
/// <remarks>
/// This record provides a fluent API for configuring how environment variables 
/// are bound to configuration objects, including type conversion, culture settings, 
/// and options pattern behavior.
/// </remarks>
/// <example>
/// <code>
/// using System.Globalization;
/// 
/// services.AddOptions&lt;DatabaseSettings&gt;()
///     .BindFromEnvarAttributes(settings =&gt;
///     {
///         settings.UseCustomEnvarPropertyBinder(new CustomBinder())
///                 .UseCulture(CultureInfo.GetCultureInfo("en-US"))
///                 .AllowOptionsSnapshot()
///                 .AllowOptionsMonitor();
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
    /// <item><description>Disables <see cref="Microsoft.Extensions.Options.IOptionsSnapshot{TOptions}"/></description></item>
    /// <item><description>Disables <see cref="Microsoft.Extensions.Options.IOptionsMonitor{TOptions}"/></description></item>
    /// </list>
    /// </remarks>
    internal EnvarSettings()
    {
        EnvarPropertyBinder = new DefaultEnvarPropertyBinder();
        Culture = CultureInfo.InvariantCulture;
        IsOptionsSnapshotAllowed = false;
        IsOptionsMonitorAllowed = false;
    }

    /// <summary>
    /// Configures a custom property binder for type conversion.
    /// </summary>
    /// <param name="binder">The custom property binder to use for type conversion.</param>
    /// <returns>A new <see cref="EnvarSettings"/> instance with the specified binder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="binder"/> is null.</exception>
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
    ///     .BindFromEnvarAttributes(settings =&gt;
    ///     {
    ///         settings.UseCustomEnvarPropertyBinder(new CustomBinder());
    ///     });
    /// </code>
    /// </example>
    public EnvarSettings UseCustomEnvarPropertyBinder(IEnvarPropertyBinder binder)
    {
        EnvarPropertyBinder = binder;
        return this;
    }

    /// <summary>
    /// Configures the culture used for type conversion.
    /// </summary>
    /// <param name="culture">The culture to use for parsing numeric and date values.</param>
    /// <returns>A new <see cref="EnvarSettings"/> instance with the specified culture.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="culture"/> is null.</exception>
    /// <remarks>
    /// By default, <see cref="CultureInfo.InvariantCulture"/> is used to ensure consistent 
    /// parsing regardless of the system locale. Use this method when environment variables 
    /// contain culture-specific formats.
    /// </remarks>
    /// <example>
    /// <para>For European number formats (comma as decimal separator):</para>
    /// <code>
    /// using System.Globalization;
    /// 
    /// services.AddOptions&lt;MyConfig&gt;()
    ///     .BindFromEnvarAttributes(settings =&gt;
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
        Culture = culture;
        return this;
    }

    /// <summary>
    /// Allows <see cref="Microsoft.Extensions.Options.IOptionsSnapshot{TOptions}"/> to be resolved.
    /// </summary>
    /// <returns>A new <see cref="EnvarSettings"/> instance with options snapshot enabled.</returns>
    /// <remarks>
    /// <para>
    /// By default, <see cref="Microsoft.Extensions.Options.IOptionsSnapshot{TOptions}"/> resolution 
    /// throws a <see cref="NotSupportedException"/> because environment variables are static 
    /// during application runtime.
    /// </para>
    /// <para>
    /// When enabled, <see cref="Microsoft.Extensions.Options.IOptionsSnapshot{TOptions}"/> will 
    /// always return the same values that were read at application startup.
    /// </para>
    /// </remarks>
    /// <example>
    /// <para>Configuration:</para>
    /// <code>
    /// services.AddOptions&lt;DatabaseSettings&gt;()
    ///     .BindFromEnvarAttributes(settings =&gt;
    ///     {
    ///         settings.AllowOptionsSnapshot();
    ///     });
    /// </code>
    /// <para>Now you can inject IOptionsSnapshot&lt;DatabaseSettings&gt; in your services:</para>
    /// <code>
    /// public class MyService
    /// {
    ///     public MyService(IOptionsSnapshot&lt;DatabaseSettings&gt; config)
    ///     {
    ///         // config.Value will always return the same values from startup
    ///     }
    /// }
    /// </code>
    /// </example>
    public EnvarSettings AllowOptionsSnapshot()
    {
        IsOptionsSnapshotAllowed = true;
        return this;
    }

    /// <summary>
    /// Allows <see cref="Microsoft.Extensions.Options.IOptionsMonitor{TOptions}"/> to be resolved.
    /// </summary>
    /// <returns>A new <see cref="EnvarSettings"/> instance with options monitor enabled.</returns>
    /// <remarks>
    /// <para>
    /// By default, <see cref="Microsoft.Extensions.Options.IOptionsMonitor{TOptions}"/> resolution 
    /// throws a <see cref="NotSupportedException"/> because environment variables are static 
    /// during application runtime.
    /// </para>
    /// <para>
    /// When enabled, <see cref="Microsoft.Extensions.Options.IOptionsMonitor{TOptions}"/> will 
    /// always return the same values that were read at application startup and will never 
    /// trigger change notifications.
    /// </para>
    /// </remarks>
    /// <example>
    /// <para>Configuration:</para>
    /// <code>
    /// services.AddOptions&lt;DatabaseSettings&gt;()
    ///     .BindFromEnvarAttributes(settings =&gt;
    ///     {
    ///         settings.AllowOptionsMonitor();
    ///     });
    /// </code>
    /// <para>Now you can inject IOptionsMonitor&lt;DatabaseSettings&gt; in your services:</para>
    /// <code>
    /// public class MyService
    /// {
    ///     public MyService(IOptionsMonitor&lt;DatabaseSettings&gt; monitor)
    ///     {
    ///         // monitor.CurrentValue will always return the same values from startup
    ///         // OnChange callbacks will never be triggered
    ///     }
    /// }
    /// </code>
    /// </example>
    public EnvarSettings AllowOptionsMonitor()
    {
        IsOptionsMonitorAllowed = true;
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

    /// <summary>
    /// Gets a value indicating whether <see cref="Microsoft.Extensions.Options.IOptionsSnapshot{TOptions}"/> is allowed.
    /// </summary>
    internal bool IsOptionsSnapshotAllowed { get; private set; }

    /// <summary>
    /// Gets a value indicating whether <see cref="Microsoft.Extensions.Options.IOptionsMonitor{TOptions}"/> is allowed.
    /// </summary>
    internal bool IsOptionsMonitorAllowed { get; private set; }
}
