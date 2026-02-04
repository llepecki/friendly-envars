using System;
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
    /// <item><description>Enables <see cref="Microsoft.Extensions.Options.IOptionsSnapshot{TOptions}"/></description></item>
    /// <item><description>Enables <see cref="Microsoft.Extensions.Options.IOptionsMonitor{TOptions}"/></description></item>
    /// </list>
    /// </remarks>
    internal EnvarSettings()
    {
        EnvarPropertyBinder = new DefaultEnvarPropertyBinder();
        Culture = CultureInfo.InvariantCulture;
        IsOptionsSnapshotAllowed = true;
        IsOptionsMonitorAllowed = true;
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
    ///     .BindEnvars(settings =&gt;
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

    /// <summary>
    /// Gets a value indicating whether <see cref="Microsoft.Extensions.Options.IOptionsSnapshot{TOptions}"/> is allowed.
    /// </summary>
    internal bool IsOptionsSnapshotAllowed { get; private set; }

    /// <summary>
    /// Gets a value indicating whether <see cref="Microsoft.Extensions.Options.IOptionsMonitor{TOptions}"/> is allowed.
    /// </summary>
    internal bool IsOptionsMonitorAllowed { get; private set; }

    /// <summary>
    /// Blocks <see cref="Microsoft.Extensions.Options.IOptionsSnapshot{TOptions}"/> resolution.
    /// </summary>
    /// <returns>A new <see cref="EnvarSettings"/> instance with options snapshot disabled.</returns>
    /// <remarks>
    /// <para>
    /// When disabled, <see cref="Microsoft.Extensions.Options.IOptionsSnapshot{TOptions}"/> resolution
    /// will throw a <see cref="NotSupportedException"/>.
    /// </para>
    /// <para>
    /// This is useful when you want to ensure that only <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/>
    /// is used and prevent accidental injection of snapshot-based options.
    /// </para>
    /// </remarks>
    /// <example>
    /// <para>Configuration:</para>
    /// <code>
    /// services.AddOptions&lt;DatabaseSettings&gt;()
    ///     .BindEnvars(settings =&gt;
    ///     {
    ///         settings.BlockOptionsSnapshot();
    ///     });
    /// </code>
    /// <para>This will throw when trying to inject IOptionsSnapshot&lt;DatabaseSettings&gt;:</para>
    /// <code>
    /// public class MyService
    /// {
    ///     // This will throw NotSupportedException
    ///     public MyService(IOptionsSnapshot&lt;DatabaseSettings&gt; config) { }
    /// }
    /// </code>
    /// </example>
    public EnvarSettings BlockOptionsSnapshot()
    {
        IsOptionsSnapshotAllowed = false;
        return this;
    }

    /// <summary>
    /// Blocks <see cref="Microsoft.Extensions.Options.IOptionsMonitor{TOptions}"/> resolution.
    /// </summary>
    /// <returns>A new <see cref="EnvarSettings"/> instance with options monitor disabled.</returns>
    /// <remarks>
    /// <para>
    /// When disabled, <see cref="Microsoft.Extensions.Options.IOptionsMonitor{TOptions}"/> resolution
    /// will throw a <see cref="NotSupportedException"/>.
    /// </para>
    /// <para>
    /// This is useful when you want to ensure that only <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/>
    /// is used and prevent accidental injection of monitor-based options.
    /// </para>
    /// </remarks>
    /// <example>
    /// <para>Configuration:</para>
    /// <code>
    /// services.AddOptions&lt;DatabaseSettings&gt;()
    ///     .BindEnvars(settings =&gt;
    ///     {
    ///         settings.BlockOptionsMonitor();
    ///     });
    /// </code>
    /// <para>This will throw when trying to inject IOptionsMonitor&lt;DatabaseSettings&gt;:</para>
    /// <code>
    /// public class MyService
    /// {
    ///     // This will throw NotSupportedException
    ///     public MyService(IOptionsMonitor&lt;DatabaseSettings&gt; monitor) { }
    /// }
    /// </code>
    /// </example>
    public EnvarSettings BlockOptionsMonitor()
    {
        IsOptionsMonitorAllowed = false;
        return this;
    }
}
