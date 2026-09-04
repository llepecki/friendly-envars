using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Xunit;

namespace FriendlyEnvars.Tests;

// Pins reflection and environment reads to registration, not options creation.
public class PerformanceContractTests
{
    public enum Level
    {
        Low,
        Medium,
        High
    }

    [Flags]
    public enum Modes
    {
        None = 0,
        Alpha = 1,
        Beta = 2,
        Gamma = 4
    }

    public sealed class WideOptions
    {
        [Envar("PERF_NAME")]
        public string Name { get; set; } = string.Empty;

        [Envar("PERF_COUNT")]
        public int Count { get; set; }

        [Envar("PERF_RATIO")]
        public double? Ratio { get; set; }

        [Envar("PERF_LEVEL")]
        public Level Level { get; set; }

        [Envar("PERF_MODES")]
        public Modes Modes { get; set; }

        [Envar("PERF_ABSENT")]
        public string? Absent { get; set; }

        public string? Undecorated { get; set; }
    }

    private static readonly string[] SelectedPropertyNames =
        ["Name", "Count", "Ratio", "Level", "Modes", "Absent"];

    private static CountingReader CreateReader()
    {
        return new CountingReader(new Dictionary<string, string?>
        {
            ["PERF_NAME"] = "measured",
            ["PERF_COUNT"] = "42",
            ["PERF_RATIO"] = "2.5",
            ["PERF_LEVEL"] = "Medium",
            ["PERF_MODES"] = "Alpha,Gamma",
            ["PERF_ABSENT"] = null
        });
    }

    private static void AssertBound(WideOptions options)
    {
        Assert.Equal("measured", options.Name);
        Assert.Equal(42, options.Count);
        Assert.Equal(2.5, options.Ratio);
        Assert.Equal(Level.Medium, options.Level);
        Assert.Equal(Modes.Alpha | Modes.Gamma, options.Modes);
        Assert.Null(options.Absent);
    }

    [Fact]
    public void RegistrationReadsEachSelectedPropertyOnceAndReportsItOnce()
    {
        var services = new ServiceCollection();
        var reader = CreateReader();
        var observer = new CountingObserver();

        OptionsBuilderExtensions.BindEnvarsCore(services.AddOptions<WideOptions>(), null, reader, observer);

        // Absent values still require one read; undecorated properties require none.
        Assert.Equal(SelectedPropertyNames.Length, reader.ReadCount);
        Assert.Equal(1, observer.PlanBuildStartedCount);
        Assert.Equal(
            SelectedPropertyNames.OrderBy(static name => name, StringComparer.Ordinal),
            observer.InspectedNames.OrderBy(static name => name, StringComparer.Ordinal));
        Assert.Equal(SelectedPropertyNames.Length, observer.InspectedNames.Count);
    }

    [Fact]
    public void CreatingOptionsThroughFactoryAndSnapshotAddsNoReadsAndNoInspection()
    {
        var services = new ServiceCollection();
        var reader = CreateReader();
        var observer = new CountingObserver();

        OptionsBuilderExtensions.BindEnvarsCore(services.AddOptions<WideOptions>(), null, reader, observer);

        int readsAfterRegistration = reader.ReadCount;
        int inspectionsAfterRegistration = observer.InspectedNames.Count;

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IOptionsFactory<WideOptions>>();

        // The factory creates per call; snapshots create per scope.
        for (int i = 0; i < 32; i++)
        {
            AssertBound(factory.Create(Options.DefaultName));
        }

        for (int i = 0; i < 32; i++)
        {
            using var scope = provider.CreateScope();
            AssertBound(scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<WideOptions>>().Value);
        }

        Assert.Equal(readsAfterRegistration, reader.ReadCount);
        Assert.Equal(1, observer.PlanBuildStartedCount);
        Assert.Equal(inspectionsAfterRegistration, observer.InspectedNames.Count);
    }

    [Fact]
    public void ACustomBinderStillReceivesTheDeclaredTypeThroughThePublicContract()
    {
        // Custom binders receive the declared type, including Nullable<T>.
        var services = new ServiceCollection();
        var reader = CreateReader();
        var recordingBinder = new TypeRecordingBinder();

        OptionsBuilderExtensions.BindEnvarsCore(
            services.AddOptions<WideOptions>(),
            settings => settings.UseCustomEnvarPropertyBinder(recordingBinder),
            reader,
            NullBindingPlanObserver.Instance);

        using var provider = services.BuildServiceProvider();
        AssertBound(provider.GetRequiredService<IOptionsFactory<WideOptions>>().Create(Options.DefaultName));

        Assert.Contains(typeof(double?), recordingBinder.SeenTypes);
        Assert.DoesNotContain(typeof(double), recordingBinder.SeenTypes);
    }

    private sealed class CountingReader : IEnvironmentVariableReader
    {
        private readonly Dictionary<string, string?> _values;
        private int _readCount;

        public CountingReader(Dictionary<string, string?> values)
        {
            _values = values;
        }

        public int ReadCount => Volatile.Read(ref _readCount);

        public string? GetEnvironmentVariable(string name)
        {
            Interlocked.Increment(ref _readCount);
            return _values.GetValueOrDefault(name);
        }
    }

    private sealed class CountingObserver : IBindingPlanObserver
    {
        private readonly List<string> _inspectedNames = [];
        private readonly object _gate = new();
        private int _planBuildStartedCount;

        public int PlanBuildStartedCount => Volatile.Read(ref _planBuildStartedCount);

        public IReadOnlyList<string> InspectedNames
        {
            get
            {
                lock (_gate)
                {
                    return _inspectedNames.ToArray();
                }
            }
        }

        public void PlanBuildStarted() => Interlocked.Increment(ref _planBuildStartedCount);

        public void MetadataInspected(PropertyInfo property)
        {
            lock (_gate)
            {
                _inspectedNames.Add(property.Name);
            }
        }
    }

    private sealed class TypeRecordingBinder : IEnvarPropertyBinder
    {
        private readonly DefaultEnvarPropertyBinder _inner = new();
        private readonly List<Type> _seenTypes = [];
        private readonly object _gate = new();

        public IReadOnlyList<Type> SeenTypes
        {
            get
            {
                lock (_gate)
                {
                    return _seenTypes.ToArray();
                }
            }
        }

        public object? Convert(string value, Type targetType, System.Globalization.CultureInfo culture)
        {
            lock (_gate)
            {
                _seenTypes.Add(targetType);
            }

            return _inner.Convert(value, targetType, culture);
        }
    }
}
