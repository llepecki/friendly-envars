using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Xunit;

namespace FriendlyEnvars.Tests;

/// <summary>
/// Proves that each <c>BindEnvars</c> call takes exactly one snapshot of the environment, and that every
/// options instance it later produces is built from that snapshot rather than from the live environment.
/// </summary>
public class OptionsLifecycleTests : EnvarTestsBase
{
    public class TwoValueOptions
    {
        [Envar("H03_FIRST")]
        public string? First { get; set; }

        [Envar("H03_SECOND")]
        public string? Second { get; set; }

        public string? NotDecorated { get; set; }
    }

    public class NumericOptions
    {
        [Envar("H03_NUMBER")]
        public int Number { get; set; } = -1;
    }

    public class StringOptions
    {
        [Envar("H03_TEXT")]
        public string Text { get; set; } = "default";
    }

    public class MixedShapeOptions
    {
        [Envar("H03_VALID")]
        public string? Valid { get; set; }

        [Envar("H03_UNSUPPORTED")]
        public string Unsupported { get; } = string.Empty;
    }

    public class ConvertedOptions
    {
        [Envar("H03_CONVERTED")]
        public MutablePayload? Payload { get; set; }
    }

    public sealed class MutablePayload
    {
        public string Value { get; set; } = string.Empty;
    }

    /// <summary>Records every read so both the count and the order can be asserted.</summary>
    private sealed class RecordingReader : IEnvironmentVariableReader
    {
        private readonly Dictionary<string, string?> _values;
        private readonly List<string> _reads = [];
        private readonly object _gate = new();

        public RecordingReader(Dictionary<string, string?> values)
        {
            _values = values;
        }

        public IReadOnlyList<string> Reads
        {
            get
            {
                lock (_gate)
                {
                    return _reads.ToArray();
                }
            }
        }

        public string? GetEnvironmentVariable(string name)
        {
            lock (_gate)
            {
                _reads.Add(name);
            }

            return _values.GetValueOrDefault(name);
        }
    }

    /// <summary>Fails the read of one specific variable, leaving the others readable.</summary>
    private sealed class FailingReader : IEnvironmentVariableReader
    {
        private readonly string _failOn;
        private readonly Func<Exception> _failure;
        private readonly List<string> _reads = [];

        public FailingReader(string failOn, Func<Exception> failure)
        {
            _failOn = failOn;
            _failure = failure;
        }

        public IReadOnlyList<string> Reads => _reads;

        public string? GetEnvironmentVariable(string name)
        {
            _reads.Add(name);

            if (string.Equals(name, _failOn, StringComparison.Ordinal))
            {
                throw _failure();
            }

            return "ok";
        }
    }

    private sealed class RecordingObserver : IBindingPlanObserver
    {
        private readonly List<PropertyInfo> _inspected = [];

        public int PlanBuildStartedCount { get; private set; }

        public IReadOnlyList<PropertyInfo> Inspected => _inspected;

        public void PlanBuildStarted()
        {
            PlanBuildStartedCount++;
        }

        public void MetadataInspected(PropertyInfo property)
        {
            _inspected.Add(property);
        }
    }

    /// <summary>Returns a brand new mutable object on every call, and counts the calls.</summary>
    private sealed class FreshPayloadBinder : IEnvarPropertyBinder
    {
        public int ConvertCount { get; private set; }

        public object? Convert(string value, Type targetType, CultureInfo culture)
        {
            ConvertCount++;
            return new MutablePayload { Value = value };
        }
    }

    private static OptionsBuilder<T> Bind<T>(
        IServiceCollection services,
        IEnvironmentVariableReader reader,
        string optionsName = "",
        Action<EnvarSettings>? configure = null,
        IBindingPlanObserver? observer = null) where T : class, new()
    {
        return OptionsBuilderExtensions.BindEnvarsCore(
            services.AddOptions<T>(optionsName), configure, reader, observer ?? NullBindingPlanObserver.Instance);
    }

    [Fact]
    public void Registration_ReadsEverySelectedPropertyExactlyOnce_AndOptionsCreationReadsNothing()
    {
        var reader = new RecordingReader(new Dictionary<string, string?>
        {
            ["H03_FIRST"] = "one",
            ["H03_SECOND"] = "two"
        });

        var services = new ServiceCollection();
        Bind<TwoValueOptions>(services, reader);

        // Every decorated property was read exactly once, and the undecorated one was not read at all.
        Assert.Equal(["H03_FIRST", "H03_SECOND"], reader.Reads);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IOptionsFactory<TwoValueOptions>>();

        for (int i = 0; i < 8; i++)
        {
            var options = factory.Create(Options.DefaultName);
            Assert.Equal("one", options.First);
            Assert.Equal("two", options.Second);
        }

        using (var scope = provider.CreateScope())
        {
            _ = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<TwoValueOptions>>().Value;
        }

        _ = provider.GetRequiredService<IOptionsMonitor<TwoValueOptions>>().CurrentValue;
        _ = provider.GetRequiredService<IOptions<TwoValueOptions>>().Value;

        // Creating options performed no further reads at all.
        Assert.Equal(2, reader.Reads.Count);
    }

    [Fact]
    public void Registration_BuildsPlanMetadataOncePerSelectedProperty()
    {
        var reader = new RecordingReader(new Dictionary<string, string?>());
        var observer = new RecordingObserver();

        var services = new ServiceCollection();
        Bind<TwoValueOptions>(services, reader, observer: observer);

        Assert.Equal(1, observer.PlanBuildStartedCount);
        Assert.Equal(
            [nameof(TwoValueOptions.First), nameof(TwoValueOptions.Second)],
            observer.Inspected.Select(static property => property.Name));
    }

    [Fact]
    public void UnsupportedPropertyShape_IsRejectedBeforeAnyEnvironmentRead()
    {
        var reader = new RecordingReader(new Dictionary<string, string?>
        {
            ["H03_VALID"] = "one",
            ["H03_UNSUPPORTED"] = "two"
        });

        var services = new ServiceCollection();
        var builder = services.AddOptions<MixedShapeOptions>();
        int descriptorCountBeforeBind = services.Count;

        var exception = Assert.Throws<EnvarsException>(
            () => OptionsBuilderExtensions.BindEnvarsCore(builder, null, reader, NullBindingPlanObserver.Instance));

        Assert.Equal(EnvarFailureKind.InvalidProperty, exception.FailureKind);
        Assert.Equal("H03_UNSUPPORTED", exception.EnvironmentVariableName);
        Assert.Equal(nameof(MixedShapeOptions.Unsupported), exception.PropertyName);

        // Every selected property is validated before any environment variable is read, so a malformed
        // options type is rejected without the environment having been touched at all - including the
        // variable belonging to the property that precedes the offending one.
        Assert.Empty(reader.Reads);
        Assert.Equal(descriptorCountBeforeBind, services.Count);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IConfigureOptions<MixedShapeOptions>));
    }

    [Fact]
    public void FailingRead_StopsAtTheFailingProperty_AndRegistersNothing()
    {
        var reader = new FailingReader("H03_SECOND", static () => new InvalidOperationException("read failed"));

        var services = new ServiceCollection();
        var builder = services.AddOptions<TwoValueOptions>();
        int descriptorCountBeforeBind = services.Count;

        var exception = Assert.Throws<EnvarsException>(
            () => OptionsBuilderExtensions.BindEnvarsCore(builder, null, reader, NullBindingPlanObserver.Instance));

        // Properties up to and including the failing one were read once each; later ones were not read.
        Assert.Equal(["H03_FIRST", "H03_SECOND"], reader.Reads);

        Assert.Equal(EnvarFailureKind.EnvironmentRead, exception.FailureKind);
        Assert.Equal("H03_SECOND", exception.EnvironmentVariableName);
        Assert.Equal(typeof(TwoValueOptions), exception.OptionsType);
        Assert.Equal(nameof(TwoValueOptions.Second), exception.PropertyName);
        Assert.Equal(typeof(string), exception.TargetType);
        Assert.Equal(typeof(InvalidOperationException).FullName, exception.CauseType);
        Assert.Null(exception.InnerException);

        // Nothing was added to the service collection, so the failed registration left no trace.
        Assert.Equal(descriptorCountBeforeBind, services.Count);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IConfigureOptions<TwoValueOptions>));
    }

    [Fact]
    public void CancelledRead_PropagatesUnchanged_AndRegistersNothing()
    {
        var cancellation = new OperationCanceledException("cancelled during read");
        var reader = new FailingReader("H03_SECOND", () => cancellation);

        var services = new ServiceCollection();
        var builder = services.AddOptions<TwoValueOptions>();
        int descriptorCountBeforeBind = services.Count;

        var thrown = Assert.Throws<OperationCanceledException>(
            () => OptionsBuilderExtensions.BindEnvarsCore(builder, null, reader, NullBindingPlanObserver.Instance));

        // Reference-equivalent and unwrapped: cancellation is the caller's control flow, not a failure.
        Assert.Same(cancellation, thrown);

        Assert.Equal(["H03_FIRST", "H03_SECOND"], reader.Reads);
        Assert.Equal(descriptorCountBeforeBind, services.Count);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IConfigureOptions<TwoValueOptions>));
    }

    [Fact]
    public void EnvironmentMutatedAfterRegistration_DoesNotAffectAnyOptionsAbstraction()
    {
        SetEnvironmentVariable("H03_TEXT", "captured");

        var services = new ServiceCollection();
        services.AddOptions<StringOptions>().BindEnvars();

        using var provider = services.BuildServiceProvider();

        var optionsBeforeMutation = provider.GetRequiredService<IOptions<StringOptions>>().Value;
        Assert.Equal("captured", optionsBeforeMutation.Text);

        Environment.SetEnvironmentVariable("H03_TEXT", "changed-but-still-valid");

        Assert.Equal("captured", provider.GetRequiredService<IOptions<StringOptions>>().Value.Text);
        Assert.Equal("captured", provider.GetRequiredService<IOptionsFactory<StringOptions>>().Create(Options.DefaultName).Text);
        Assert.Equal("captured", provider.GetRequiredService<IOptionsMonitor<StringOptions>>().CurrentValue.Text);
        Assert.Equal("captured", provider.GetRequiredService<IOptionsMonitor<StringOptions>>().Get(Options.DefaultName).Text);

        using (var scope = provider.CreateScope())
        {
            Assert.Equal("captured", scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<StringOptions>>().Value.Text);
        }

        // A further mutation is equally invisible. Conversion of an unconvertible value is covered by
        // InvalidValueSetAfterRegistration_DoesNotBreakOptionsCreation, which needs a non-string property.
        Environment.SetEnvironmentVariable("H03_TEXT", "also-ignored");

        using (var scope = provider.CreateScope())
        {
            Assert.Equal("captured", scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<StringOptions>>().Value.Text);
        }

        Assert.Equal("captured", provider.GetRequiredService<IOptionsFactory<StringOptions>>().Create(Options.DefaultName).Text);
    }

    [Fact]
    public void InvalidValueSetAfterRegistration_DoesNotBreakOptionsCreation()
    {
        SetEnvironmentVariable("H03_NUMBER", "42");

        var services = new ServiceCollection();
        services.AddOptions<NumericOptions>().BindEnvars();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IOptionsFactory<NumericOptions>>();

        Assert.Equal(42, factory.Create(Options.DefaultName).Number);

        Environment.SetEnvironmentVariable("H03_NUMBER", "not-a-number");

        // The captured "42" is still what gets converted, so every abstraction keeps succeeding.
        Assert.Equal(42, factory.Create(Options.DefaultName).Number);
        Assert.Equal(42, provider.GetRequiredService<IOptions<NumericOptions>>().Value.Number);
        Assert.Equal(42, provider.GetRequiredService<IOptionsMonitor<NumericOptions>>().CurrentValue.Number);
        Assert.Equal(42, provider.GetRequiredService<IOptionsMonitor<NumericOptions>>().Get(Options.DefaultName).Number);

        using (var scope = provider.CreateScope())
        {
            Assert.Equal(42, scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<NumericOptions>>().Value.Number);
        }
    }

    public class NumericCultureOptions
    {
        [Envar("H03_DECIMAL")]
        public double Value { get; set; }
    }

    [Fact]
    public void MutatingTheSuppliedCultureAfterRegistration_DoesNotAffectAnyOptionsAbstraction()
    {
        SetEnvironmentVariable("H03_DECIMAL", "1.5");

        // Constructed rather than fetched from the cache, so it is mutable.
        var culture = new CultureInfo("en-US");

        var services = new ServiceCollection();
        services.AddOptions<NumericCultureOptions>().BindEnvars(settings => settings.UseCulture(culture));

        using var provider = services.BuildServiceProvider();

        // Would turn "1.5" into 15 if the live instance were consulted at options-creation time.
        culture.NumberFormat.NumberDecimalSeparator = ",";
        culture.NumberFormat.NumberGroupSeparator = ".";

        Assert.Equal(1.5d, provider.GetRequiredService<IOptions<NumericCultureOptions>>().Value.Value);
        Assert.Equal(1.5d, provider.GetRequiredService<IOptionsFactory<NumericCultureOptions>>().Create(Options.DefaultName).Value);
        Assert.Equal(1.5d, provider.GetRequiredService<IOptionsMonitor<NumericCultureOptions>>().CurrentValue.Value);

        using var scope = provider.CreateScope();
        Assert.Equal(1.5d, scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<NumericCultureOptions>>().Value.Value);
    }

    [Fact]
    public void EachNamedRegistrationKeepsItsOwnSnapshot()
    {
        SetEnvironmentVariable("H03_TEXT", "before");

        var services = new ServiceCollection();
        services.AddOptions<StringOptions>("early").BindEnvars();

        Environment.SetEnvironmentVariable("H03_TEXT", "after");

        services.AddOptions<StringOptions>("late").BindEnvars();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IOptionsFactory<StringOptions>>();

        Assert.Equal("before", factory.Create("early").Text);
        Assert.Equal("after", factory.Create("late").Text);

        Environment.SetEnvironmentVariable("H03_TEXT", "later-still");

        Assert.Equal("before", factory.Create("early").Text);
        Assert.Equal("after", factory.Create("late").Text);

        var monitor = provider.GetRequiredService<IOptionsMonitor<StringOptions>>();
        Assert.Equal("before", monitor.Get("early").Text);
        Assert.Equal("after", monitor.Get("late").Text);

        using var scope = provider.CreateScope();
        var snapshot = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<StringOptions>>();
        Assert.Equal("before", snapshot.Get("early").Text);
        Assert.Equal("after", snapshot.Get("late").Text);
    }

    [Fact]
    public void AbsentVariable_LeavesTheDefaultValueUntouched()
    {
        var reader = new RecordingReader(new Dictionary<string, string?> { ["H03_TEXT"] = null });

        var services = new ServiceCollection();
        Bind<StringOptions>(services, reader);

        using var provider = services.BuildServiceProvider();

        Assert.Equal("default", provider.GetRequiredService<IOptions<StringOptions>>().Value.Text);
    }

    [Fact]
    public void EmptyVariable_IsCapturedAndPassedToTheBinder()
    {
        // Constructed through the reader seam rather than the process environment: on net8.0
        // Environment.SetEnvironmentVariable(name, "") deletes the variable instead of emptying it, so the
        // scenario is not reachable in-process on every supported target framework.
        var reader = new RecordingReader(new Dictionary<string, string?> { ["H03_TEXT"] = string.Empty });

        var services = new ServiceCollection();
        Bind<StringOptions>(services, reader);

        using var provider = services.BuildServiceProvider();

        Assert.Equal(string.Empty, provider.GetRequiredService<IOptions<StringOptions>>().Value.Text);
    }

    [Fact]
    public void EmptyVariable_ForANonStringProperty_FailsConversion()
    {
        var reader = new RecordingReader(new Dictionary<string, string?> { ["H03_NUMBER"] = string.Empty });

        var services = new ServiceCollection();
        Bind<NumericOptions>(services, reader);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IOptionsFactory<NumericOptions>>();

        var exception = Assert.Throws<EnvarsException>(() => factory.Create(Options.DefaultName));

        Assert.Equal(EnvarFailureKind.Conversion, exception.FailureKind);
        Assert.Equal("H03_NUMBER", exception.EnvironmentVariableName);
    }

    [Fact]
    public void ConvertedObjectsAreNotSharedBetweenOptionsInstances()
    {
        var reader = new RecordingReader(new Dictionary<string, string?> { ["H03_CONVERTED"] = "payload" });
        var binder = new FreshPayloadBinder();

        var services = new ServiceCollection();
        Bind<ConvertedOptions>(services, reader, configure: settings => settings.UseCustomEnvarPropertyBinder(binder));

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IOptionsFactory<ConvertedOptions>>();

        var first = factory.Create(Options.DefaultName);
        var second = factory.Create(Options.DefaultName);

        Assert.NotNull(first.Payload);
        Assert.NotNull(second.Payload);
        Assert.NotSame(first.Payload, second.Payload);

        // Mutating one instance's converted object must not be visible through the other.
        first.Payload!.Value = "mutated";
        Assert.Equal("payload", second.Payload!.Value);

        // The binder ran exactly once per present plan entry per options instance.
        Assert.Equal(2, binder.ConvertCount);
    }

    [Fact]
    public void ConversionIsFailFast_AndLaterEntriesAreNotAttempted()
    {
        var reader = new RecordingReader(new Dictionary<string, string?>
        {
            ["H03_FIRST"] = "one",
            ["H03_SECOND"] = "two"
        });

        var binder = new FailOnSecondBinder();

        var services = new ServiceCollection();
        Bind<TwoValueOptions>(services, reader, configure: settings => settings.UseCustomEnvarPropertyBinder(binder));

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IOptionsFactory<TwoValueOptions>>();

        var exception = Assert.Throws<EnvarsException>(() => factory.Create(Options.DefaultName));

        Assert.Equal(EnvarFailureKind.Conversion, exception.FailureKind);
        Assert.Equal("H03_SECOND", exception.EnvironmentVariableName);
        Assert.Equal(["one", "two"], binder.SeenValues);
    }

    private sealed class FailOnSecondBinder : IEnvarPropertyBinder
    {
        private readonly List<string> _seenValues = [];

        public IReadOnlyList<string> SeenValues => _seenValues;

        public object? Convert(string value, Type targetType, CultureInfo culture)
        {
            _seenValues.Add(value);

            if (value == "two")
            {
                throw new FormatException("rejected");
            }

            return value;
        }
    }
}
