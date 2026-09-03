using FriendlyEnvars;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

// FriendlyEnvars sample. Run it with no arguments for the success path, or with --invalid-validation to
// see data-annotation validation reject bad configuration during host startup.
//
// Two exit codes are contractual and are asserted by eng/run-sample.sh:
//   0 - default mode, after printing SampleOutput.Success
//   2 - --invalid-validation mode, after printing SampleOutput.ValidationFailed
//
// Nothing derived from a secret is ever written to stdout or stderr.

bool invalidValidation = args.Contains("--invalid-validation", StringComparer.Ordinal);

SampleEnvironment.Apply(invalidValidation);

// Defaults are disabled so the sample's output is exactly what it prints: no logging providers and no
// configuration providers competing with FriendlyEnvars.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    DisableDefaults = true
});

builder.Services
    .AddOptions<DatabaseSettings>()
    .BindEnvars(static settings => settings.UseCustomEnvarPropertyBinder(new SecondsAwareBinder()))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<ApiSettings>()
    .BindEnvars()
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<FeatureFlags>()
    .BindEnvars();

builder.Services.AddSingleton<ConfigurationReporter>();

using var host = builder.Build();

if (invalidValidation)
{
    try
    {
        await host.StartAsync();
    }
    catch (OptionsValidationException)
    {
        // Only this exception is expected here. Its message is not printed: it is not needed, and not
        // printing it keeps the output free of anything value-derived.
        Console.WriteLine(SampleOutput.ValidationFailed);
        return 2;
    }

    // Reaching this point means invalid configuration was accepted, which is a failure of the sample's
    // contract. The service is deliberately not resolved.
    Console.Error.WriteLine("Expected OptionsValidationException during StartAsync, but startup succeeded.");
    return 1;
}

await host.StartAsync();

host.Services.GetRequiredService<ConfigurationReporter>().Report();

await host.StopAsync();

Console.WriteLine(SampleOutput.Success);
return 0;

/// <summary>The exact lines eng/run-sample.sh asserts on.</summary>
internal static class SampleOutput
{
    public const string Success = "Sample completed successfully!";
    public const string ValidationFailed = "Validation failed during StartAsync as expected.";
}

/// <summary>
/// Populates the environment the sample binds from, without overwriting anything the caller already set.
/// </summary>
internal static class SampleEnvironment
{
    /// <summary>
    /// Stand-in credentials. They are not real, and nothing derived from them is ever printed - that is
    /// asserted by eng/run-sample.sh, which checks the captured output for every 6-character window of
    /// these literals.
    /// </summary>
    private const string FakePassword = "QZXJKVWYPLMB0000";

    private const string FakeApiKey = "MBLPYWVKJXZQ1111";

    public static void Apply(bool invalidValidation)
    {
        SetIfAbsent("SAMPLE_DB_HOST", "db.example.com");
        SetIfAbsent("SAMPLE_DB_NAME", "sample_db");
        SetIfAbsent("SAMPLE_DB_USER", "sample_user");
        SetIfAbsent("SAMPLE_DB_PASSWORD", FakePassword);
        SetIfAbsent("SAMPLE_DB_SSL_ENABLED", "true");

        // Bound through SecondsAwareBinder, which accepts a plain number of seconds.
        SetIfAbsent("SAMPLE_DB_CONNECTION_TIMEOUT", "45");

        SetIfAbsent("SAMPLE_API_BASE_URL", "https://api.example.com");
        SetIfAbsent("SAMPLE_API_KEY", FakeApiKey);
        SetIfAbsent("SAMPLE_API_TIMEOUT_SECONDS", "60");
        SetIfAbsent("SAMPLE_API_SUPPORT_EMAIL", "support@example.com");

        SetIfAbsent("SAMPLE_FEATURE_LOGGING", "true");
        SetIfAbsent("SAMPLE_FEATURE_CACHING", "true");
        SetIfAbsent("SAMPLE_FEATURE_METRICS", "false");

        // 70000 is outside [Range(1, 65535)]. It converts to int perfectly well, so binding succeeds and
        // the failure surfaces where it should: data-annotation validation during StartAsync.
        Environment.SetEnvironmentVariable("SAMPLE_DB_PORT", invalidValidation ? "70000" : "5432");
    }

    private static void SetIfAbsent(string name, string value)
    {
        if (Environment.GetEnvironmentVariable(name) is null)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }
}

/// <summary>
/// Database configuration. Declared as a class rather than a record on purpose: a record's generated
/// <see cref="object.ToString"/> would print every property, including the password.
/// </summary>
public class DatabaseSettings
{
    [Required]
    [Envar("SAMPLE_DB_HOST")]
    public string Host { get; init; } = string.Empty;

    [Range(1, 65535)]
    [Envar("SAMPLE_DB_PORT")]
    public int Port { get; init; }

    [Required]
    [Envar("SAMPLE_DB_NAME")]
    public string DatabaseName { get; init; } = string.Empty;

    [Required]
    [Envar("SAMPLE_DB_USER")]
    public string Username { get; init; } = string.Empty;

    [Envar("SAMPLE_DB_PASSWORD")]
    public string? Password { get; init; }

    [Envar("SAMPLE_DB_SSL_ENABLED")]
    public bool SslEnabled { get; init; } = true;

    [Envar("SAMPLE_DB_CONNECTION_TIMEOUT")]
    public TimeSpan ConnectionTimeout { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary>API configuration. A class for the same reason as <see cref="DatabaseSettings"/>.</summary>
public class ApiSettings
{
    [Required]
    [Url]
    [Envar("SAMPLE_API_BASE_URL")]
    public string BaseUrl { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    [Envar("SAMPLE_API_KEY")]
    public string ApiKey { get; init; } = string.Empty;

    [Range(1, 3600)]
    [Envar("SAMPLE_API_TIMEOUT_SECONDS")]
    public int TimeoutSeconds { get; init; } = 30;

    [EmailAddress]
    [Envar("SAMPLE_API_SUPPORT_EMAIL")]
    public string? SupportEmail { get; init; }
}

public class FeatureFlags
{
    [Envar("SAMPLE_FEATURE_LOGGING")]
    public bool LoggingEnabled { get; init; }

    [Envar("SAMPLE_FEATURE_CACHING")]
    public bool CachingEnabled { get; init; }

    [Envar("SAMPLE_FEATURE_METRICS")]
    public bool MetricsEnabled { get; init; }
}

/// <summary>
/// Accepts a bare number of seconds for <see cref="TimeSpan"/> properties, and defers everything else to
/// the built-in binder.
/// </summary>
/// <remarks>
/// A custom binder receives complete environment values, including secrets, and may be invoked
/// concurrently. It must be deterministic and thread-safe, and must never log or retain what it is given.
/// This one is stateless and writes nothing.
/// </remarks>
public sealed class SecondsAwareBinder : IEnvarPropertyBinder
{
    private static readonly DefaultEnvarPropertyBinder Default = new();

    public object? Convert(string value, Type targetType, CultureInfo culture)
    {
        if (targetType == typeof(TimeSpan) && int.TryParse(value, NumberStyles.Integer, culture, out int seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return Default.Convert(value, targetType, culture);
    }
}

/// <summary>
/// Consumes the bound options the way an application service would. It prints only non-secret fields;
/// the password and the API key are used but never rendered.
/// </summary>
public sealed class ConfigurationReporter
{
    private readonly DatabaseSettings _database;
    private readonly ApiSettings _api;
    private readonly FeatureFlags _features;

    public ConfigurationReporter(
        IOptions<DatabaseSettings> database,
        IOptions<ApiSettings> api,
        IOptions<FeatureFlags> features)
    {
        _database = database.Value;
        _api = api.Value;
        _features = features.Value;
    }

    public void Report()
    {
        Console.WriteLine($"Database endpoint : {_database.Host}:{_database.Port.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"Database name     : {_database.DatabaseName}");
        Console.WriteLine($"Database user     : {_database.Username}");
        Console.WriteLine($"Database password : {DescribeSecret(_database.Password)}");
        Console.WriteLine($"TLS enabled       : {_database.SslEnabled}");
        Console.WriteLine($"Connection timeout: {_database.ConnectionTimeout}");
        Console.WriteLine($"API base URL      : {_api.BaseUrl}");
        Console.WriteLine($"API key           : {DescribeSecret(_api.ApiKey)}");
        Console.WriteLine($"API timeout       : {_api.TimeoutSeconds.ToString(CultureInfo.InvariantCulture)}s");
        Console.WriteLine($"Support email     : {_api.SupportEmail}");
        Console.WriteLine($"Logging enabled   : {_features.LoggingEnabled}");
        Console.WriteLine($"Caching enabled   : {_features.CachingEnabled}");
        Console.WriteLine($"Metrics enabled   : {_features.MetricsEnabled}");
    }

    /// <summary>
    /// Reports whether a secret was supplied without disclosing any part of it. Deliberately does not
    /// print a prefix, a suffix or the length, because each of those narrows a search.
    /// </summary>
    private static string DescribeSecret(string? secret)
    {
        return string.IsNullOrEmpty(secret) ? "<not set>" : "<set, redacted>";
    }
}
