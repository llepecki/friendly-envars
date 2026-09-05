using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using Xunit;

namespace FriendlyEnvars.Tests;

// Pins the fixes from the four-dimension product review: loud failures for unreachable [Envar]
// declarations, kind-preserving DateTime parsing, registration-time conversion, the public
// environment source, and the name prefix.
public class ReviewFindingsTests : EnvarTestsBase
{
    public class ShadowBase
    {
        [Envar("REVIEW_SHADOWED")]
        public string Value { get; set; } = string.Empty;
    }

    public class ShadowDerived : ShadowBase
    {
        public new string Value { get; set; } = string.Empty;
    }

    [Fact]
    public void AShadowedBaseEnvarFailsRegistrationInsteadOfSilentlyBindingNothing()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<EnvarsException>(() =>
            BindCapturedEnvironment<ShadowDerived>(services, new Dictionary<string, string?>
            {
                ["REVIEW_SHADOWED"] = "value"
            }));

        Assert.Equal(EnvarFailureKind.InvalidProperty, exception.FailureKind);
        Assert.Contains("hidden by a redeclaration", exception.Message, StringComparison.Ordinal);
    }

    public class StaticBase
    {
        [Envar("REVIEW_BASE_STATIC")]
        public static string Value { get; set; } = string.Empty;
    }

    public class StaticDerived : StaticBase
    {
        [Envar("REVIEW_INSTANCE")]
        public string Instance { get; set; } = string.Empty;
    }

    [Fact]
    public void AnInheritedStaticEnvarFailsRegistrationLikeADeclaredOne()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<EnvarsException>(() =>
            BindCapturedEnvironment<StaticDerived>(services, new Dictionary<string, string?>()));

        Assert.Equal(EnvarFailureKind.InvalidProperty, exception.FailureKind);
    }

    public class DateTimeOptions
    {
        [Envar("REVIEW_TIMESTAMP")]
        public DateTime Timestamp { get; set; }
    }

    [Fact]
    public void AUtcSuffixedDateTimeKeepsItsKindInsteadOfBecomingHostLocalTime()
    {
        var services = new ServiceCollection();
        BindCapturedEnvironment<DateTimeOptions>(services, new Dictionary<string, string?>
        {
            ["REVIEW_TIMESTAMP"] = "2024-06-01T12:00:00Z"
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<DateTimeOptions>>().Value;

        Assert.Equal(DateTimeKind.Utc, options.Timestamp.Kind);
        Assert.Equal(new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc), options.Timestamp);
    }

    public class NumberOptions
    {
        [Envar("REVIEW_NUMBER")]
        public int Number { get; set; }
    }

    [Fact]
    public void AnUnconvertibleValueFailsAtRegistrationNotAtTheFirstResolution()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<EnvarsException>(() =>
            BindCapturedEnvironment<NumberOptions>(services, new Dictionary<string, string?>
            {
                ["REVIEW_NUMBER"] = "not-a-number"
            }));

        Assert.Equal(EnvarFailureKind.Conversion, exception.FailureKind);

        // Registration failed before anything was added, so resolution cannot even start.
        Assert.DoesNotContain(services, static descriptor =>
            descriptor.ServiceType == typeof(Microsoft.Extensions.Options.IConfigureOptions<NumberOptions>));
    }

    [Fact]
    public void UseEnvironmentSourceBindsFromTheSnapshotWithoutTouchingTheProcessEnvironment()
    {
        var services = new ServiceCollection();
        services.AddOptions<NumberOptions>().BindEnvars(settings => settings.UseEnvironmentSource(
            new Dictionary<string, string?> { ["REVIEW_NUMBER"] = "42" }));

        using var provider = services.BuildServiceProvider();

        Assert.Equal(42, provider.GetRequiredService<IOptions<NumberOptions>>().Value.Number);
        Assert.Null(Environment.GetEnvironmentVariable("REVIEW_NUMBER"));
    }

    [Fact]
    public void UseEnvironmentSourceCopiesTheSnapshotSoLaterMutationsHaveNoEffect()
    {
        var variables = new Dictionary<string, string?> { ["REVIEW_NUMBER"] = "42" };
        var settings = new List<EnvarSettings>();

        var services = new ServiceCollection();
        services.AddOptions<NumberOptions>().BindEnvars(configured =>
        {
            configured.UseEnvironmentSource(variables);
            variables["REVIEW_NUMBER"] = "mutated-before-capture";
            settings.Add(configured);
        });

        using var provider = services.BuildServiceProvider();

        Assert.Equal(42, provider.GetRequiredService<IOptions<NumberOptions>>().Value.Number);
        Assert.Single(settings);
    }

    [Fact]
    public void UseNamePrefixReadsThePrefixedVariable()
    {
        var services = new ServiceCollection();
        services.AddOptions<NumberOptions>().BindEnvars(settings => settings
            .UseNamePrefix("APP_")
            .UseEnvironmentSource(new Dictionary<string, string?>
            {
                ["APP_REVIEW_NUMBER"] = "7",
                ["REVIEW_NUMBER"] = "999"
            }));

        using var provider = services.BuildServiceProvider();

        Assert.Equal(7, provider.GetRequiredService<IOptions<NumberOptions>>().Value.Number);
    }

    [Fact]
    public void AnInvalidPrefixIsRejectedUpFront()
    {
        var settings = new List<string>();

        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddOptions<NumberOptions>().BindEnvars(configured => configured.UseNamePrefix("BAD=PREFIX")));

        Assert.Empty(settings);
    }

    [Fact]
    public void TheInvariantCultureIsReportedByNameInConversionFailures()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<EnvarsException>(() =>
            BindCapturedEnvironment<NumberOptions>(services, new Dictionary<string, string?>
            {
                ["REVIEW_NUMBER"] = "x"
            }));

        Assert.Equal("invariant", exception.CultureName);
    }

    [Fact]
    public void AnAmbiguousCaseInsensitiveEnumNameIsReportedAsAmbiguous()
    {
        var binder = new DefaultEnvarPropertyBinder();

        var exception = Assert.Throws<FormatException>(
            () => binder.Convert("read", typeof(CaseCollision), CultureInfo.InvariantCulture));

        Assert.Contains("differ only by case", exception.Message, StringComparison.Ordinal);
    }

    public enum CaseCollision
    {
        Read = 1,
        READ = 2
    }

    public class ThrowingSetterOptions
    {
        [Envar("REVIEW_SETTER")]
        public string? Value
        {
            get => null;
            set => throw new InvalidOperationException($"rejected {value}");
        }
    }

    [Fact]
    public void AssignmentFailuresStayAtCreationWhileConversionFailuresMoveToRegistration()
    {
        var services = new ServiceCollection();

        // Registration succeeds: the setter is never invoked by the dry run.
        BindCapturedEnvironment<ThrowingSetterOptions>(services, new Dictionary<string, string?>
        {
            ["REVIEW_SETTER"] = "value"
        });

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<EnvarsException>(
            () => provider.GetRequiredService<IOptions<ThrowingSetterOptions>>().Value);

        Assert.Equal(EnvarFailureKind.Assignment, exception.FailureKind);
    }
}
