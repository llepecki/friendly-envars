using FriendlyEnvars;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.ComponentModel.DataAnnotations;

// Sample demonstrating how to use FriendlyEnvars library
Console.WriteLine("=== FriendlyEnvars Library Sample ===\n");

// Set up sample environment variables
Console.WriteLine("Setting up sample environment variables...");
Environment.SetEnvironmentVariable("DB_HOST", "localhost");
Environment.SetEnvironmentVariable("DB_PORT", "5432");
Environment.SetEnvironmentVariable("DB_NAME", "myapp_db");
Environment.SetEnvironmentVariable("DB_USER", "admin");
Environment.SetEnvironmentVariable("DB_PASSWORD", "secret123");
Environment.SetEnvironmentVariable("DB_SSL_ENABLED", "true");
Environment.SetEnvironmentVariable("DB_CONNECTION_TIMEOUT", "45"); // Will be converted to TimeSpan via custom binder

Environment.SetEnvironmentVariable("API_BASE_URL", "https://api.example.com");
Environment.SetEnvironmentVariable("API_KEY", "abc123def456");
Environment.SetEnvironmentVariable("API_TIMEOUT_SECONDS", "60");
Environment.SetEnvironmentVariable("API_SUPPORT_EMAIL", "support@example.com");
Environment.SetEnvironmentVariable("API_RETRY_COUNT", "5");

Environment.SetEnvironmentVariable("FEATURE_LOGGING_ENABLED", "true");
Environment.SetEnvironmentVariable("FEATURE_CACHING_ENABLED", "true");
Environment.SetEnvironmentVariable("FEATURE_METRICS_ENABLED", "false");
Environment.SetEnvironmentVariable("FEATURE_DEBUG_MODE", "true");

// Example 1: Basic usage with default settings
Console.WriteLine("=== Example 1: Basic Usage ===");
var services1 = new ServiceCollection();
services1.AddOptions<DatabaseConfig>()
    .BindFromEnvars()
    .ValidateDataAnnotations()
    .ValidateOnStart();

var provider1 = services1.BuildServiceProvider();
var dbConfig = provider1.GetRequiredService<IOptions<DatabaseConfig>>().Value;

Console.WriteLine($"Database Host: {dbConfig.Host}");
Console.WriteLine($"Database Port: {dbConfig.Port}");
Console.WriteLine($"Database Name: {dbConfig.DatabaseName}");
Console.WriteLine($"SSL Enabled: {dbConfig.SslEnabled}");
Console.WriteLine($"Connection Timeout: {dbConfig.ConnectionTimeout}");
Console.WriteLine();

// Example 2: Using custom property binder
Console.WriteLine("=== Example 2: Custom Property Binder ===");
var services2 = new ServiceCollection();
services2.AddOptions<DatabaseConfig>()
    .BindFromEnvars(settings =>
    {
        settings.UseCustomEnvarPropertyBinder(new CustomEnvarPropertyBinder());
    })
    .ValidateDataAnnotations()
    .ValidateOnStart();

var provider2 = services2.BuildServiceProvider();
var dbConfigCustom = provider2.GetRequiredService<IOptions<DatabaseConfig>>().Value;

Console.WriteLine($"Connection Timeout (with custom binder): {dbConfigCustom.ConnectionTimeout}");
Console.WriteLine();

// Example 3: Multiple configuration classes
Console.WriteLine("=== Example 3: Multiple Configuration Classes ===");
var services3 = new ServiceCollection();

// Register multiple configuration classes
services3.AddOptions<DatabaseConfig>()
    .BindFromEnvars()
    .ValidateDataAnnotations()
    .ValidateOnStart();

services3.AddOptions<ApiConfig>()
    .BindFromEnvars()
    .ValidateDataAnnotations()
    .ValidateOnStart();

services3.AddOptions<FeatureFlags>()
    .BindFromEnvars();

var provider3 = services3.BuildServiceProvider();

var apiConfig = provider3.GetRequiredService<IOptions<ApiConfig>>().Value;
var featureFlags = provider3.GetRequiredService<IOptions<FeatureFlags>>().Value;

Console.WriteLine($"API Base URL: {apiConfig.BaseUrl}");
Console.WriteLine($"API Key: {apiConfig.ApiKey[..6]}***");
Console.WriteLine($"API Timeout: {apiConfig.TimeoutSeconds}s");
Console.WriteLine($"Support Email: {apiConfig.SupportEmail}");
Console.WriteLine();

Console.WriteLine("Feature Flags:");
Console.WriteLine($"  Logging: {featureFlags.LoggingEnabled}");
Console.WriteLine($"  Caching: {featureFlags.CachingEnabled}");
Console.WriteLine($"  Metrics: {featureFlags.MetricsEnabled}");
Console.WriteLine($"  Debug Mode: {featureFlags.DebugMode}");
Console.WriteLine();

// Example 4: Named options
Console.WriteLine("=== Example 4: Named Options ===");
Environment.SetEnvironmentVariable("CACHE_DB_HOST", "cache.example.com");
Environment.SetEnvironmentVariable("CACHE_DB_PORT", "6379");
Environment.SetEnvironmentVariable("CACHE_DB_NAME", "cache_db");
Environment.SetEnvironmentVariable("CACHE_DB_USER", "cache_user");

var services4 = new ServiceCollection();
services4.AddOptions<CacheConfig>("cache")
    .BindFromEnvars()
    .ValidateDataAnnotations()
    .ValidateOnStart();

var provider4 = services4.BuildServiceProvider();
var cacheConfig = provider4.GetRequiredService<IOptionsFactory<CacheConfig>>().Create("cache");

Console.WriteLine($"Cache Host: {cacheConfig.Host}");
Console.WriteLine($"Cache Port: {cacheConfig.Port}");
Console.WriteLine();

// Example 5: Error handling
Console.WriteLine("=== Example 5: Error Handling ===");
try
{
    Environment.SetEnvironmentVariable("INVALID_PORT", "not_a_number");

    var services5 = new ServiceCollection();
    services5.AddOptions<InvalidConfig>()
        .BindFromEnvars()
        .ValidateDataAnnotations()
        .ValidateOnStart();

    var provider5 = services5.BuildServiceProvider();
    var invalidConfig = provider5.GetRequiredService<IOptions<InvalidConfig>>().Value;
}
catch (EnvarsException ex)
{
    Console.WriteLine($"Caught EnvarsException: {ex.Message}");
}
catch (OptionsValidationException ex)
{
    Console.WriteLine($"Caught OptionsValidationException: {ex.Message}");
}
Console.WriteLine();

// Example 6: Host Builder Integration
Console.WriteLine("=== Example 6: Host Builder Integration ===");
var hostBuilder = Host.CreateDefaultBuilder()
    .ConfigureServices(services =>
    {
        services.AddOptions<DatabaseConfig>()
            .BindFromEnvars()
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<ApiConfig>()
            .BindFromEnvars()
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Add your application services here
        services.AddSingleton<MyService>();
    });

var host = hostBuilder.Build();
var myService = host.Services.GetRequiredService<MyService>();
myService.DoWork();

Console.WriteLine("\n=== Sample completed successfully! ===");

// Clean up environment variables
Environment.SetEnvironmentVariable("DB_HOST", null);
Environment.SetEnvironmentVariable("DB_PORT", null);
Environment.SetEnvironmentVariable("DB_NAME", null);
Environment.SetEnvironmentVariable("DB_USER", null);
Environment.SetEnvironmentVariable("DB_PASSWORD", null);
Environment.SetEnvironmentVariable("DB_SSL_ENABLED", null);
Environment.SetEnvironmentVariable("DB_CONNECTION_TIMEOUT", null);
Environment.SetEnvironmentVariable("API_BASE_URL", null);
Environment.SetEnvironmentVariable("API_KEY", null);
Environment.SetEnvironmentVariable("API_TIMEOUT_SECONDS", null);
Environment.SetEnvironmentVariable("API_SUPPORT_EMAIL", null);
Environment.SetEnvironmentVariable("API_RETRY_COUNT", null);
Environment.SetEnvironmentVariable("FEATURE_LOGGING_ENABLED", null);
Environment.SetEnvironmentVariable("FEATURE_CACHING_ENABLED", null);
Environment.SetEnvironmentVariable("FEATURE_METRICS_ENABLED", null);
Environment.SetEnvironmentVariable("FEATURE_DEBUG_MODE", null);
Environment.SetEnvironmentVariable("CACHE_DB_HOST", null);
Environment.SetEnvironmentVariable("CACHE_DB_PORT", null);
Environment.SetEnvironmentVariable("CACHE_DB_NAME", null);
Environment.SetEnvironmentVariable("CACHE_DB_USER", null);
Environment.SetEnvironmentVariable("INVALID_PORT", null);

// 1. Basic Configuration Class with Environment Variables
public class DatabaseConfig
{
    [Required]
    [Envar("DB_HOST")]
    public string Host { get; set; } = string.Empty;

    [Required]
    [Range(1, 65535)]
    [Envar("DB_PORT")]
    public int Port { get; set; }

    [Required]
    [Envar("DB_NAME")]
    public string DatabaseName { get; set; } = string.Empty;

    [Required]
    [Envar("DB_USER")]
    public string Username { get; set; } = string.Empty;

    [Envar("DB_PASSWORD")]
    public string? Password { get; set; }

    [Envar("DB_SSL_ENABLED")]
    public bool SslEnabled { get; set; } = true;

    [Envar("DB_CONNECTION_TIMEOUT")]
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

// 2. API Configuration with URL and Email validation
public class ApiConfig
{
    [Required]
    [Url]
    [Envar("API_BASE_URL")]
    public string BaseUrl { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [Envar("API_KEY")]
    public string ApiKey { get; set; } = string.Empty;

    [Range(1, 3600)]
    [Envar("API_TIMEOUT_SECONDS")]
    public int TimeoutSeconds { get; set; } = 30;

    [EmailAddress]
    [Envar("API_SUPPORT_EMAIL")]
    public string? SupportEmail { get; set; }

    [Envar("API_RETRY_COUNT")]
    public int RetryCount { get; set; } = 3;
}

// 3. Feature Flags Configuration
public class FeatureFlags
{
    [Envar("FEATURE_LOGGING_ENABLED")]
    public bool LoggingEnabled { get; set; } = true;

    [Envar("FEATURE_CACHING_ENABLED")]
    public bool CachingEnabled { get; set; } = false;

    [Envar("FEATURE_METRICS_ENABLED")]
    public bool MetricsEnabled { get; set; } = true;

    [Envar("FEATURE_DEBUG_MODE")]
    public bool DebugMode { get; set; } = false;
}

// 4. Custom Property Binder Example
public class CustomEnvarPropertyBinder : IEnvarPropertyBinder
{
    private readonly EnvarPropertyBinder _defaultBinder = new();

    public object? Convert(string value, Type targetType)
    {
        // Custom logic for specific types
        if (targetType == typeof(TimeSpan))
        {
            // Support both seconds (number) and TimeSpan format
            if (int.TryParse(value, out int seconds))
            {
                return TimeSpan.FromSeconds(seconds);
            }
        }

        // Fall back to default behavior
        return _defaultBinder.Convert(value, targetType);
    }
}

// 5. Cache Configuration for Named Options Example
public class CacheConfig
{
    [Required]
    [Envar("CACHE_DB_HOST")]
    public string Host { get; set; } = string.Empty;

    [Required]
    [Range(1, 65535)]
    [Envar("CACHE_DB_PORT")]
    public int Port { get; set; }

    [Required]
    [Envar("CACHE_DB_NAME")]
    public string DatabaseName { get; set; } = string.Empty;

    [Required]
    [Envar("CACHE_DB_USER")]
    public string Username { get; set; } = string.Empty;
}

// 6. Invalid Configuration for Error Handling Example
public class InvalidConfig
{
    [Required]
    [Envar("INVALID_PORT")]
    public int Port { get; set; }
}

// 7. Sample Service for Host Builder Example
public class MyService
{
    private readonly DatabaseConfig _dbConfig;
    private readonly ApiConfig _apiConfig;

    public MyService(IOptions<DatabaseConfig> dbConfig, IOptions<ApiConfig> apiConfig)
    {
        _dbConfig = dbConfig.Value;
        _apiConfig = apiConfig.Value;
    }

    public void DoWork()
    {
        Console.WriteLine($"Service working with DB: {_dbConfig.Host}:{_dbConfig.Port}");
        Console.WriteLine($"Service working with API: {_apiConfig.BaseUrl}");
    }
}
