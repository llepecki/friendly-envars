using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace FriendlyEnvars;

public static class OptionsBuilderExtensions
{
    private static readonly ConcurrentDictionary<Type, EnvarPropertyMetadata[]> EnvarPropertyCache = new();

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
    /// is not set, the property retains its default value. Empty values are passed to the binder.
    /// </para>
    /// <para>
    /// By default, <see cref="Microsoft.Extensions.Options.IOptionsSnapshot{TOptions}"/> and 
    /// <see cref="Microsoft.Extensions.Options.IOptionsMonitor{TOptions}"/> are enabled and will work 
    /// normally. This can be changed using the <paramref name="configure"/> delegate.
    /// </para>
    /// </remarks>
    /// <example>
    /// <para>Basic usage:</para>
    /// <code>
    /// services.AddOptions&lt;DatabaseSettings&gt;()
    ///     .BindEnvars();
    /// </code>
    /// <para>With validation:</para>
    /// <code>
    /// services.AddOptions&lt;DatabaseSettings&gt;()
    ///     .BindEnvars()
    ///     .ValidateDataAnnotations()
    ///     .ValidateOnStart();
    /// </code>
    /// <para>With custom configuration:</para>
    /// <code>
    /// using System.Globalization;
    ///
    /// services.AddOptions&lt;DatabaseSettings&gt;()
    ///     .BindEnvars(settings =&gt;
    ///     {
    ///         settings.UseCulture(CultureInfo.GetCultureInfo("en-US"))
    ///                 .UseCustomEnvarPropertyBinder(new CustomBinder())
    ///                 .BlockOptionsSnapshot();
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
    public static OptionsBuilder<T> BindEnvars<T>(this OptionsBuilder<T> optionsBuilder, Action<EnvarSettings>? configure = null) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        var settings = new EnvarSettings();
        configure?.Invoke(settings);

        optionsBuilder.Configure(_ => { });

        optionsBuilder.Services.AddSingleton<IConfigureOptions<T>>(
            new ConfigureNamedOptions<T>(optionsBuilder.Name, options => Bind(options, settings.EnvarPropertyBinder, settings.Culture)));

        if (!settings.IsOptionsMonitorAllowed)
        {
            optionsBuilder.Services.AddSingleton<IOptionsMonitor<T>>(_ => throw new NotSupportedException(
                $"IOptionsMonitor<{typeof(T).Name}> has been explicitly blocked by calling BlockOptionsMonitor(). " +
                "Since environment variables are static during application runtime, IOptionsMonitor provides no additional value. " +
                "Use IOptions<T> instead or remove the BlockOptionsMonitor() call to re-enable."));
        }

        if (!settings.IsOptionsSnapshotAllowed)
        {
            optionsBuilder.Services.AddScoped<IOptionsSnapshot<T>>(_ => throw new NotSupportedException(
                $"IOptionsSnapshot<{typeof(T).Name}> has been explicitly blocked by calling BlockOptionsSnapshot(). " +
                "Since environment variables are static during application runtime, IOptionsSnapshot provides no additional value. " +
                "Use IOptions<T> instead or remove the BlockOptionsSnapshot() call to re-enable."));
        }

        return optionsBuilder;
    }

    [StackTraceHidden]
    private static void Bind<T>(T instance, IEnvarPropertyBinder binder, CultureInfo culture)
    {
        var type = typeof(T);

        foreach (var metadata in EnvarPropertyCache.GetOrAdd(type, GetEnvarProperties))
        {
            var value = Environment.GetEnvironmentVariable(metadata.Attribute.Name);

            if (value is null)
            {
                continue;
            }

            if (!metadata.Property.CanWrite)
            {
                throw new EnvarsException($"Property '{metadata.Property.Name}' with the {nameof(EnvarAttribute)} does not have an accessible setter");
            }

            try
            {
                var convertedValue = binder.Convert(value, metadata.Property.PropertyType, culture);
                metadata.Property.SetValue(instance, convertedValue);
            }
            catch (Exception ex) when (ex is not EnvarsException)
            {
                throw new EnvarsException($"Failed to convert environment variable '{metadata.Attribute.Name}' with value '{value}' to type '{metadata.Property.PropertyType.Name}' for property '{metadata.Property.Name}'", ex);
            }
        }
    }

    private static EnvarPropertyMetadata[] GetEnvarProperties(Type type)
    {
        var properties = type.GetProperties();
        var metadata = new List<EnvarPropertyMetadata>(properties.Length);

        foreach (var property in properties)
        {
            var envarAttribute = property.GetCustomAttribute<EnvarAttribute>();
            if (envarAttribute is null)
            {
                continue;
            }

            metadata.Add(new EnvarPropertyMetadata(property, envarAttribute));
        }

        return metadata.ToArray();
    }

    private readonly record struct EnvarPropertyMetadata(PropertyInfo Property, EnvarAttribute Attribute);
}
