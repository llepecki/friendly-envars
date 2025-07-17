using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace FriendlyEnvars;

public static class OptionsBuilderExtensions
{
    /// <summary>
    /// Configures the options to be bound from environment variables using <see cref="EnvarAttribute"/> decorations.
    /// </summary>
    /// <typeparam name="T">The type of options to bind. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="optionsBuilder">The options builder to configure.</param>
    /// <param name="configure">Optional configuration delegate to customize binding behavior.</param>
    /// <returns>The same <see cref="OptionsBuilder{T}"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="optionsBuilder"/> is null.</exception>
    /// <exception cref="EnvarsException">Thrown when environment variable conversion fails or a property doesn't have a setter.</exception>
    /// <remarks>
    /// <para>
    /// This method scans all properties of type <typeparamref name="T"/> decorated with <see cref="EnvarAttribute"/> 
    /// and binds their values from the corresponding environment variables.
    /// </para>
    /// <para>
    /// Properties without the <see cref="EnvarAttribute"/> are ignored. If an environment variable 
    /// is not set or is empty, the property retains its default value.
    /// </para>
    /// <para>
    /// By default, <see cref="Microsoft.Extensions.Options.IOptionsSnapshot{TOptions}"/> and 
    /// <see cref="Microsoft.Extensions.Options.IOptionsMonitor{TOptions}"/> resolution will throw 
    /// <see cref="NotSupportedException"/>. This can be changed using the <paramref name="configure"/> delegate.
    /// </para>
    /// </remarks>
    /// <example>
    /// <para>Basic usage:</para>
    /// <code>
    /// services.AddOptions&lt;DatabaseSettings&gt;()
    ///     .BindFromEnvarAttributes();
    /// </code>
    /// <para>With validation:</para>
    /// <code>
    /// services.AddOptions&lt;DatabaseSettings&gt;()
    ///     .BindFromEnvarAttributes()
    ///     .ValidateDataAnnotations()
    ///     .ValidateOnStart();
    /// </code>
    /// <para>With custom configuration:</para>
    /// <code>
    /// using System.Globalization;
    /// 
    /// services.AddOptions&lt;DatabaseSettings&gt;()
    ///     .BindFromEnvarAttributes(settings =&gt;
    ///     {
    ///         settings.UseCulture(CultureInfo.GetCultureInfo("en-US"))
    ///                 .UseCustomEnvarPropertyBinder(new CustomBinder())
    ///                 .AllowOptionsSnapshot();
    ///     });
    /// </code>
    /// <para>Configuration class example:</para>
    /// <code>
    /// public record DatabaseSettings
    /// {
    ///     [Required]
    ///     [Envar("DB_HOST")]
    ///     public string Host { get; init; } = string.Empty;
    /// 
    ///     [Range(1, 65535)]
    ///     [Envar("DB_PORT")]
    ///     public int Port { get; init; } = 5432;
    /// 
    ///     [Envar("DB_SSL_ENABLED")]
    ///     public bool SslEnabled { get; init; } = true;
    /// }
    /// </code>
    /// <para>Environment variables:</para>
    /// <code>
    /// DB_HOST=production.example.com
    /// DB_PORT=5433
    /// DB_SSL_ENABLED=false
    /// </code>
    /// </example>
    public static OptionsBuilder<T> BindFromEnvarAttributes<T>(this OptionsBuilder<T> optionsBuilder, Action<EnvarSettings>? configure = null) where T : class, new()
    {
        var settings = new EnvarSettings();
        configure?.Invoke(settings);

        optionsBuilder.Configure(_ => { });

        optionsBuilder.Services.AddSingleton<IConfigureOptions<T>>(
            new ConfigureNamedOptions<T>(optionsBuilder.Name, options => BindFromEnvars(options, settings.EnvarPropertyBinder, settings.Culture)));

        if (!settings.IsOptionsMonitorAllowed)
        {
            optionsBuilder.Services.AddSingleton<IOptionsMonitor<T>>(_ => throw new NotSupportedException(
                $"IOptionsMonitor<{typeof(T).Name}> is not supported for options bound with FriendlyEnvars. " +
                "The library assumes that environment variables are static during application runtime. " +
                "Use IOptions<T> instead or explicitly allow options monitor by calling AllowOptionsMonitor."));
        }

        if (!settings.IsOptionsSnapshotAllowed)
        {
            optionsBuilder.Services.AddScoped<IOptionsSnapshot<T>>(_ => throw new NotSupportedException(
                $"IOptionsSnapshot<{typeof(T).Name}> is not supported for options bound with FriendlyEnvars. " +
                "The library assumes that environment variables are static during application runtime. " +
                "Use IOptions<T> instead or explicitly allow options snapshot by calling AllowOptionsSnapshot."));
        }

        return optionsBuilder;
    }

    [StackTraceHidden]
    private static void BindFromEnvars<T>(T instance, IEnvarPropertyBinder binder, CultureInfo culture)
    {
        var type = typeof(T);

        foreach (var property in type.GetProperties())
        {
            var envarAttribute = property.GetCustomAttribute<EnvarAttribute>();

            if (envarAttribute == null)
            {
                continue;
            }

            var value = Environment.GetEnvironmentVariable(envarAttribute.Name);

            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            if (!property.CanWrite)
            {
                throw new EnvarsException($"Property '{property.Name}' with the {nameof(EnvarAttribute)} does not have an accessible setter");
            }

            try
            {
                var convertedValue = binder.Convert(value, property.PropertyType, culture);
                property.SetValue(instance, convertedValue);
            }
            catch (Exception ex) when (ex is not EnvarsException)
            {
                throw new EnvarsException($"Failed to convert environment variable '{envarAttribute.Name}' with value '{value}' to type '{property.PropertyType.Name}' for property '{property.Name}'", ex);
            }
        }
    }
}
