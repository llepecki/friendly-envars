using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FriendlyEnvars.RepositoryVerifier.Commands;

internal static class BenchmarkCommand
{
    private static readonly string[] Methods =
        ["FirstOptionsAccess", "CachedOptionsAccess", "RepeatedFactory", "SnapshotPerScope"];

    private static readonly int[] PropertyCounts = [0, 1, 10, 100];

    private static readonly string[] ValueScenarios =
        ["Absent", "String", "Numeric", "Enum", "CustomConverter"];

    private static readonly string[] ImprovementMethods = ["RepeatedFactory", "SnapshotPerScope"];

    private const double RegressionLimit = 1.10;
    private const double ImprovementLimit = 0.80;

    private sealed record Case(double MeanNanoseconds, double AllocatedBytes);

    public static void Run(CommandLine commandLine)
    {
        string baselinePath = commandLine.GetRequired("baseline");
        string candidatePath = commandLine.GetRequired("candidate");
        commandLine.EnsureAllConsumed();

        var failures = new List<string>();
        var baseline = Load(baselinePath, failures);
        var candidate = Load(candidatePath, failures);

        if (failures.Count > 0)
        {
            throw new VerificationException(Describe(baselinePath, candidatePath, failures));
        }

        var expectedKeys = ExpectedKeys();

        RequireExactKeys("baseline", baselinePath, baseline.Keys, expectedKeys, failures);
        RequireExactKeys("candidate", candidatePath, candidate.Keys, expectedKeys, failures);

        if (failures.Count > 0)
        {
            throw new VerificationException(Describe(baselinePath, candidatePath, failures));
        }

        double worstTimeRatio = 0;

        foreach (string key in expectedKeys.Order(StringComparer.Ordinal))
        {
            var left = baseline[key];
            var right = candidate[key];

            double timeRatio = right.MeanNanoseconds / left.MeanNanoseconds;
            worstTimeRatio = Math.Max(worstTimeRatio, timeRatio);

            if (timeRatio > RegressionLimit)
            {
                failures.Add(FormatRatioFailure(key, "mean time", left.MeanNanoseconds, right.MeanNanoseconds, timeRatio));
            }

            if (left.AllocatedBytes > 0)
            {
                double allocationRatio = right.AllocatedBytes / left.AllocatedBytes;

                if (allocationRatio > RegressionLimit)
                {
                    failures.Add(FormatRatioFailure(key, "allocated bytes", left.AllocatedBytes, right.AllocatedBytes, allocationRatio));
                }
            }
            else if (right.AllocatedBytes != 0)
            {
                failures.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{key}: baseline allocates nothing but the candidate allocates {right.AllocatedBytes} B/op; a zero baseline requires a zero candidate"));
            }
        }

        // Hot paths must improve by 20% in time or allocation for every value scenario.
        foreach (string scenario in ValueScenarios)
        {
            foreach (string method in ImprovementMethods)
            {
                string key = Key(method, 10, scenario);
                var left = baseline[key];
                var right = candidate[key];

                bool timeImproved = right.MeanNanoseconds / left.MeanNanoseconds <= ImprovementLimit;
                bool allocationImproved = left.AllocatedBytes > 0 && right.AllocatedBytes / left.AllocatedBytes <= ImprovementLimit;

                if (!timeImproved && !allocationImproved)
                {
                    string allocationNote = left.AllocatedBytes > 0
                        ? string.Create(CultureInfo.InvariantCulture, $"allocation {right.AllocatedBytes / left.AllocatedBytes:0.000}x")
                        : "allocation baseline is zero";

                    failures.Add(string.Create(
                        CultureInfo.InvariantCulture,
                        $"{key}: no metric with a nonzero baseline is at or below {ImprovementLimit:0.00}x (time {right.MeanNanoseconds / left.MeanNanoseconds:0.000}x, {allocationNote})"));
                }
            }
        }

        if (failures.Count > 0)
        {
            throw new VerificationException(Describe(baselinePath, candidatePath, failures));
        }

        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"benchmark OK: {expectedKeys.Count} case(s) compared; worst mean-time ratio {worstTimeRatio:0.000}x; " +
            $"every ten-property factory and snapshot case improved to <= {ImprovementLimit:0.00}x on a nonzero-baseline metric."));
    }

    private static HashSet<string> ExpectedKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (string method in Methods)
        {
            foreach (int propertyCount in PropertyCounts)
            {
                foreach (string scenario in ValueScenarios)
                {
                    keys.Add(Key(method, propertyCount, scenario));
                }
            }
        }

        return keys;
    }

    private static string Key(string method, int propertyCount, string scenario)
    {
        // BenchmarkDotNet's full JSON joins parameters with '&' and no spaces; the key uses the
        // same format.
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{method}(PropertyCount={propertyCount}&ValueScenario={scenario})");
    }

    private static void RequireExactKeys(
        string role, string path, IEnumerable<string> actual, HashSet<string> expected, List<string> failures)
    {
        var actualSet = new HashSet<string>(actual, StringComparer.Ordinal);

        foreach (string missing in expected.Except(actualSet, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            failures.Add($"{role} report '{path}' is missing case '{missing}'");
        }

        foreach (string extra in actualSet.Except(expected, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            failures.Add($"{role} report '{path}' contains an unexpected case '{extra}'");
        }
    }

    private static Dictionary<string, Case> Load(string path, List<string> failures)
    {
        var cases = new Dictionary<string, Case>(StringComparer.Ordinal);

        if (!File.Exists(path))
        {
            failures.Add($"report '{path}' does not exist");
            return cases;
        }

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (JsonException exception)
        {
            failures.Add($"report '{path}' is not valid JSON: {exception.GetType().FullName}");
            return cases;
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("Benchmarks", out var benchmarks) ||
                benchmarks.ValueKind != JsonValueKind.Array)
            {
                failures.Add($"report '{path}' has no Benchmarks array");
                return cases;
            }

            foreach (var benchmark in benchmarks.EnumerateArray())
            {
                LoadCase(path, benchmark, cases, failures);
            }
        }

        return cases;
    }

    private static void LoadCase(
        string path, JsonElement benchmark, Dictionary<string, Case> cases, List<string> failures)
    {
        string method = RequireString(benchmark, "Method");
        string parameters = RequireString(benchmark, "Parameters");
        string key = string.Create(CultureInfo.InvariantCulture, $"{method}({parameters.Replace("\"", "", StringComparison.Ordinal)})");

        if (benchmark.TryGetProperty("Success", out var success) && success.ValueKind == JsonValueKind.False)
        {
            failures.Add($"report '{path}' case '{key}' did not run successfully");
            return;
        }

        if (!benchmark.TryGetProperty("Statistics", out var statistics) ||
            statistics.ValueKind != JsonValueKind.Object ||
            !statistics.TryGetProperty("Mean", out var mean) ||
            !mean.TryGetDouble(out double meanNanoseconds))
        {
            failures.Add($"report '{path}' case '{key}' has no mean time; the case did not produce statistics");
            return;
        }

        if (!double.IsFinite(meanNanoseconds) || meanNanoseconds <= 0)
        {
            failures.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"report '{path}' case '{key}' has a non-finite or non-positive mean time ({meanNanoseconds})"));
            return;
        }

        if (!benchmark.TryGetProperty("Memory", out var memory) ||
            memory.ValueKind != JsonValueKind.Object ||
            !memory.TryGetProperty("BytesAllocatedPerOperation", out var allocated) ||
            !allocated.TryGetDouble(out double allocatedBytes))
        {
            failures.Add($"report '{path}' case '{key}' has no allocation figure; the memory diagnoser did not run");
            return;
        }

        if (!double.IsFinite(allocatedBytes) || allocatedBytes < 0)
        {
            failures.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"report '{path}' case '{key}' has a negative or non-finite allocation ({allocatedBytes})"));
            return;
        }

        if (!cases.TryAdd(key, new Case(meanNanoseconds, allocatedBytes)))
        {
            failures.Add($"report '{path}' declares case '{key}' more than once");
        }
    }

    private static string RequireString(JsonElement benchmark, string property)
    {
        return benchmark.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw new VerificationException($"a benchmark entry has no string '{property}' field.");
    }

    private static string FormatRatioFailure(string key, string metric, double left, double right, double ratio)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{key}: candidate {metric} is {ratio:0.000}x baseline ({right:0.###} vs {left:0.###}), above the {RegressionLimit:0.00}x limit");
    }

    private static string Describe(string baselinePath, string candidatePath, List<string> failures)
    {
        return $"Benchmark comparison of '{candidatePath}' against '{baselinePath}' failed:{Environment.NewLine}  - " +
               string.Join($"{Environment.NewLine}  - ", failures);
    }
}
