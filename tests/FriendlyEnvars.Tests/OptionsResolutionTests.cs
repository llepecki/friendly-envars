using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace FriendlyEnvars.Tests;

public class OptionsResolutionTests : EnvarTestsBase
{
    private readonly ServiceProvider _serviceProvider;

    public class TestOptions
    {
        [Required]
        [Envar("TEST_SETTING")]
        public string TestSetting { get; set; } = string.Empty;

        [Envar("OPTIONAL_SETTING")]
        public string OptionalSetting { get; set; } = string.Empty;
    }

    public OptionsResolutionTests()
    {
        SetEnvironmentVariable("TEST_SETTING", "test_value");
        SetEnvironmentVariable("OPTIONAL_SETTING", "optional_value");

        var services = new ServiceCollection();
        services.AddOptions<TestOptions>().BindEnvars();

        _serviceProvider = services.BuildServiceProvider();
    }

    public override void Dispose()
    {
        base.Dispose();
        _serviceProvider.Dispose();
    }

    [Fact]
    public void IOptions_ShouldResolveCorrectly()
    {
        var options = _serviceProvider.GetRequiredService<IOptions<TestOptions>>();

        Assert.NotNull(options);
        Assert.NotNull(options.Value);
        Assert.Equal("test_value", options.Value.TestSetting);
        Assert.Equal("optional_value", options.Value.OptionalSetting);
    }

    [Fact]
    public void IOptions_ShouldReturnSameInstanceOnMultipleResolves()
    {
        var options1 = _serviceProvider.GetRequiredService<IOptions<TestOptions>>();
        var options2 = _serviceProvider.GetRequiredService<IOptions<TestOptions>>();

        Assert.Same(options1, options2);
        Assert.Same(options1.Value, options2.Value);
    }

    [Fact]
    public void IOptionsSnapshot_ShouldResolveNormally()
    {
        using var scope = _serviceProvider.CreateScope();
        var snapshot = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<TestOptions>>();

        Assert.NotNull(snapshot);
        Assert.Equal("test_value", snapshot.Value.TestSetting);
        Assert.Equal("optional_value", snapshot.Value.OptionalSetting);
    }

    [Fact]
    public void IOptionsMonitor_ShouldResolveNormally()
    {
        var monitor = _serviceProvider.GetRequiredService<IOptionsMonitor<TestOptions>>();

        Assert.NotNull(monitor);
        Assert.Equal("test_value", monitor.CurrentValue.TestSetting);
        Assert.Equal("optional_value", monitor.CurrentValue.OptionalSetting);
    }

    [Fact]
    public void IOptionsFactory_ShouldResolveNormally()
    {
        var factory = _serviceProvider.GetRequiredService<IOptionsFactory<TestOptions>>();
        var options = factory.Create(Options.DefaultName);

        Assert.NotNull(options);
        Assert.Equal("test_value", options.TestSetting);
        Assert.Equal("optional_value", options.OptionalSetting);
    }

    [Fact]
    public void NamedOptions_IOptionsFactory_ShouldResolveNormally()
    {
        var services = new ServiceCollection();
        services.AddOptions<TestOptions>("MyName").BindEnvars();

        using var serviceProvider = services.BuildServiceProvider();

        var optionsFactory = serviceProvider.GetRequiredService<IOptionsFactory<TestOptions>>();
        var namedOptions = optionsFactory.Create("MyName");

        Assert.NotNull(namedOptions);
        Assert.Equal("test_value", namedOptions.TestSetting);
        Assert.Equal("optional_value", namedOptions.OptionalSetting);
    }

    [Fact]
    public void NamedOptions_IOptionsSnapshot_ShouldResolveNormally()
    {
        var services = new ServiceCollection();
        services.AddOptions<TestOptions>("MyName").BindEnvars();

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var snapshot = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<TestOptions>>();
        var namedOptions = snapshot.Get("MyName");

        Assert.NotNull(namedOptions);
        Assert.Equal("test_value", namedOptions.TestSetting);
        Assert.Equal("optional_value", namedOptions.OptionalSetting);
    }

    [Fact]
    public void NamedOptions_IOptionsMonitor_ShouldResolveNormally()
    {
        var services = new ServiceCollection();
        services.AddOptions<TestOptions>("MyName").BindEnvars();

        using var serviceProvider = services.BuildServiceProvider();

        var monitor = serviceProvider.GetRequiredService<IOptionsMonitor<TestOptions>>();
        var namedOptions = monitor.Get("MyName");

        Assert.NotNull(namedOptions);
        Assert.Equal("test_value", namedOptions.TestSetting);
        Assert.Equal("optional_value", namedOptions.OptionalSetting);
    }

    [Fact]
    public void MultipleOptionsTypes_ShouldWorkIndependently()
    {
        var services = new ServiceCollection();
        services.AddOptions<TestOptions>().BindEnvars();

        // Add another options type that doesn't use FriendlyEnvars
        services.Configure<AnotherOptions>(static opts => opts.SomeProperty = "configured");

        using var serviceProvider = services.BuildServiceProvider();

        // FriendlyEnvars options should work
        var friendlyOptions = serviceProvider.GetRequiredService<IOptions<TestOptions>>();
        Assert.Equal("test_value", friendlyOptions.Value.TestSetting);

        // Regular options should work normally
        var regularOptions = serviceProvider.GetRequiredService<IOptions<AnotherOptions>>();
        Assert.Equal("configured", regularOptions.Value.SomeProperty);

        // FriendlyEnvars options resolve through snapshot and monitor as well
        var friendlySnapshot = serviceProvider.GetRequiredService<IOptionsSnapshot<TestOptions>>();
        Assert.NotNull(friendlySnapshot);
        Assert.Equal("test_value", friendlySnapshot.Value.TestSetting);

        // Regular options should work with snapshot/monitor
        var regularSnapshot = serviceProvider.GetRequiredService<IOptionsSnapshot<AnotherOptions>>();
        Assert.NotNull(regularSnapshot);
        Assert.Equal("configured", regularSnapshot.Value.SomeProperty);
    }

    public class AnotherOptions
    {
        public string SomeProperty { get; set; } = string.Empty;
    }
}
