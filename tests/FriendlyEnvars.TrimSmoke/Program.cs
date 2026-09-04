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

        if (!string.Equals(options.Name, "trimmed", StringComparison.Ordinal) || options.Count != 7)
        {
            Console.Error.WriteLine("Trim smoke failed: the bound values did not match the expected ones.");
            return 1;
        }

        Console.WriteLine("Trim smoke completed successfully!");
        return 0;
    }
}

/// <summary>Defines the values used by the trimmed binding smoke test.</summary>
public sealed class TrimSmokeOptions
{
    [Envar("TRIM_SMOKE_NAME")]
    public string Name { get; set; } = string.Empty;

    [Envar("TRIM_SMOKE_COUNT")]
    public int Count { get; set; }
}
