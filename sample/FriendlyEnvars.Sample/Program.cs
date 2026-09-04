using FriendlyEnvars;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

// Exit 0 demonstrates valid binding. --invalid-validation exits 2 after startup validation fails.
// eng/run-sample.sh checks both paths and verifies that no secret reaches the output.

bool invalidValidation = args.Contains("--invalid-validation", StringComparer.Ordinal);

SampleEnvironment.Apply(invalidValidation);

// Disable host defaults to keep output and configuration sources deterministic.
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

// Each named registration captures the current value.
Environment.SetEnvironmentVariable("SAMPLE_REGION_ENDPOINT", "https://eu.example.com");

builder.Services
    .AddOptions<RegionSettings>("eu")
    .BindEnvars();

Environment.SetEnvironmentVariable("SAMPLE_REGION_ENDPOINT", "https://us.example.com");

builder.Services
    .AddOptions<RegionSettings>("us")
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
        Console.WriteLine(SampleOutput.ValidationFailed);
        return 2;
    }

    Console.Error.WriteLine("Expected OptionsValidationException during StartAsync, but startup succeeded.");
    return 1;
}

await host.StartAsync();

host.Services.GetRequiredService<ConfigurationReporter>().Report();

await host.StopAsync();

Console.WriteLine(SampleOutput.Success);
return 0;

internal static class SampleOutput
{
    public const string Success = "Sample completed successfully!";
    public const string ValidationFailed = "Validation failed during StartAsync as expected.";
}

internal static class SampleEnvironment
{
    // The output gate checks that no six-character fragment of these fixtures is printed.
    private const string FakePassword = "QZXJKVWYPLMB0000";

    private const string FakeApiKey = "MBLPYWVKJXZQ1111";

    public static void Apply(bool invalidValidation)
    {
        SetIfAbsent("SAMPLE_DB_HOST", "db.example.com");
        SetIfAbsent("SAMPLE_DB_NAME", "sample_db");
        SetIfAbsent("SAMPLE_DB_USER", "sample_user");
        SetIfAbsent("SAMPLE_DB_PASSWORD", FakePassword);
        SetIfAbsent("SAMPLE_DB_SSL_ENABLED", "true");

        SetIfAbsent("SAMPLE_DB_CONNECTION_TIMEOUT", "45");

        SetIfAbsent("SAMPLE_API_BASE_URL", "https://api.example.com");
        SetIfAbsent("SAMPLE_API_KEY", FakeApiKey);
        SetIfAbsent("SAMPLE_API_TIMEOUT_SECONDS", "60");
        SetIfAbsent("SAMPLE_API_SUPPORT_EMAIL", "support@example.com");

        SetIfAbsent("SAMPLE_FEATURE_LOGGING", "true");
        SetIfAbsent("SAMPLE_FEATURE_CACHING", "true");
        SetIfAbsent("SAMPLE_FEATURE_METRICS", "false");

        // The invalid value converts to int, then fails data-annotation validation.
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
/// Database options. A class avoids a record-generated <see cref="object.ToString"/> that could expose secrets.
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

/// <summary>API options that avoid record-generated secret output.</summary>
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

/// <summary>Options used to demonstrate named registrations.</summary>
public class RegionSettings
{
    [Envar("SAMPLE_REGION_ENDPOINT")]
    public string Endpoint { get; init; } = string.Empty;
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
/// Parses a <see cref="TimeSpan"/> from seconds and delegates other types.
/// </summary>
/// <remarks>
/// This stateless binder is safe for concurrent calls and does not retain or print values.
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
/// Reads the options and prints only non-secret values.
/// </summary>
public sealed class ConfigurationReporter
{
    private readonly DatabaseSettings _database;
    private readonly ApiSettings _api;
    private readonly FeatureFlags _features;
    private readonly IOptionsFactory<RegionSettings> _regionFactory;
    private readonly IOptionsMonitor<RegionSettings> _regionMonitor;
    private readonly IServiceScopeFactory _scopeFactory;

    public ConfigurationReporter(
        IOptions<DatabaseSettings> database,
        IOptions<ApiSettings> api,
        IOptions<FeatureFlags> features,
        IOptionsFactory<RegionSettings> regionFactory,
        IOptionsMonitor<RegionSettings> regionMonitor,
        IServiceScopeFactory scopeFactory)
    {
        _database = database.Value;
        _api = api.Value;
        _features = features.Value;
        _regionFactory = regionFactory;
        _regionMonitor = regionMonitor;
        _scopeFactory = scopeFactory;
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

        using var scope = _scopeFactory.CreateScope();
        var snapshot = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<RegionSettings>>();

        foreach (string name in new[] { "eu", "us" })
        {
            Console.WriteLine(
                $"Region '{name}'      : factory={_regionFactory.Create(name).Endpoint} " +
                $"monitor={_regionMonitor.Get(name).Endpoint} snapshot={snapshot.Get(name).Endpoint}");
        }
    }

    private static string DescribeSecret(string? secret)
    {
        return string.IsNullOrEmpty(secret) ? "<not set>" : "<set, redacted>";
    }
}
