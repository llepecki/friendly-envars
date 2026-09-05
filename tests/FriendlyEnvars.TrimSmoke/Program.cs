using FriendlyEnvars;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;

namespace FriendlyEnvars.TrimSmoke;

/// <summary>Runs the trimmed binding smoke test.</summary>
public static class Program
{
    public static int Main()
    {
        var services = new ServiceCollection();

        services.AddOptions<TrimSmokeOptions>().BindEnvars();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<TrimSmokeOptions>>().Value;

        if (!string.Equals(options.Name, "trimmed", StringComparison.Ordinal)
            || options.Count != 7
            || options.Endpoint is not { Host: "trim-host", Port: 8080 }
            || !string.Equals(options.Inherited, "from-base", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("Trim smoke failed: the bound values did not match the expected ones.");
            return 1;
        }

        Console.WriteLine("Trim smoke completed successfully!");
        return 0;
    }
}

/// <summary>Base type for the inheritance leg: the attribute walk must survive trimming.</summary>
public abstract class TrimSmokeOptionsBase
{
    [Envar("TRIM_SMOKE_INHERITED")]
    public virtual string Inherited { get; set; } = string.Empty;
}

/// <summary>
/// The shapes the gate exercises: string, numeric, an inherited virtual property, and a
/// TypeConverter-fallback type - the reflective paths trimming is most likely to break.
/// </summary>
public sealed class TrimSmokeOptions : TrimSmokeOptionsBase
{
    [Envar("TRIM_SMOKE_NAME")]
    public string Name { get; set; } = string.Empty;

    [Envar("TRIM_SMOKE_COUNT")]
    public int Count { get; set; }

    [Envar("TRIM_SMOKE_ENDPOINT")]
    public TrimSmokeEndpoint? Endpoint { get; set; }

    public override string Inherited { get; set; } = string.Empty;
}

/// <summary>Bound through the TypeConverter fallback from "host:port" text.</summary>
[System.ComponentModel.TypeConverter(typeof(TrimSmokeEndpointConverter))]
public sealed class TrimSmokeEndpoint
{
    public string Host { get; init; } = string.Empty;

    public int Port { get; init; }
}

/// <summary>Parses "host:port" text for the smoke test.</summary>
public sealed class TrimSmokeEndpointConverter : System.ComponentModel.TypeConverter
{
    public override bool CanConvertFrom(System.ComponentModel.ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(
        System.ComponentModel.ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object value)
    {
        if (value is string text)
        {
            int separator = text.IndexOf(':', StringComparison.Ordinal);

            return new TrimSmokeEndpoint
            {
                Host = text[..separator],
                Port = int.Parse(text[(separator + 1)..], System.Globalization.CultureInfo.InvariantCulture)
            };
        }

        return base.ConvertFrom(context, culture, value);
    }
}
