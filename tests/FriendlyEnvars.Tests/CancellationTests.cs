using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Globalization;
using System.Threading;
using Xunit;

namespace FriendlyEnvars.Tests;

/// <summary>
/// Cancellation is the caller's control flow, not a binding failure, so it must reach the caller as the
/// very object that was thrown rather than being sanitised into an <see cref="EnvarsException"/>.
/// </summary>
/// <remarks>
/// All three stages that call caller-supplied or reflected code are covered: the environment read, the
/// binder, and the property setter. The setter case matters most, because reflection wraps whatever a
/// setter throws in a <see cref="System.Reflection.TargetInvocationException"/>, so cancellation has to
/// be unwrapped to be propagated unchanged.
/// </remarks>
public class CancellationTests : EnvarTestsBase
{
    public class TextOptions
    {
        [Envar("L01_TEXT")]
        public string? Value { get; set; }
    }

    public class CancellingSetterOptions
    {
        internal static OperationCanceledException? Cancellation { get; set; }

        [Envar("L01_SETTER")]
        public string? Value
        {
            get => null;
            set => throw Cancellation ?? new OperationCanceledException();
        }
    }

    private sealed class CancellingBinder : IEnvarPropertyBinder
    {
        private readonly OperationCanceledException _cancellation;

        public CancellingBinder(OperationCanceledException cancellation)
        {
            _cancellation = cancellation;
        }

        public object? Convert(string value, Type targetType, CultureInfo culture) => throw _cancellation;
    }

    private sealed class CancellingReader : IEnvironmentVariableReader
    {
        private readonly OperationCanceledException _cancellation;

        public CancellingReader(OperationCanceledException cancellation)
        {
            _cancellation = cancellation;
        }

        public string? GetEnvironmentVariable(string name) => throw _cancellation;
    }

    [Fact]
    public void BinderCancellation_PropagatesTheSameInstance()
    {
        var cancellation = new OperationCanceledException("cancelled inside the binder");

        var services = new ServiceCollection();
        OptionsBuilderExtensions.BindEnvarsCore(
            services.AddOptions<TextOptions>(),
            settings => settings.UseCustomEnvarPropertyBinder(new CancellingBinder(cancellation)),
            new StubReader("bound"),
            NullBindingPlanObserver.Instance);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IOptionsFactory<TextOptions>>();

        var thrown = Assert.Throws<OperationCanceledException>(() => factory.Create(Options.DefaultName));

        Assert.Same(cancellation, thrown);
    }

    [Fact]
    public void SetterCancellation_PropagatesTheSameInstanceUnwrapped()
    {
        var cancellation = new OperationCanceledException("cancelled inside the setter");
        CancellingSetterOptions.Cancellation = cancellation;

        try
        {
            var services = new ServiceCollection();
            OptionsBuilderExtensions.BindEnvarsCore(
                services.AddOptions<CancellingSetterOptions>(),
                configure: null,
                new StubReader("bound"),
                NullBindingPlanObserver.Instance);

            using var provider = services.BuildServiceProvider();
            var factory = provider.GetRequiredService<IOptionsFactory<CancellingSetterOptions>>();

            var thrown = Assert.Throws<OperationCanceledException>(() => factory.Create(Options.DefaultName));

            // Unwrapped from the reflection wrapper, and the same object the setter threw.
            Assert.Same(cancellation, thrown);
            Assert.IsNotType<System.Reflection.TargetInvocationException>(thrown);

            // Rethrown through ExceptionDispatchInfo rather than `throw cancellation`, which would erase
            // the setter's frame and make the cancellation harder to trace back to its origin.
            Assert.Contains("set_Value", thrown.StackTrace!, StringComparison.Ordinal);
        }
        finally
        {
            CancellingSetterOptions.Cancellation = null;
        }
    }

    [Fact]
    public void EnvironmentReadCancellation_PropagatesTheSameInstance()
    {
        var cancellation = new OperationCanceledException("cancelled during the environment read");

        var services = new ServiceCollection();
        var builder = services.AddOptions<TextOptions>();

        var thrown = Assert.Throws<OperationCanceledException>(() => OptionsBuilderExtensions.BindEnvarsCore(
            builder, configure: null, new CancellingReader(cancellation), NullBindingPlanObserver.Instance));

        Assert.Same(cancellation, thrown);
    }

    [Fact]
    public void ADerivedCancellationIsAlsoPropagatedUnchanged()
    {
        // TaskCanceledException derives from OperationCanceledException and must behave identically.
        using var source = new CancellationTokenSource();
        source.Cancel();

        var cancellation = new TaskCanceledExceptionSubstitute(source.Token);

        var services = new ServiceCollection();
        OptionsBuilderExtensions.BindEnvarsCore(
            services.AddOptions<TextOptions>(),
            settings => settings.UseCustomEnvarPropertyBinder(new CancellingBinder(cancellation)),
            new StubReader("bound"),
            NullBindingPlanObserver.Instance);

        using var provider = services.BuildServiceProvider();

        var thrown = Assert.Throws<TaskCanceledExceptionSubstitute>(
            () => provider.GetRequiredService<IOptionsFactory<TextOptions>>().Create(Options.DefaultName));

        Assert.Same(cancellation, thrown);
        Assert.Equal(source.Token, thrown.CancellationToken);
    }

    /// <summary>A derived cancellation type, standing in for TaskCanceledException.</summary>
    private sealed class TaskCanceledExceptionSubstitute : OperationCanceledException
    {
        public TaskCanceledExceptionSubstitute(CancellationToken token) : base(token)
        {
        }
    }

    /// <summary>Returns the same value for every name.</summary>
    private sealed class StubReader : IEnvironmentVariableReader
    {
        private readonly string? _value;

        public StubReader(string? value)
        {
            _value = value;
        }

        public string? GetEnvironmentVariable(string name) => _value;
    }

    [Fact]
    public void ANonCancellationFailureIsStillSanitised()
    {
        // The cancellation filter must not accidentally let other exceptions through unwrapped.
        var services = new ServiceCollection();
        OptionsBuilderExtensions.BindEnvarsCore(
            services.AddOptions<TextOptions>(),
            static settings => settings.UseCustomEnvarPropertyBinder(new ThrowingBinder()),
            new StubReader("bound"),
            NullBindingPlanObserver.Instance);

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<EnvarsException>(
            () => provider.GetRequiredService<IOptionsFactory<TextOptions>>().Create(Options.DefaultName));

        Assert.Equal(EnvarFailureKind.Conversion, exception.FailureKind);
        Assert.Null(exception.InnerException);
    }

    private sealed class ThrowingBinder : IEnvarPropertyBinder
    {
        public object? Convert(string value, Type targetType, CultureInfo culture) =>
            throw new InvalidOperationException("not a cancellation");
    }

    [Fact]
    public void ConversionAndAssignmentFailuresAreReportedAsDifferentKinds()
    {
        var conversion = CaptureFailure<TextOptions>(
            static settings => settings.UseCustomEnvarPropertyBinder(new ThrowingBinder()));

        var assignment = CaptureFailure<ThrowingSetterOptions>(configure: null);

        Assert.Equal(EnvarFailureKind.Conversion, conversion.FailureKind);
        Assert.Equal(EnvarFailureKind.Assignment, assignment.FailureKind);
        Assert.NotEqual(conversion.FailureKind, assignment.FailureKind);

        // Conversion carries the culture and binder that were in play; assignment cannot.
        Assert.Equal(CultureInfo.InvariantCulture.Name, conversion.CultureName);
        Assert.Equal(typeof(ThrowingBinder), conversion.BinderType);
        Assert.Null(assignment.CultureName);
        Assert.Null(assignment.BinderType);

        Assert.Equal(typeof(InvalidOperationException).FullName, conversion.CauseType);
        Assert.Equal(typeof(NotSupportedException).FullName, assignment.CauseType);

        Assert.Null(conversion.InnerException);
        Assert.Null(assignment.InnerException);

        Assert.Equal(
            $"Failed to convert environment variable 'L01_TEXT' to 'System.String' for option " +
            $"'{typeof(TextOptions).FullName}.Value' (options name '<default>').",
            conversion.Message);

        Assert.Equal(
            $"Failed to assign environment variable 'L01_SETTER' to option " +
            $"'{typeof(ThrowingSetterOptions).FullName}.Value' (options name '<default>').",
            assignment.Message);
    }

    public class ThrowingSetterOptions
    {
        [Envar("L01_SETTER")]
        public string? Value
        {
            get => null;
            set => throw new NotSupportedException($"setter rejected {value}");
        }
    }

    private static EnvarsException CaptureFailure<T>(Action<EnvarSettings>? configure) where T : class, new()
    {
        var services = new ServiceCollection();
        OptionsBuilderExtensions.BindEnvarsCore(
            services.AddOptions<T>(), configure, new StubReader("bound"), NullBindingPlanObserver.Instance);

        using var provider = services.BuildServiceProvider();

        return Assert.Throws<EnvarsException>(
            () => provider.GetRequiredService<IOptionsFactory<T>>().Create(Options.DefaultName));
    }

    [Fact]
    public void CancellationCarriesNothingDerivedFromTheValue()
    {
        // A propagated cancellation is the caller's own object, so the library adds nothing to it - but
        // it must also not have been replaced by a sanitised failure that lost the caller's intent.
        const string Secret = "QZXJKVWYPLMB-SECRET-VALUE";
        var cancellation = new OperationCanceledException("cancelled");

        var services = new ServiceCollection();
        OptionsBuilderExtensions.BindEnvarsCore(
            services.AddOptions<TextOptions>(),
            settings => settings.UseCustomEnvarPropertyBinder(new CancellingBinder(cancellation)),
            new StubReader(Secret),
            NullBindingPlanObserver.Instance);

        using var provider = services.BuildServiceProvider();

        var thrown = Assert.Throws<OperationCanceledException>(
            () => provider.GetRequiredService<IOptionsFactory<TextOptions>>().Create(Options.DefaultName));

        Assert.Same(cancellation, thrown);
        Assert.DoesNotContain(Secret, thrown.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void CancellationDuringTheReadLeavesNothingRegistered()
    {
        var cancellation = new OperationCanceledException("cancelled during the environment read");

        var services = new ServiceCollection();
        var builder = services.AddOptions<TextOptions>();
        int descriptorCountBeforeBind = services.Count;

        Assert.Throws<OperationCanceledException>(() => OptionsBuilderExtensions.BindEnvarsCore(
            builder, configure: null, new CancellingReader(cancellation), NullBindingPlanObserver.Instance));

        Assert.Equal(descriptorCountBeforeBind, services.Count);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IConfigureOptions<TextOptions>));
    }
}
