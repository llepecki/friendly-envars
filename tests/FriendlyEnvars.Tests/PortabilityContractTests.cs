using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Xunit;

namespace FriendlyEnvars.Tests;

/// <summary>
/// The subset of behaviour that depends on the host operating system or target framework rather than on
/// the library alone: how environment-variable names and values survive a round trip through the real
/// process environment, which property shapes are accepted, and when values are captured.
/// </summary>
/// <remarks>
/// CI runs this class on Ubuntu, Windows and macOS under both target frameworks via
/// <c>--filter 'Category=Portability'</c>. Everything here therefore goes through the real environment
/// wherever the contract is about the environment, and only uses the internal reader seam where a
/// scenario is not expressible in-process on every framework.
/// </remarks>
[Trait("Category", "Portability")]
public class PortabilityContractTests : EnvarTestsBase
{
    public class NameOptions
    {
        [Envar("PORT_PLAIN")]
        public string? Plain { get; set; }

        [Envar("PORT_WITH_1_DIGIT")]
        public string? WithDigit { get; set; }

        [Envar("PORT_Å_VAR")]
        public string? Unicode { get; set; }

        [Envar("PORT VAR")]
        public string? WithSpace { get; set; }
    }

    public class ValueOptions
    {
        [Envar("PORT_VALUE")]
        public string? Value { get; set; }
    }

    public class NumericOptions
    {
        [Envar("PORT_NUMBER")]
        public double Number { get; set; }
    }

    public class GetterOnlyOptions
    {
        [Envar("PORT_GETTER_ONLY")]
        public string GetterOnly { get; } = "untouched";
    }

    public class InitOnlyOptions
    {
        [Envar("PORT_INIT_ONLY")]
        public string? InitOnly { get; init; }
    }

    public abstract class BaseOptions
    {
        [Envar("PORT_INHERITED")]
        public string? Inherited { get; set; }
    }

    public class DerivedOptions : BaseOptions
    {
        [Envar("PORT_DECLARED")]
        public string? Declared { get; set; }
    }

    public class DefaultedOptions
    {
        [Envar("PORT_DEFAULTED")]
        public string Defaulted { get; set; } = "default";
    }

    private static T Resolve<T>(ServiceCollection services) where T : class, new()
    {
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<T>>().Value;
    }

    [Fact]
    public void EnvironmentNames_SurviveARoundTripThroughTheProcessEnvironment()
    {
        SetEnvironmentVariable("PORT_PLAIN", "plain");
        SetEnvironmentVariable("PORT_WITH_1_DIGIT", "digit");
        SetEnvironmentVariable("PORT_Å_VAR", "unicode");
        SetEnvironmentVariable("PORT VAR", "space");

        var services = new ServiceCollection();
        services.AddOptions<NameOptions>().BindEnvars();

        var options = Resolve<NameOptions>(services);

        Assert.Equal("plain", options.Plain);
        Assert.Equal("digit", options.WithDigit);
        Assert.Equal("unicode", options.Unicode);
        Assert.Equal("space", options.WithSpace);
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("  leading and trailing  ")]
    [InlineData("with\ttab")]
    [InlineData("with\nnewline")]
    [InlineData("with\r\ncrlf")]
    [InlineData("unicode Åé中文")]
    [InlineData("emoji \U0001F512")]
    public void Values_ArePreservedByteForByte(string value)
    {
        SetEnvironmentVariable("PORT_VALUE", value);

        var services = new ServiceCollection();
        services.AddOptions<ValueOptions>().BindEnvars();

        Assert.Equal(value, Resolve<ValueOptions>(services).Value);
    }

    [Fact]
    public void Conversion_UsesTheConfiguredCulture_NotTheHostCulture()
    {
        SetEnvironmentVariable("PORT_NUMBER", "3.14");

        var originalCulture = Thread.CurrentThread.CurrentCulture;

        try
        {
            // A host locale that reads "3.14" as three-point-one-four only under the invariant rules.
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            var services = new ServiceCollection();
            services.AddOptions<NumericOptions>().BindEnvars();

            Assert.Equal(3.14d, Resolve<NumericOptions>(services).Number);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void GetterOnlyProperty_IsRejectedWhileRegistering()
    {
        SetEnvironmentVariable("PORT_GETTER_ONLY", "value");

        var services = new ServiceCollection();

        var exception = Assert.Throws<EnvarsException>(() => services.AddOptions<GetterOnlyOptions>().BindEnvars());

        Assert.Equal(EnvarFailureKind.InvalidProperty, exception.FailureKind);
        Assert.Equal(nameof(GetterOnlyOptions.GetterOnly), exception.PropertyName);
    }

    [Fact]
    public void InitOnlyProperty_Binds()
    {
        SetEnvironmentVariable("PORT_INIT_ONLY", "value");

        var services = new ServiceCollection();
        services.AddOptions<InitOnlyOptions>().BindEnvars();

        Assert.Equal("value", Resolve<InitOnlyOptions>(services).InitOnly);
    }

    [Fact]
    public void InheritedPublicProperty_Binds()
    {
        SetEnvironmentVariable("PORT_INHERITED", "from-base");
        SetEnvironmentVariable("PORT_DECLARED", "from-derived");

        var services = new ServiceCollection();
        services.AddOptions<DerivedOptions>().BindEnvars();

        var options = Resolve<DerivedOptions>(services);

        Assert.Equal("from-base", options.Inherited);
        Assert.Equal("from-derived", options.Declared);
    }

    [Fact]
    public void AbsentVariable_LeavesTheDefaultInPlace()
    {
        var services = new ServiceCollection();
        services.AddOptions<DefaultedOptions>().BindEnvars();

        Assert.Equal("default", Resolve<DefaultedOptions>(services).Defaulted);
    }

    [Fact]
    public void CapturedEmptyValue_BindsAsAnEmptyString()
    {
        // Expressed through the reader seam: on net8.0 setting an empty value deletes the variable, so
        // this scenario cannot be built from the process environment on every supported framework.
        var services = new ServiceCollection();
        BindCapturedEnvironment<DefaultedOptions>(services, new Dictionary<string, string?>
        {
            ["PORT_DEFAULTED"] = string.Empty
        });

        Assert.Equal(string.Empty, Resolve<DefaultedOptions>(services).Defaulted);
    }

    [Fact]
    public void ValueIsCapturedAtRegistration_NotAtOptionsCreation()
    {
        SetEnvironmentVariable("PORT_VALUE", "captured");

        var services = new ServiceCollection();
        services.AddOptions<ValueOptions>().BindEnvars();

        using var provider = services.BuildServiceProvider();

        Environment.SetEnvironmentVariable("PORT_VALUE", "changed-after-registration");

        Assert.Equal("captured", provider.GetRequiredService<IOptionsFactory<ValueOptions>>().Create(Options.DefaultName).Value);
        Assert.Equal("captured", provider.GetRequiredService<IOptions<ValueOptions>>().Value.Value);
    }
}
