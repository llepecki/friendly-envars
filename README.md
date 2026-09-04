# FriendlyEnvars

[![CI](https://github.com/llepecki/friendly-envars/actions/workflows/ci.yml/badge.svg)](https://github.com/llepecki/friendly-envars/actions/workflows/ci.yml)

FriendlyEnvars binds environment variables to typed .NET options. Add `[Envar]` to each property you want to bind.

## Quick start

FriendlyEnvars targets .NET 8. The sample below runs on .NET 8 and .NET 10.

### 1. Create the project

Choose one target. `Microsoft.Extensions.Hosting` supplies dependency injection and the Options APIs.

.NET 8:

<!-- smoke-consumer: packages net8.0 -->
```bash
dotnet new console --framework net8.0 --output quickstart
cd quickstart
dotnet add package FriendlyEnvars --version 2.0.0
dotnet add package Microsoft.Extensions.Hosting --version 8.0.1
dotnet add package Microsoft.Extensions.Options.DataAnnotations --version 8.0.0
```

.NET 10:

<!-- smoke-consumer: packages net10.0 -->
```bash
dotnet new console --framework net10.0 --output quickstart
cd quickstart
dotnet add package FriendlyEnvars --version 2.0.0
dotnet add package Microsoft.Extensions.Hosting --version 10.0.11
dotnet add package Microsoft.Extensions.Options.DataAnnotations --version 10.0.11
```

### 2. Set the variables

<!-- smoke-consumer: environment valid -->
```bash
export DB_HOST=db.internal
export DB_PORT=5432
export DB_SSL_ENABLED=true
export DB_CONNECTION_TIMEOUT=00:00:45
```

### 3. Replace `Program.cs`

<!-- smoke-consumer: program valid -->
```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using FriendlyEnvars;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public sealed record DatabaseSettings
{
    [Envar("DB_HOST")]
    [Required]
    public string Host { get; init; } = string.Empty;

    [Envar("DB_PORT")]
    [Range(1, 65535)]
    public int Port { get; init; }

    [Envar("DB_SSL_ENABLED")]
    public bool SslEnabled { get; init; } = true;

    [Envar("DB_CONNECTION_TIMEOUT")]
    public TimeSpan ConnectionTimeout { get; init; } = TimeSpan.FromSeconds(30);
}

public static class Program
{
    public static async Task Main()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.Services
            .AddOptions<DatabaseSettings>()
            .BindEnvars()
            .ValidateDataAnnotations()
            .ValidateOnStart();

        using IHost host = builder.Build();
        await host.StartAsync(CancellationToken.None);

        DatabaseSettings settings = host.Services.GetRequiredService<IOptions<DatabaseSettings>>().Value;

        Console.WriteLine($"host={settings.Host}");
        Console.WriteLine($"port={settings.Port}");
        Console.WriteLine($"ssl={settings.SslEnabled}");
        Console.WriteLine($"timeout={settings.ConnectionTimeout}");

        await host.StopAsync(CancellationToken.None);
    }
}
```

Run it:

```bash
dotnet run
```

<!-- smoke-consumer: output valid -->
```text
host=db.internal
port=5432
ssl=True
timeout=00:00:45
```

Conversion is automatic. Data-annotation validation is optional and runs only when you call `ValidateDataAnnotations()`.

### Validation failure example

This complete program sets an invalid value. `ValidateOnStart()` makes host startup fail.

<!-- smoke-consumer: program invalid -->
```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using FriendlyEnvars;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public sealed record PortSettings
{
    [Envar("DB_PORT")]
    [Range(1, 65535)]
    public int Port { get; init; } = 5432;
}

public static class Program
{
    public static async Task Main()
    {
        Environment.SetEnvironmentVariable("DB_PORT", "70000");

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.Services
            .AddOptions<PortSettings>()
            .BindEnvars()
            .ValidateDataAnnotations()
            .ValidateOnStart();

        using IHost host = builder.Build();
        await host.StartAsync(CancellationToken.None);
    }
}
```

The process exits with a nonzero code and reports:

<!-- smoke-consumer: output invalid -->
```text
OptionsValidationException
```

## Behavior

- Values are captured once when `BindEnvars()` is called. Later environment changes have no effect.
- An unset variable leaves the property's existing value unchanged.
- An empty string is a value. It is passed to the binder.
- Properties must be public, non-indexed, instance properties with a public `set` or `init` accessor.
- A second `BindEnvars()` call for the same options type and name throws `InvalidOperationException`.
- Registrations run in order. The last source that sets a property wins.

### Supported types

The default binder supports:

- `string`, `char`, and `bool`
- All integer and floating-point types, plus `decimal`
- `Guid`, `Uri`, `TimeSpan`, `DateTime`, `DateTimeOffset`, `DateOnly`, and `TimeOnly`
- Enums, including `[Flags]` enums
- Nullable forms of these types
- Types with a `TypeConverter`

Conversions use `InvariantCulture` unless you select another culture:

```csharp
using System.Globalization;

services.AddOptions<DatabaseSettings>()
    .BindEnvars(settings =>
        settings.UseCulture(CultureInfo.GetCultureInfo("pl-PL")));
```

## Named options

Each named `BindEnvars()` call captures its own values:

```csharp
public sealed class RegionSettings
{
    [Envar("REGION_ENDPOINT")]
    public string Endpoint { get; init; } = string.Empty;
}

var services = new ServiceCollection();

Environment.SetEnvironmentVariable("REGION_ENDPOINT", "https://eu.example.com");
services.AddOptions<RegionSettings>("eu").BindEnvars();

Environment.SetEnvironmentVariable("REGION_ENDPOINT", "https://us.example.com");
services.AddOptions<RegionSettings>("us").BindEnvars();

using ServiceProvider provider = services.BuildServiceProvider();

var factory = provider.GetRequiredService<IOptionsFactory<RegionSettings>>();
var monitor = provider.GetRequiredService<IOptionsMonitor<RegionSettings>>();

using IServiceScope scope = provider.CreateScope();
var snapshot = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<RegionSettings>>();

Console.WriteLine(factory.Create("eu").Endpoint);
Console.WriteLine(monitor.Get("us").Endpoint);
Console.WriteLine(snapshot.Get("eu").Endpoint);
```

An unknown name gets the type's default values.

## Precedence

`BindEnvars()` uses the normal Options registration order. Environment values win here:

```csharp
services.AddOptions<ServerSettings>()
    .Configure(options => options.Host = "from-code")
    .BindEnvars();
```

Code wins here:

```csharp
services.AddOptions<ServerSettings>()
    .BindEnvars()
    .Configure(options => options.Host = "from-code");
```

An unset variable never overwrites an earlier value.

## Custom binders

Use `IEnvarPropertyBinder` when the default conversions are not enough. Register it with `UseCustomEnvarPropertyBinder()`.

A custom binder or `TypeConverter` receives the full environment value. Treat that code as trusted:

- Keep it deterministic and thread-safe. One binder instance may serve concurrent options creation.
- Do not log, print, cache, or retain input values.
- Do not include a value in an `OperationCanceledException` message. Cancellation is propagated unchanged.

Other binder exceptions are sanitized. FriendlyEnvars keeps the exception type, but not its message or object.

## Errors

FriendlyEnvars throws `EnvarsException` for invalid properties, read failures, conversion failures, and assignment failures. Inspect `FailureKind` and the structured metadata instead of parsing `Message`.

Library-generated exceptions never include the environment value or an inner exception. `OperationCanceledException` is propagated unchanged.

## `IOptions` behavior

`IOptions<T>`, `IOptionsSnapshot<T>`, `IOptionsMonitor<T>`, and `IOptionsFactory<T>` work normally. All use the values captured by `BindEnvars()`; snapshots and monitors do not re-read the environment.

## Version 2.0 changes

- `EnvarsException` is sealed and includes structured failure metadata.
- The options-blocking APIs were removed.
- Property shapes and environment names are validated by `BindEnvars()`.
- Duplicate options type/name registrations are rejected.
- Values are captured by `BindEnvars()`, not during options creation.
- Enum parsing rejects invalid and ambiguous text.
- `BindEnvars<T>` preserves public properties in trimmed applications.
- The selected culture is cloned and made read-only.

Empty strings have been passed to the binder since version 1.1.0. Unset the variable or use a custom binder if you need different behavior.
