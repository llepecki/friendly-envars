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
    private static readonly DefaultEnvarPropertyBinder SharedDefaultBinder = new();

    /// <summary>
    /// Binds mapped environment variables to an options type.
    /// </summary>
    /// <typeparam name="T">A class with a parameterless constructor.</typeparam>
    /// <param name="optionsBuilder">The options registration.</param>
    /// <param name="configure">An optional conversion configuration.</param>
    /// <returns><paramref name="optionsBuilder"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="optionsBuilder"/> is <see langword="null"/>.</exception>
    /// <exception cref="EnvarsException">A mapping is invalid, a read fails, or a value cannot be converted or assigned.</exception>
    /// <exception cref="InvalidOperationException">The same options type and name are already bound.</exception>
    /// <remarks>
    /// This method validates mappings and captures values immediately. An unset variable is skipped;
    /// an empty string is passed to the binder. Later environment changes have no effect.
    /// <para>
    /// Normal Options registration order applies. Later configuration overrides earlier configuration
    /// for the same property and options name.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddOptions&lt;DatabaseSettings&gt;()
    ///     .BindEnvars();
    /// </code>
    /// </example>
    public static OptionsBuilder<T> BindEnvars<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        this OptionsBuilder<T> optionsBuilder,
        Action<EnvarSettings>? configure = null) where T : class, new()
    {
        return BindEnvarsCore(optionsBuilder, configure, ProcessEnvironmentVariableReader.Instance, NullBindingPlanObserver.Instance);
    }

    internal static OptionsBuilder<T> BindEnvarsCore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        OptionsBuilder<T> optionsBuilder,
        Action<EnvarSettings>? configure,
        IEnvironmentVariableReader environmentVariableReader,
        IBindingPlanObserver planObserver) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        var settings = new EnvarSettings(SharedDefaultBinder);
        configure?.Invoke(settings);

        var binder = settings.EnvarPropertyBinder;

        // Clone first: ReadOnly would otherwise freeze the caller's own CultureInfo instance, and the
        // snapshot must be independent of whatever the caller mutates afterwards.
        var culture = CultureInfo.ReadOnly((CultureInfo)settings.Culture.Clone());
        string optionsName = optionsBuilder.Name;

        if (FriendlyEnvarsRegistrationMarker.IsRegistered(optionsBuilder.Services, typeof(T), optionsName))
        {
            throw new InvalidOperationException(
                $"FriendlyEnvars is already registered for options type '{typeof(T).FullName}' and " +
                $"options name '{EnvarsException.FormatOptionsName(optionsName)}'.");
        }

        // Build first so a failure cannot leave a partial registration.
        var plan = BindingPlan.Build(typeof(T), optionsName, environmentVariableReader, planObserver);

        optionsBuilder.Services.AddSingleton(new FriendlyEnvarsRegistrationMarker(typeof(T), optionsName));

        optionsBuilder.Services.AddSingleton<IConfigureOptions<T>>(
            new ConfigureNamedOptions<T>(optionsName, options => plan.Apply(options, binder, culture)));

        return optionsBuilder;
    }
}
