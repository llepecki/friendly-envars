using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Globalization;
using System.Threading;
using Xunit;

namespace FriendlyEnvars.Tests;

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

        // The registration-time dry run reaches the binder, so cancellation propagates here.
        var thrown = Assert.Throws<OperationCanceledException>(() => OptionsBuilderExtensions.BindEnvarsCore(
            services.AddOptions<TextOptions>(),
            settings => settings.UseCustomEnvarPropertyBinder(new CancellingBinder(cancellation)),
            new StubReader("bound"),
            NullBindingPlanObserver.Instance));

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

            // Preserve the instance and unwrap reflection.
            Assert.Same(cancellation, thrown);
            Assert.IsNotType<System.Reflection.TargetInvocationException>(thrown);

            // ExceptionDispatchInfo preserves the setter frame.
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
        // Derived cancellation types follow the same rule.
        using var source = new CancellationTokenSource();
        source.Cancel();

        var cancellation = new TaskCanceledExceptionSubstitute(source.Token);

        var services = new ServiceCollection();

        var thrown = Assert.Throws<TaskCanceledExceptionSubstitute>(() => OptionsBuilderExtensions.BindEnvarsCore(
            services.AddOptions<TextOptions>(),
            settings => settings.UseCustomEnvarPropertyBinder(new CancellingBinder(cancellation)),
            new StubReader("bound"),
            NullBindingPlanObserver.Instance));

        Assert.Same(cancellation, thrown);
        Assert.Equal(source.Token, thrown.CancellationToken);
    }

    private sealed class TaskCanceledExceptionSubstitute : OperationCanceledException
    {
        public TaskCanceledExceptionSubstitute(CancellationToken token) : base(token)
        {
        }
    }

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
        // Non-cancellation failures remain sanitized.
        var services = new ServiceCollection();

        var exception = Assert.Throws<EnvarsException>(() => OptionsBuilderExtensions.BindEnvarsCore(
            services.AddOptions<TextOptions>(),
            static settings => settings.UseCustomEnvarPropertyBinder(new ThrowingBinder()),
            new StubReader("bound"),
            NullBindingPlanObserver.Instance));

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
        Assert.Equal("invariant", conversion.CultureName);
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

        // Conversion failures surface at registration; assignment failures at the first creation.
        try
        {
            OptionsBuilderExtensions.BindEnvarsCore(
                services.AddOptions<T>(), configure, new StubReader("bound"), NullBindingPlanObserver.Instance);
        }
        catch (EnvarsException registrationFailure)
        {
            return registrationFailure;
        }

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

        var thrown = Assert.Throws<OperationCanceledException>(() => OptionsBuilderExtensions.BindEnvarsCore(
            services.AddOptions<TextOptions>(),
            settings => settings.UseCustomEnvarPropertyBinder(new CancellingBinder(cancellation)),
            new StubReader(Secret),
            NullBindingPlanObserver.Instance));

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
