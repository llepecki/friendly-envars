using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using System;
using System.Linq;

namespace FriendlyEnvars.Benchmarks;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("usage: <benchmarks-executable> <artifacts-directory>");
            return 2;
        }

        var configuration = ManualConfig.CreateEmpty()
            .WithArtifactsPath(args[0])
            .AddJob(Job.ShortRun)
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddExporter(JsonExporter.Full)
            .AddLogger(ConsoleLogger.Default)
            .AddColumnProvider(DefaultColumnProviders.Instance);

        var summary = BenchmarkRunner.Run<BindingBenchmarks>(configuration);

        if (summary.HasCriticalValidationErrors || summary.Reports.Any(static report => !report.Success))
        {
            Console.Error.WriteLine("One or more benchmark cases failed to run.");
            return 1;
        }

        return 0;
    }
}
