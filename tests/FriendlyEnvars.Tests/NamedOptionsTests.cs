using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using Xunit;

namespace FriendlyEnvars.Tests;

/// <summary>
/// Named options behave exactly as they do for any other options source, with one property that is
/// specific to this library: each <c>BindEnvars</c> call captures the environment separately, so two
/// names of the same type can hold different values and neither changes afterwards.
/// </summary>
public class NamedOptionsTests : EnvarTestsBase
{
    private const string VariableName = "L05_ENDPOINT";

    public class RegionSettings
    {
        [Envar(VariableName)]
        public string Endpoint { get; set; } = "unset";
    }

    /// <summary>
    /// Registers two names of one type against two different captured values, mirroring the sample.
    /// </summary>
    private static ServiceCollection RegisterTwoRegions()
    {
        var services = new ServiceCollection();

        BindCapturedEnvironment<RegionSettings>(
            services, new Dictionary<string, string?> { [VariableName] = "https://eu.example.com" }, "eu");

        BindCapturedEnvironment<RegionSettings>(
            services, new Dictionary<string, string?> { [VariableName] = "https://us.example.com" }, "us");

        return services;
    }

    [Fact]
    public void EachNameIsReachableThroughTheFactory()
    {
        using var provider = RegisterTwoRegions().BuildServiceProvider();
        var factory = provider.GetRequiredService<IOptionsFactory<RegionSettings>>();

        Assert.Equal("https://eu.example.com", factory.Create("eu").Endpoint);
        Assert.Equal("https://us.example.com", factory.Create("us").Endpoint);
    }

    [Fact]
    public void EachNameIsReachableThroughTheMonitor()
    {
        using var provider = RegisterTwoRegions().BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<RegionSettings>>();

        Assert.Equal("https://eu.example.com", monitor.Get("eu").Endpoint);
        Assert.Equal("https://us.example.com", monitor.Get("us").Endpoint);
    }

    [Fact]
    public void EachNameIsReachableThroughAScopedSnapshot()
    {
        using var provider = RegisterTwoRegions().BuildServiceProvider();
        using var scope = provider.CreateScope();

        var snapshot = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<RegionSettings>>();

        Assert.Equal("https://eu.example.com", snapshot.Get("eu").Endpoint);
        Assert.Equal("https://us.example.com", snapshot.Get("us").Endpoint);
    }

    [Fact]
    public void EachNameKeepsTheValueCapturedByItsOwnRegistration_ThroughTheRealEnvironment()
    {
        // The same variable, read twice at two different moments, through the public API.
        SetEnvironmentVariable(VariableName, "https://eu.example.com");

        var services = new ServiceCollection();
        services.AddOptions<RegionSettings>("eu").BindEnvars();

        Environment.SetEnvironmentVariable(VariableName, "https://us.example.com");

        services.AddOptions<RegionSettings>("us").BindEnvars();

        // Changing it again after both registrations changes neither.
        Environment.SetEnvironmentVariable(VariableName, "https://ignored.example.com");

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IOptionsFactory<RegionSettings>>();

        Assert.Equal("https://eu.example.com", factory.Create("eu").Endpoint);
        Assert.Equal("https://us.example.com", factory.Create("us").Endpoint);
    }

    [Fact]
    public void TheDefaultNameIsIndependentOfNamedRegistrations()
    {
        var services = new ServiceCollection();

        BindCapturedEnvironment<RegionSettings>(
            services, new Dictionary<string, string?> { [VariableName] = "https://default.example.com" });

        BindCapturedEnvironment<RegionSettings>(
            services, new Dictionary<string, string?> { [VariableName] = "https://eu.example.com" }, "eu");

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IOptionsFactory<RegionSettings>>();

        Assert.Equal("https://default.example.com", factory.Create(Options.DefaultName).Endpoint);
        Assert.Equal("https://default.example.com", provider.GetRequiredService<IOptions<RegionSettings>>().Value.Endpoint);
        Assert.Equal("https://eu.example.com", factory.Create("eu").Endpoint);
    }

    [Fact]
    public void AnUnregisteredNameGetsTheTypeDefaults()
    {
        using var provider = RegisterTwoRegions().BuildServiceProvider();
        var factory = provider.GetRequiredService<IOptionsFactory<RegionSettings>>();

        // No configurator matches "apac", so nothing is applied to it.
        Assert.Equal("unset", factory.Create("apac").Endpoint);
    }

    [Fact]
    public void RegisteringTheSameNameTwiceIsRejected()
    {
        var services = new ServiceCollection();
        services.AddOptions<RegionSettings>("eu").BindEnvars();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddOptions<RegionSettings>("eu").BindEnvars());

        Assert.Equal(
            $"FriendlyEnvars is already registered for options type '{typeof(RegionSettings).FullName}' and options name 'eu'.",
            exception.Message);
    }

    [Fact]
    public void ConfigurationOrderAppliesPerName()
    {
        var services = new ServiceCollection();

        services.AddOptions<RegionSettings>("eu").Configure(static options => options.Endpoint = "from-code");
        BindCapturedEnvironment<RegionSettings>(
            services, new Dictionary<string, string?> { [VariableName] = "https://eu.example.com" }, "eu");

        BindCapturedEnvironment<RegionSettings>(
            services, new Dictionary<string, string?> { [VariableName] = "https://us.example.com" }, "us");
        services.AddOptions<RegionSettings>("us").Configure(static options => options.Endpoint = "from-code");

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IOptionsFactory<RegionSettings>>();

        // Last registration wins, independently for each name.
        Assert.Equal("https://eu.example.com", factory.Create("eu").Endpoint);
        Assert.Equal("from-code", factory.Create("us").Endpoint);
    }
}
