using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using Xunit;

namespace FriendlyEnvars.Tests;

/// <summary>
/// Proves that discovery happens exactly once per registration rather than lazily under contention, and
/// that the library does not serialise calls into a caller's binder.
/// </summary>
/// <remarks>
/// The previous design built metadata lazily into a process-wide cache, so the first concurrent burst of
/// option creations raced to populate it. Building the plan eagerly during <c>BindEnvars</c> removes the
/// race entirely: by the time any options instance can be created there is nothing left to construct.
/// </remarks>
public class BindingConcurrencyTests : EnvarTestsBase
{
    private const int Participants = 32;

    private static readonly TimeSpan BarrierTimeout = TimeSpan.FromSeconds(30);

    public class SingleValueOptions
    {
        [Envar("M09_VALUE")]
        public string? Value { get; set; }
    }

    public class ThreeValueOptions
    {
        [Envar("M09_ONE")]
        public string? One { get; set; }

        [Envar("M09_TWO")]
        public string? Two { get; set; }

        [Envar("M09_THREE")]
        public string? Three { get; set; }
    }

    private sealed class RecordingObserver : IBindingPlanObserver
    {
        private int _planBuildStarted;
        private readonly List<string> _inspected = [];
        private readonly object _gate = new();

        public int PlanBuildStartedCount => Volatile.Read(ref _planBuildStarted);

        public IReadOnlyList<string> Inspected
        {
            get
            {
                lock (_gate)
                {
                    return _inspected.ToArray();
                }
            }
        }

        public void PlanBuildStarted() => Interlocked.Increment(ref _planBuildStarted);

        public void MetadataInspected(PropertyInfo property)
        {
            lock (_gate)
            {
                _inspected.Add(property.Name);
            }
        }
    }

    /// <summary>
    /// Counts conversions, and makes every participant rendezvous inside <c>Convert</c>. If the library
    /// serialised calls into the binder, only one participant could ever be inside it and the barrier
    /// would time out instead of releasing.
    /// </summary>
    private sealed class RendezvousBinder : IEnvarPropertyBinder
    {
        private readonly Barrier _barrier;
        private int _conversions;
        private int _timeouts;

        public RendezvousBinder(int participants)
        {
            _barrier = new Barrier(participants);
        }

        public int ConversionCount => Volatile.Read(ref _conversions);

        public int TimeoutCount => Volatile.Read(ref _timeouts);

        public object? Convert(string value, Type targetType, CultureInfo culture)
        {
            Interlocked.Increment(ref _conversions);

            if (!_barrier.SignalAndWait(BarrierTimeout))
            {
                Interlocked.Increment(ref _timeouts);
            }

            return value;
        }
    }

    private sealed class CountingBinder : IEnvarPropertyBinder
    {
        private int _conversions;

        public int ConversionCount => Volatile.Read(ref _conversions);

        public object? Convert(string value, Type targetType, CultureInfo culture)
        {
            Interlocked.Increment(ref _conversions);
            return value;
        }
    }

    /// <summary>
    /// Runs <paramref name="action"/> on dedicated threads. Dedicated threads rather than the thread pool,
    /// because a barrier across pool work items can deadlock if the pool declines to grow.
    /// </summary>
    private static void RunConcurrently(int participants, Action<int> action)
    {
        var threads = new Thread[participants];
        var failures = new Exception?[participants];

        for (int i = 0; i < participants; i++)
        {
            int index = i;

            threads[i] = new Thread(() =>
            {
                try
                {
                    action(index);
                }
                catch (Exception exception)
                {
                    failures[index] = exception;
                }
            })
            {
                IsBackground = true,
                Name = $"friendly-envars-concurrency-{index}"
            };
        }

        foreach (var thread in threads)
        {
            thread.Start();
        }

        foreach (var thread in threads)
        {
            Assert.True(thread.Join(BarrierTimeout), $"thread '{thread.Name}' did not finish; the library is serialising or deadlocking");
        }

        var thrown = failures.Where(static failure => failure is not null).ToArray();

        Assert.Empty(thrown);
    }

    [Fact]
    public void DiscoveryRunsExactlyOncePerRegistration_EvenUnderConcurrentOptionsCreation()
    {
        SetEnvironmentVariable("M09_ONE", "one");
        SetEnvironmentVariable("M09_TWO", "two");
        SetEnvironmentVariable("M09_THREE", "three");

        var observer = new RecordingObserver();

        var services = new ServiceCollection();
        OptionsBuilderExtensions.BindEnvarsCore(
            services.AddOptions<ThreeValueOptions>(),
            configure: null,
            ProcessEnvironmentVariableReader.Instance,
            observer);

        // Everything was discovered while BindEnvars ran.
        Assert.Equal(1, observer.PlanBuildStartedCount);
        Assert.Equal(
            [nameof(ThreeValueOptions.One), nameof(ThreeValueOptions.Two), nameof(ThreeValueOptions.Three)],
            observer.Inspected);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IOptionsFactory<ThreeValueOptions>>();

        RunConcurrently(Participants, _ =>
        {
            var options = factory.Create(Options.DefaultName);

            Assert.Equal("one", options.One);
            Assert.Equal("two", options.Two);
            Assert.Equal("three", options.Three);
        });

        // Creating 32 instances concurrently discovered nothing further.
        Assert.Equal(1, observer.PlanBuildStartedCount);
        Assert.Equal(3, observer.Inspected.Count);
    }

    [Fact]
    public void ConcurrentFactoryCreationsEnterTheSharedBinderSimultaneously()
    {
        SetEnvironmentVariable("M09_VALUE", "bound");

        var binder = new RendezvousBinder(Participants);

        var services = new ServiceCollection();
        services.AddOptions<SingleValueOptions>().BindEnvars(settings => settings.UseCustomEnvarPropertyBinder(binder));

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IOptionsFactory<SingleValueOptions>>();

        RunConcurrently(Participants, _ => Assert.Equal("bound", factory.Create(Options.DefaultName).Value));

        // One conversion per options instance, and every participant was inside Convert at the same time.
        Assert.Equal(Participants, binder.ConversionCount);
        Assert.Equal(0, binder.TimeoutCount);
    }

    [Fact]
    public void ConcurrentScopedSnapshotsEnterTheSharedBinderSimultaneously()
    {
        SetEnvironmentVariable("M09_VALUE", "bound");

        var binder = new RendezvousBinder(Participants);

        var services = new ServiceCollection();
        services.AddOptions<SingleValueOptions>().BindEnvars(settings => settings.UseCustomEnvarPropertyBinder(binder));

        using var provider = services.BuildServiceProvider();

        RunConcurrently(Participants, _ =>
        {
            using var scope = provider.CreateScope();

            Assert.Equal("bound", scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<SingleValueOptions>>().Value.Value);
        });

        Assert.Equal(Participants, binder.ConversionCount);
        Assert.Equal(0, binder.TimeoutCount);
    }

    [Fact]
    public void OneBinderInstanceIsSharedByEveryOptionsInstanceOfARegistration()
    {
        SetEnvironmentVariable("M09_VALUE", "bound");

        var binder = new CountingBinder();

        var services = new ServiceCollection();
        services.AddOptions<SingleValueOptions>().BindEnvars(settings => settings.UseCustomEnvarPropertyBinder(binder));

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IOptionsFactory<SingleValueOptions>>();

        RunConcurrently(Participants, _ => factory.Create(Options.DefaultName));

        // Exactly one conversion per created instance: the library neither caches conversions nor
        // repeats them, and it does not create a binder per instance.
        Assert.Equal(Participants, binder.ConversionCount);
    }

    [Fact]
    public void ConcurrentRegistrationsOfDifferentNamesEachDiscoverOnce()
    {
        SetEnvironmentVariable("M09_VALUE", "bound");

        var observers = new RecordingObserver[Participants];
        var collections = new ServiceCollection[Participants];

        // Registration itself is not required to be thread-safe against the SAME collection, so each
        // participant builds its own; what is asserted is that each performs exactly one discovery.
        RunConcurrently(Participants, index =>
        {
            observers[index] = new RecordingObserver();
            collections[index] = new ServiceCollection();

            OptionsBuilderExtensions.BindEnvarsCore(
                collections[index].AddOptions<SingleValueOptions>($"name-{index}"),
                configure: null,
                ProcessEnvironmentVariableReader.Instance,
                observers[index]);
        });

        foreach (var observer in observers)
        {
            Assert.Equal(1, observer.PlanBuildStartedCount);
            Assert.Equal([nameof(SingleValueOptions.Value)], observer.Inspected);
        }
    }
}
