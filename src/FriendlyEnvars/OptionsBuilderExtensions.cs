using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Reflection;

namespace FriendlyEnvars;

public static class OptionsBuilderExtensions
{
    public static OptionsBuilder<T> BindFromEnvarAttributes<T>(this OptionsBuilder<T> optionsBuilder, Action<EnvarSettings>? configure = null) where T : class, new()
    {
        var settings = new EnvarSettings();
        configure?.Invoke(settings);

        optionsBuilder.Configure(_ => { });

        optionsBuilder.Services.AddSingleton<IConfigureOptions<T>>(new ConfigureNamedOptions<T>(optionsBuilder.Name, options =>
        {
            var boundInstance = BindFromEnvironmentVariables<T>(settings.EnvarPropertyBinder);
            var type = typeof(T);

            foreach (var property in type.GetProperties())
            {
                if (property is { CanRead: true, CanWrite: true })
                {
                    var value = property.GetValue(boundInstance);

                    if (value != null)
                    {
                        property.SetValue(options, value);
                    }
                }
            }
        }));

        if (!settings.IsOptionsMonitorAllowed)
        {
            optionsBuilder.Services.AddSingleton<IOptionsMonitor<T>>(_ => throw new NotSupportedException(
                $"IOptionsMonitor<{typeof(T).Name}> is not supported for options bound with FriendlyEnvars. " +
                "The library assumes that environment variables are static during application runtime. " +
                "Use IOptions<T> instead."));
        }

        if (!settings.IsOptionsSnapshotAllowed)
        {
            optionsBuilder.Services.AddScoped<IOptionsSnapshot<T>>(_ => throw new NotSupportedException(
                $"IOptionsSnapshot<{typeof(T).Name}> is not supported for options bound with FriendlyEnvars. " +
                "The library assumes that environment variables are static during application runtime. " +
                "Use IOptions<T> instead."));
        }

        return optionsBuilder;
    }

    internal static T BindFromEnvironmentVariables<T>(IEnvarPropertyBinder binder) where T : new()
    {
        var instance = new T();
        var type = typeof(T);

        foreach (var property in type.GetProperties())
        {
            var envVarAttribute = property.GetCustomAttribute<EnvarAttribute>();

            if (envVarAttribute == null)
            {
                continue;
            }

            var value = Environment.GetEnvironmentVariable(envVarAttribute.Name);

            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            try
            {
                var convertedValue = binder.Convert(value, property.PropertyType);
                property.SetValue(instance, convertedValue);
            }
            catch (Exception ex)
            {
                throw new EnvarsException($"Failed to convert environment variable '{envVarAttribute.Name}' value '{value}' to type '{property.PropertyType.Name}' for property '{property.Name}'.", ex);
            }
        }

        return instance;
    }
}
