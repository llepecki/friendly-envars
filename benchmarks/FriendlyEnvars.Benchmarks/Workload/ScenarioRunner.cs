using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace FriendlyEnvars.Benchmarks;

// Shared source keeps the baseline and candidate workloads identical.
public abstract class ScenarioRunner
{
    public abstract void PrepareEnvironment();

    public abstract void RegisterServices();

    public abstract ServiceProvider BuildRegisteredProvider();

    public abstract object FirstAccess();

    public abstract object CachedAccess(ServiceProvider provider);

    public abstract object FactoryCreate(ServiceProvider provider);

    public abstract object SnapshotPerScope(ServiceProvider provider);
}

public sealed class ScenarioRunner<T> : ScenarioRunner where T : class, new()
{
    private readonly IReadOnlyDictionary<string, string?> _environment;
    private ServiceCollection? _registeredServices;

    public ScenarioRunner(IReadOnlyDictionary<string, string?> environment)
    {
        _environment = environment;
    }

    public override void PrepareEnvironment()
    {
        foreach (var pair in _environment)
        {
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
    }

    // BindEnvars runs once, in setup. Measured methods only consume options.
    public override void RegisterServices()
    {
        var services = new ServiceCollection();
        services.AddOptions<T>().BindEnvars();
        _registeredServices = services;
    }

    public override ServiceProvider BuildRegisteredProvider()
    {
        return RegisteredServices.BuildServiceProvider();
    }

    public override object FirstAccess()
    {
        using var provider = RegisteredServices.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<T>>().Value;
    }

    private ServiceCollection RegisteredServices =>
        _registeredServices ?? throw new InvalidOperationException("RegisterServices has not run.");

    public override object CachedAccess(ServiceProvider provider)
    {
        return provider.GetRequiredService<IOptions<T>>().Value;
    }

    public override object FactoryCreate(ServiceProvider provider)
    {
        return provider.GetRequiredService<IOptionsFactory<T>>().Create(Options.DefaultName);
    }

    public override object SnapshotPerScope(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<T>>().Value;
    }
}

public static class WorkloadRegistry
{
    public static ScenarioRunner Resolve(int propertyCount, string valueScenario)
    {
        if (propertyCount == 0)
        {
            // The zero-property case binds nothing, so every value scenario is the same case.
            return new ScenarioRunner<EmptyProps>(new Dictionary<string, string?>());
        }

        return (valueScenario, propertyCount) switch
        {
            ("String", 1) => Runner<StringProps1>("STRING", 1, static i => $"value-{i}"),
            ("String", 10) => Runner<StringProps10>("STRING", 10, static i => $"value-{i}"),
            ("String", 100) => Runner<StringProps100>("STRING", 100, static i => $"value-{i}"),
            ("Numeric", 1) => Runner<NumericProps1>("NUMERIC", 1, static i => i.ToString(CultureInfo.InvariantCulture)),
            ("Numeric", 10) => Runner<NumericProps10>("NUMERIC", 10, static i => i.ToString(CultureInfo.InvariantCulture)),
            ("Numeric", 100) => Runner<NumericProps100>("NUMERIC", 100, static i => i.ToString(CultureInfo.InvariantCulture)),
            ("Enum", 1) => Runner<EnumProps1>("ENUM", 1, static i => $"Level{i % 10}"),
            ("Enum", 10) => Runner<EnumProps10>("ENUM", 10, static i => $"Level{i % 10}"),
            ("Enum", 100) => Runner<EnumProps100>("ENUM", 100, static i => $"Level{i % 10}"),
            ("CustomConverter", 1) => Runner<CustomConverterProps1>("CUSTOMCONVERTER", 1, static i => $"host{i}:{80 + i}"),
            ("CustomConverter", 10) => Runner<CustomConverterProps10>("CUSTOMCONVERTER", 10, static i => $"host{i}:{80 + i}"),
            ("CustomConverter", 100) => Runner<CustomConverterProps100>("CUSTOMCONVERTER", 100, static i => $"host{i}:{80 + i}"),
            ("Absent", 1) => Runner<AbsentProps1>("ABSENT", 1, valueFor: null),
            ("Absent", 10) => Runner<AbsentProps10>("ABSENT", 10, valueFor: null),
            ("Absent", 100) => Runner<AbsentProps100>("ABSENT", 100, valueFor: null),
            _ => throw new ArgumentException(
                $"No workload for property count {propertyCount} and scenario '{valueScenario}'.")
        };
    }

    private static ScenarioRunner<T> Runner<T>(string kind, int count, Func<int, string>? valueFor)
        where T : class, new()
    {
        var environment = new Dictionary<string, string?>(count);

        for (int i = 0; i < count; i++)
        {
            // A null value clears the variable, so the absent scenario holds even if a previous
            // process left the variable set.
            environment[$"BENCH_{kind}_{count}_{i}"] = valueFor?.Invoke(i);
        }

        return new ScenarioRunner<T>(environment);
    }
}
