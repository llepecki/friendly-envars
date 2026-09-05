using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using Xunit;

namespace FriendlyEnvars.Tests;

public class PrecedenceTests : EnvarTestsBase
{
    public class Options
    {
        [Envar("L04_VALUE")]
        public string Value { get; set; } = "type-default";

        [Envar("L04_ABSENT")]
        public string Absent { get; set; } = "type-default";
    }

    private static Options Resolve(ServiceCollection services, string optionsName)
    {
        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptionsFactory<Options>>().Create(optionsName);
    }

    private static void BindPresentValue(ServiceCollection services, string optionsName) =>
        BindCapturedEnvironment<Options>(
            services, new Dictionary<string, string?> { ["L04_VALUE"] = "from-environment" }, optionsName);

    private static void BindWithNothingSet(ServiceCollection services, string optionsName) =>
        BindCapturedEnvironment<Options>(services, new Dictionary<string, string?>(), optionsName);

    [Theory]
    [InlineData("")]
    [InlineData("named")]
    public void ConfigurationRegisteredBeforeBindEnvars_IsOverwrittenByAPresentEnvironmentValue(string optionsName)
    {
        var services = new ServiceCollection();

        services.AddOptions<Options>(optionsName).Configure(static options => options.Value = "from-configure");
        BindPresentValue(services, optionsName);

        Assert.Equal("from-environment", Resolve(services, optionsName).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("named")]
    public void ConfigurationRegisteredAfterBindEnvars_OverwritesTheEnvironmentValue(string optionsName)
    {
        var services = new ServiceCollection();

        BindPresentValue(services, optionsName);
        services.AddOptions<Options>(optionsName).Configure(static options => options.Value = "from-configure");

        Assert.Equal("from-configure", Resolve(services, optionsName).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("named")]
    public void AnAbsentEnvironmentValueDoesNotOverwriteEarlierConfiguration(string optionsName)
    {
        var services = new ServiceCollection();

        services.AddOptions<Options>(optionsName).Configure(static options => options.Absent = "from-configure");
        BindPresentValue(services, optionsName);

        // L04_ABSENT was not captured, so that property is skipped entirely rather than reset.
        Assert.Equal("from-configure", Resolve(services, optionsName).Absent);
        Assert.Equal("from-environment", Resolve(services, optionsName).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("named")]
    public void WithNothingCapturedTheTypeDefaultsAndEarlierConfigurationBothSurvive(string optionsName)
    {
        var services = new ServiceCollection();

        services.AddOptions<Options>(optionsName).Configure(static options => options.Value = "from-configure");
        BindWithNothingSet(services, optionsName);

        Assert.Equal("from-configure", Resolve(services, optionsName).Value);
        Assert.Equal("type-default", Resolve(services, optionsName).Absent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("named")]
    public void ConfigurationOnEitherSideComposesInRegistrationOrder(string optionsName)
    {
        var services = new ServiceCollection();

        services.AddOptions<Options>(optionsName).Configure(static options => options.Value = "first");
        BindPresentValue(services, optionsName);
        services.AddOptions<Options>(optionsName).Configure(static options => options.Value += "-last");

        // Environment overwrote "first", then the later Configure appended to it.
        Assert.Equal("from-environment-last", Resolve(services, optionsName).Value);
    }

    [Fact]
    public void NamedRegistrationsDoNotInterfereWithTheDefaultOne()
    {
        var services = new ServiceCollection();

        services.AddOptions<Options>().Configure(static options => options.Value = "default-configure");
        BindPresentValue(services, "named");

        Assert.Equal("default-configure", Resolve(services, "").Value);
        Assert.Equal("from-environment", Resolve(services, "named").Value);
    }

    [Fact]
    public void TheLibraryRegistersNoPostConfigureStep()
    {
        var services = new ServiceCollection();
        BindPresentValue(services, "");

        // A PostConfigure registration would run after every Configure and force the environment to win,
        // which is exactly the precedence rule this library declines to invent.
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IPostConfigureOptions<Options>));
    }
}
