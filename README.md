# FriendlyEnvars

[![CI](https://github.com/llepecki/friendly-envars/actions/workflows/ci.yml/badge.svg)](https://github.com/llepecki/friendly-envars/actions/workflows/ci.yml)

Simple, type-safe environment variable configuration for .NET

---

## 👀 Overview

Do you need to configure your .NET app *purely* via environment variables?

**FriendlyEnvars** lets you bind them directly to strongly typed configuration classes.

- Clean, explicit configuration mapping using the `[Envar]` attribute.
- Automatic type conversion, validation, and integration with the `IOptions<T>` pattern.
- Environment variables are bound once, at startup.

**Ideal for:** cloud-native apps, containerized deployments, microservices, or anywhere configuration comes from the environment.

---

## 📝 Why FriendlyEnvars?

- **Type safety**: Eliminates runtime configuration errors by mapping environment variables directly to typed properties.
- **Built-in validation**: Leverages data annotation attributes like `[Required]`, `[Range]`, etc. automatically.
- **No boilerplate**: No need to write manual parsing, error handling, or default value logic.
- **Works with `IOptions`**: Smooth experience for modern .NET dependency injection patterns.
- **Explicit & Discoverable:** Your configuration surface is crystal clear in the code.

---

## 🚀 Quick Start

Everything in this section is executable exactly as written. `eng/smoke-consumer.sh` creates empty
console projects for both supported targets, runs the commands below, copies the two programs below into
them and runs those too, so a Quick Start that stops working stops the build.

Two things are worth knowing before you start. Conversion from environment text to your property types
is automatic - you do not write any parsing. Data-annotation validation is opt-in: it comes from the
companion `Microsoft.Extensions.Options.DataAnnotations` package and runs only where you call
`ValidateDataAnnotations()`.

### 1. Create a project and add the packages

`Microsoft.Extensions.Hosting` brings in the dependency-injection and options assemblies, so there is no
need to add `Microsoft.Extensions.DependencyInjection` or `Microsoft.Extensions.Options` yourself.

On .NET 8:

<!-- smoke-consumer: packages net8.0 -->
```bash
dotnet new console --framework net8.0 --output quickstart
cd quickstart
dotnet add package FriendlyEnvars --version 2.0.0
dotnet add package Microsoft.Extensions.Hosting --version 8.0.1
dotnet add package Microsoft.Extensions.Options.DataAnnotations --version 8.0.0
```

On .NET 10:

<!-- smoke-consumer: packages net10.0 -->
```bash
dotnet new console --framework net10.0 --output quickstart
cd quickstart
dotnet add package FriendlyEnvars --version 2.0.0
dotnet add package Microsoft.Extensions.Hosting --version 10.0.11
dotnet add package Microsoft.Extensions.Options.DataAnnotations --version 10.0.11
```

### 2. Set the environment variables

<!-- smoke-consumer: environment valid -->
```bash
export DB_HOST=db.internal
export DB_PORT=5432
export DB_SSL_ENABLED=true
export DB_CONNECTION_TIMEOUT=00:00:45
```

### 3. Write the program

Replace the generated `Program.cs` with this. It is a complete file: the `using` directives, the options
type, the registration, and a host that starts, resolves the options and stops.

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

`dotnet run` prints:

<!-- smoke-consumer: output valid -->
```text
host=db.internal
port=5432
ssl=True
timeout=00:00:45
```

### 4. What a value that fails validation does

This is a separate, self-contained program. It sets an out-of-range port itself so you can run it
without changing your environment, and it deliberately does not catch the failure.

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
    // The default is deliberately in range, so the start-up failure below can only come from the
    // captured environment value.
    [Envar("DB_PORT")]
    [Range(1, 65535)]
    public int Port { get; init; } = 5432;
}

public static class Program
{
    public static async Task Main()
    {
        // 70000 is outside the Range above. Values are captured once, while BindEnvars runs, so this
        // has to be set before the host is built.
        Environment.SetEnvironmentVariable("DB_PORT", "70000");

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.Services
            .AddOptions<PortSettings>()
            .BindEnvars()
            .ValidateDataAnnotations()
            .ValidateOnStart();

        using IHost host = builder.Build();

        // ValidateOnStart runs the annotations here, so the application fails to start rather than
        // running with a port it cannot use.
        await host.StartAsync(CancellationToken.None);
    }
}
```

It exits with a nonzero code. This line of its output is the failure - the full output also carries
the stack trace and the host's own "Hosting failed to start" logging:

<!-- smoke-consumer: output invalid -->
```text
Unhandled exception. Microsoft.Extensions.Options.OptionsValidationException: DataAnnotation validation failed for 'PortSettings' members: 'Port' with the error: 'The field Port must be between 1 and 65535.'.
```

Without the `ValidateDataAnnotations()` call the same program starts normally and uses the out-of-range
value, which is what "opt-in" means here.

### 💡 Features

**Supported Types:**

- `string`, `char`, `bool`
- Numeric types: `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `decimal`
- `Guid`, `Uri`, `TimeSpan`, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`
- `Enum` (case-insensitive, including `[Flags]` enums)
- Nullable versions of all above types
- Any type with a `TypeConverter`

**Additional Features:**

- Automatic conversion using invariant culture (by default) or custom culture.
- Custom parsing recipes via `IEnvarPropertyBinder` interface.
- Validation using familiar `DataAnnotations` attributes.

### ⚙️ Advanced Usage

#### Parsing with a Specific Culture

By default, conversions use `CultureInfo.InvariantCulture` for predictable parsing. To handle locale-specific formats:

```csharp
using System.Globalization;

services.AddOptions<DatabaseSettings>()
    .BindEnvars(settings => {
        settings.UseCulture(CultureInfo.GetCultureInfo("en-US"));
    });
```

#### Custom Type Conversion

For complex types, implement `IEnvarPropertyBinder` to control parsing:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;

public record ConnectionString
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; }
    public string User { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public class CustomEnvarPropertyBinder : IEnvarPropertyBinder
{
    private readonly DefaultEnvarPropertyBinder _defaultBinder = new();

    public object? Convert(string value, Type targetType, CultureInfo culture)
    {
        if (targetType == typeof(ConnectionString))
        {
            return ParseConnectionString(value);
        }

        return _defaultBinder.Convert(value, targetType, culture);
    }

    private static ConnectionString ParseConnectionString(string connectionString)
    {
        var pairs = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var values = new Dictionary<string, string>();

        foreach (var pair in pairs)
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2)
            {
                values[parts[0].Trim()] = parts[1].Trim();
            }
        }

        return new ConnectionString
        {
            Host = values.GetValueOrDefault("Host", "localhost"),
            Port = int.Parse(values.GetValueOrDefault("Port", "5432")),
            User = values.GetValueOrDefault("User", ""),
            Password = values.GetValueOrDefault("Password", "")
        };
    }
}
```

Usage with environment variable `CONNECTION_STRING=Host=localhost;Port=5432;User=Joe;Password=Joe12`:

```csharp
public record DatabaseSettings
{
    [Envar("CONNECTION_STRING")]
    public ConnectionString Connection { get; init; } = new();
}
```

Then, configure the binder:

```csharp
services.AddOptions<DatabaseSettings>()
    .BindEnvars(settings =>
    {
        settings.UseCustomEnvarPropertyBinder(new CustomEnvarPropertyBinder());
    });
```

##### The converter/binder trust boundary

A binder is **trusted code that receives secrets.** FriendlyEnvars hands it the complete environment
value verbatim — routinely a password, connection string or API key — and does not sandbox, redact or
inspect it. The same is true of the `TypeConverter` fallback in `DefaultEnvarPropertyBinder`, which is
reached for any type without a built-in rule and may resolve to a converter declared on the target type
or registered anywhere else in the process.

An implementation must:

- **be deterministic** — the same input must always produce an equivalent result;
- **be thread-safe** — one instance is shared by every options instance the registration produces, and
  FriendlyEnvars calls it concurrently without serialising, so resolving options from several threads at
  once puts several threads inside `Convert` simultaneously. Keep it stateless, or guard whatever state
  it holds. The library never copies, resets or locks around a binder;
- **never log, print, cache or otherwise retain** the value it is given.

Exceptions thrown by a binder are sanitised — only the exception's type name survives, never its message
— so a failure cannot leak the value. The one exception is `OperationCanceledException`, which is
propagated unchanged as your own control flow; do not put a value in a cancellation message.

#### Named Options

The same options type can be registered under several names. **Each `BindEnvars()` call takes its own
snapshot of the environment**, so two names can hold different values, and neither changes afterwards:

```csharp
public class RegionSettings
{
    [Envar("REGION_ENDPOINT")]
    public string Endpoint { get; init; } = string.Empty;
}
```

```csharp
var services = new ServiceCollection();

Environment.SetEnvironmentVariable("REGION_ENDPOINT", "https://eu.example.com");
services.AddOptions<RegionSettings>("eu").BindEnvars();

Environment.SetEnvironmentVariable("REGION_ENDPOINT", "https://us.example.com");
services.AddOptions<RegionSettings>("us").BindEnvars();

using var provider = services.BuildServiceProvider();
```

Read them back the ordinary way — nothing about FriendlyEnvars changes how named options are accessed:

```csharp
var factory = provider.GetRequiredService<IOptionsFactory<RegionSettings>>();
Console.WriteLine(factory.Create("eu").Endpoint);   // https://eu.example.com

var monitor = provider.GetRequiredService<IOptionsMonitor<RegionSettings>>();
Console.WriteLine(monitor.Get("us").Endpoint);      // https://us.example.com

using var scope = provider.CreateScope();
var snapshot = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<RegionSettings>>();
Console.WriteLine(snapshot.Get("eu").Endpoint);     // https://eu.example.com
```

Three rules worth knowing:

- **Capture time is per registration.** A name holds whatever the environment said when *its own*
  `BindEnvars()` ran, not what it says later.
- **The same name cannot be registered twice.** A second `BindEnvars()` for the same options type and
  name throws `InvalidOperationException`, because the two snapshots would otherwise silently overwrite
  one another in registration order. Different names, and different types, are unaffected.
- **Precedence is per name.** The last registration for a given name wins, exactly as described below.

A name that was never registered simply gets the type's own defaults.

#### Configuration Precedence

`BindEnvars()` registers an ordinary `IConfigureOptions<T>`, so it composes with every other options
source by the normal rule: **whichever registration runs last wins.** FriendlyEnvars does not force
environment values to take priority, and does not register a `PostConfigure` step.

`Configure` assigns to a property after the instance is constructed, so the properties it writes need a
`set` accessor rather than `init`:

```csharp
public class ServerSettings
{
    [Envar("SERVER_HOST")]
    public string Host { get; set; } = "localhost";
}
```

The environment value wins here, because `BindEnvars()` is registered last:

```csharp
var services = new ServiceCollection();

services.AddOptions<ServerSettings>()
    .Configure(options => options.Host = "from-code")
    .BindEnvars();
```

The code value wins here, because `Configure` is registered last:

```csharp
var services = new ServiceCollection();

services.AddOptions<ServerSettings>()
    .BindEnvars()
    .Configure(options => options.Host = "from-code");
```

Each block is a separate registration. Calling `BindEnvars()` twice for the same options type and name
in one container is rejected, so the two examples cannot be combined into one.

A variable that is not set is skipped rather than bound as null, so it never clears a value that an
earlier `Configure` established.

#### Working with `IOptionsSnapshot` and `IOptionsMonitor`

`IOptions<T>`, `IOptionsSnapshot<T>`, `IOptionsMonitor<T>` and `IOptionsFactory<T>` all resolve normally.
FriendlyEnvars does not replace, block or otherwise interfere with any of them.

Every value is captured once, while `BindEnvars()` runs, and every options instance is built from that
snapshot, so all four abstractions observe the same values. Changing a variable afterwards changes
nothing — `IOptionsSnapshot<T>` and `IOptionsMonitor<T>` do not re-read the environment.

### ⚠️ Breaking Changes

#### What changed in 2.0

- **Exceptions are sealed and structured.** `EnvarsException` is sealed and carries `FailureKind` plus
  the environment-variable name, options type and name, property, target type, culture, binder type and
  the cause's type name. A library-generated failure never contains the value, the cause's message, or
  an inner exception. `OperationCanceledException` propagates unchanged.
- **The options-blocking configuration was removed** with no replacement. `IOptions<T>`,
  `IOptionsSnapshot<T>`, `IOptionsMonitor<T>` and `IOptionsFactory<T>` all resolve normally.
- **Property shapes and variable names are validated eagerly,** while `BindEnvars()` runs, rather than
  lazily at options creation — so a bad options type fails at startup even when the variable is absent.
- **Registering the same options type and name twice throws** `InvalidOperationException`.
- **Values are captured once,** while `BindEnvars()` runs. Changing a variable afterwards affects
  nothing.
- **Enum text follows an explicit grammar** instead of `Enum.Parse`. Three differences are deliberate:
  a non-flags enum rejects `"-1"` even when `All = -1` is declared; a non-flags enum rejects
  `"Read,Write"` even when `ReadWrite = 3` is declared; and a flags enum rejects negative numeric text
  while still accepting the declared member name. In each case the value stays reachable by name.
- **`BindEnvars<T>` declares `[DynamicallyAccessedMembers(PublicProperties)]`** on `T`, so trimmed apps
  keep the properties reflection needs.
- **The configured culture is cloned and frozen** when captured, so a binder that mutates it now throws
  instead of silently changing later parsing.

#### Empty Environment Variables (since v1.1.0)

Before v1.1.0, environment variables set to an empty string (`""`) were treated the same as unset variables — the property would retain its default value. Since v1.1.0, empty strings are passed to the binder. This means:

- `string` properties will be set to `""` instead of keeping their default.
- Non-string properties (e.g., `int`, `bool`) throw `EnvarsException` with `FailureKind.Conversion` and `CauseType` `System.FormatException` if the environment variable is empty. The cause itself is not retained, so `InnerException` is `null`.

If you relied on the old behavior of ignoring empty values, either unset the variable entirely or use a custom `IEnvarPropertyBinder` to handle empty strings.

### ⚠️ Limitations

- No runtime refresh: each value is captured once, when `BindEnvars()` runs, and never re-read.
- `IOptionsSnapshot` and `IOptionsMonitor` resolve normally, but they behave as read-only views of the startup values.

---

With FriendlyEnvars, configuration is easy, explicit, and safe.
