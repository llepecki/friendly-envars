using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace FriendlyEnvars.Benchmarks;

public class BindingBenchmarks
{
    private ScenarioRunner _runner = null!;
    private ServiceProvider _provider = null!;

    [Params(0, 1, 10, 100)]
    public int PropertyCount { get; set; }

    [Params("Absent", "String", "Numeric", "Enum", "CustomConverter")]
    public string ValueScenario { get; set; } = string.Empty;

    [GlobalSetup]
    public void Setup()
    {
        _runner = WorkloadRegistry.Resolve(PropertyCount, ValueScenario);
        _runner.PrepareEnvironment();
        _runner.RegisterServices();
        _provider = _runner.BuildRegisteredProvider();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _provider.Dispose();
    }

    // Registration runs once in setup. This measures the first resolve on a fresh provider, not the
    // registration call.
    [Benchmark]
    public object FirstOptionsAccess()
    {
        return _runner.FirstAccess();
    }

    [Benchmark]
    public object CachedOptionsAccess()
    {
        return _runner.CachedAccess(_provider);
    }

    [Benchmark]
    public object RepeatedFactory()
    {
        return _runner.FactoryCreate(_provider);
    }

    [Benchmark]
    public object SnapshotPerScope()
    {
        return _runner.SnapshotPerScope(_provider);
    }
}
