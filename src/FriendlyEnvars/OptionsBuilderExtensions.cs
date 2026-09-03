using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FriendlyEnvars;

/// <summary>
/// Adds environment-variable binding to an <see cref="OptionsBuilder{TOptions}"/>.
/// </summary>
public static class OptionsBuilderExtensions
{
    /// <summary>
    /// The binder used when the caller does not supply one. It is stateless, so a single instance is
    /// shared by every registration rather than allocated per call.
    /// </summary>
    private static readonly DefaultEnvarPropertyBinder SharedDefaultBinder = new();

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
    /// This registers an ordinary <see cref="IConfigureOptions{TOptions}"/>, so it composes with other
    /// options sources by the normal rule: whichever registration runs last wins. Configuration
    /// registered before this call is overwritten by a captured environment value; configuration
    /// registered afterwards overwrites the bound value. Environment values are not forced to take
    /// priority, and no <see cref="IPostConfigureOptions{TOptions}"/> step is registered. A variable that
    /// is not set is skipped, so it never clears a value an earlier registration established.
    /// </para>
    /// <para>
    /// <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/>,
    /// <see cref="Microsoft.Extensions.Options.IOptionsSnapshot{TOptions}"/>,
    /// <see cref="Microsoft.Extensions.Options.IOptionsMonitor{TOptions}"/> and
    /// <see cref="Microsoft.Extensions.Options.IOptionsFactory{TOptions}"/> all resolve normally.
    /// Every value is captured once, while this method runs, and every options instance is built from
    /// that snapshot, so all four abstractions observe the same values. Changing a variable afterwards
    /// changes nothing; the values do not refresh.
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
    public static OptionsBuilder<T> BindEnvars<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        this OptionsBuilder<T> optionsBuilder,
        Action<EnvarSettings>? configure = null) where T : class, new()
    {
        return BindEnvarsCore(optionsBuilder, configure, ProcessEnvironmentVariableReader.Instance, NullBindingPlanObserver.Instance);
    }

    /// <summary>
    /// The single implementation of <see cref="BindEnvars{T}"/>, parameterised by the environment reader
    /// and the plan observer so that tests can count reads and metadata inspections.
    /// </summary>
    /// <remarks>
    /// Neither seam is exposed publicly, and neither is captured by the registered configurator: the
    /// configurator closes over the completed plan only.
    /// </remarks>
    internal static OptionsBuilder<T> BindEnvarsCore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        OptionsBuilder<T> optionsBuilder,
        Action<EnvarSettings>? configure,
        IEnvironmentVariableReader environmentVariableReader,
        IBindingPlanObserver planObserver) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        var settings = new EnvarSettings(SharedDefaultBinder);
        configure?.Invoke(settings);

        // Captured now, so that mutating the settings object afterwards cannot influence binding. The
        // culture is cloned and frozen as well, because CultureInfo is mutable and the caller keeps a
        // reference to the instance they supplied.
        var binder = settings.EnvarPropertyBinder;
        var culture = CultureInfo.ReadOnly((CultureInfo)settings.Culture.Clone());
        string optionsName = optionsBuilder.Name;

        // Rejected before anything is built, and before the service collection is touched.
        if (FriendlyEnvarsRegistrationMarker.IsRegistered(optionsBuilder.Services, typeof(T), optionsName))
        {
            throw new InvalidOperationException(
                $"FriendlyEnvars is already registered for options type '{typeof(T).FullName}' and " +
                $"options name '{EnvarsException.FormatOptionsName(optionsName)}'.");
        }

        // Discovery, validation and the environment snapshot all happen here, before the service
        // collection is touched. A failure therefore leaves no partial registration behind.
        var plan = BindingPlan.Build(typeof(T), optionsName, environmentVariableReader, planObserver);

        optionsBuilder.Services.AddSingleton(new FriendlyEnvarsRegistrationMarker(typeof(T), optionsName));

        optionsBuilder.Services.AddSingleton<IConfigureOptions<T>>(
            new ConfigureNamedOptions<T>(optionsName, options => plan.Apply(options, binder, culture)));

        return optionsBuilder;
    }
}
