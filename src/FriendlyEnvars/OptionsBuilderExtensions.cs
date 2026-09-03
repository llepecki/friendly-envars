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
    /// <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/>,
    /// <see cref="Microsoft.Extensions.Options.IOptionsSnapshot{TOptions}"/>,
    /// <see cref="Microsoft.Extensions.Options.IOptionsMonitor{TOptions}"/> and
    /// <see cref="Microsoft.Extensions.Options.IOptionsFactory{TOptions}"/> all resolve normally.
    /// Environment variables do not change while the process runs, so every one of them observes the
    /// same values.
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
    ///                 .UseCustomEnvarPropertyBinder(new CustomBinder());
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

        optionsBuilder.Configure(static _ => { });

        string optionsName = optionsBuilder.Name;

        optionsBuilder.Services.AddSingleton<IConfigureOptions<T>>(
            new ConfigureNamedOptions<T>(optionsName, options => Bind(options, optionsName, settings.EnvarPropertyBinder, settings.Culture)));

        return optionsBuilder;
    }

    [StackTraceHidden]
    private static void Bind<T>(T instance, string optionsName, IEnvarPropertyBinder binder, CultureInfo culture)
    {
        var optionsType = typeof(T);

        foreach (var metadata in EnvarPropertyCache.GetOrAdd(optionsType, GetEnvarProperties))
        {
            var property = metadata.Property;
            var targetType = property.PropertyType;
            string environmentVariableName = metadata.Attribute.Name;

            string? value;

            try
            {
                value = Environment.GetEnvironmentVariable(environmentVariableName);
            }
            catch (Exception ex)
            {
                throw EnvarsException.EnvironmentReadFailure(
                    environmentVariableName, optionsType, optionsName, property.Name, targetType, EnvarsException.DescribeCause(ex));
            }

            if (value is null)
            {
                continue;
            }

            if (!property.CanWrite)
            {
                throw EnvarsException.InvalidPropertyShape(environmentVariableName, optionsType, optionsName, property.Name, targetType);
            }

            object? convertedValue;

            // Conversion and assignment are caught separately so the reported failure kind says which of
            // the two went wrong. Every exception is caught, including EnvarsException raised by a custom
            // binder, because an unsanitised one would carry the value straight through.
            try
            {
                convertedValue = binder.Convert(value, targetType, culture);
            }
            catch (Exception ex)
            {
                throw EnvarsException.ConversionFailure(
                    environmentVariableName, optionsType, optionsName, property.Name, targetType, culture.Name, binder.GetType(), EnvarsException.DescribeCause(ex));
            }

            try
            {
                property.SetValue(instance, convertedValue);
            }
            catch (Exception ex)
            {
                throw EnvarsException.AssignmentFailure(
                    environmentVariableName, optionsType, optionsName, property.Name, targetType, EnvarsException.DescribeCause(ex));
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
