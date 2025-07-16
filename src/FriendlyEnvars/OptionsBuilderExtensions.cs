using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace FriendlyEnvars;

public static class OptionsBuilderExtensions
{
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
