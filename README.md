# FriendlyEnvars

<img src="resources/icon-1.png" alt="" width="60" height="60" align="left" style="margin-right: 16px;" title="FriendlyEnvars Logo">

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

### 1. Define Your Configuration Class

Annotate properties you want loaded from environment variables. Properties must have a setter (`set` or `init`):

```csharp
public record DatabaseSettings
{
    [Envar("DB_HOST")]
    public string Host { get; init; } = string.Empty;

    [Envar("DB_PORT")]
    public int Port { get; init; }

    [Envar("DB_SSL_ENABLED")]
    public bool SslEnabled { get; init; } = true;

    [Envar("DB_CONNECTION_TIMEOUT")]
    public TimeSpan ConnectionTimeout { get; init; } = TimeSpan.FromSeconds(30);
}
```

### 2. Register in DI

Hook up configuration binding in your Startup.cs or DI setup:

```csharp
services
    .AddOptions<DatabaseSettings>()
    .BindEnvars();
```

### 3. Add Validation (Optional)

Validate environment variables using standard data annotation attributes:

```csharp
services
    .AddOptions<DatabaseSettings>()
    .BindEnvars()
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

### 4. Inject and Use Your Config

```csharp
public class MyService
{
    public MyService(IOptions<DatabaseSettings> settings)
    {
        var dbSettings = settings.Value;
    }
}
```

### 💡 Features

**Supported Types:**

- `string`, `char`, `bool`
- Numeric types: `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `decimal`
- `Guid`, `Uri`, `TimeSpan`, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`
- `Enum` (case-insensitive)
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

#### Working with `IOptionsSnapshot` and `IOptionsMonitor`

By default, `IOptionsSnapshot<T>` and `IOptionsMonitor<T>` are enabled and will always reflect the values from app startup.
Environment variable configs do not refresh at runtime.

You can disable support for `IOptionsSnapshot<T>` and `IOptionsMonitor<T>` if you want to ensure only `IOptions<T>` is used:

```csharp
services.AddOptions<DatabaseSettings>()
    .BindEnvars(settings =>
    {
        settings
            .BlockOptionsSnapshot()
            .BlockOptionsMonitor();
    });
```

### ⚠️ Limitations

- No runtime refresh: environment variables are read once on application startup.
- `IOptionsSnapshot` and `IOptionsMonitor` are enabled by default as read-only views, but can be disabled if needed.

---

With FriendlyEnvars, configuration is easy, explicit, and safe.
