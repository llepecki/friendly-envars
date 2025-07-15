using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace FriendlyEnvars.Tests;

public class OptionsResolutionTests : IDisposable
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
        Environment.SetEnvironmentVariable("TEST_SETTING", "test_value");
        Environment.SetEnvironmentVariable("OPTIONAL_SETTING", "optional_value");

        var services = new ServiceCollection();
        services.AddOptions<TestOptions>().BindFromEnvarAttributes();

        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("TEST_SETTING", null);
        Environment.SetEnvironmentVariable("OPTIONAL_SETTING", null);
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
    public void IOptionsSnapshot_ShouldThrowNotSupportedException_ByDefault()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            _serviceProvider.GetRequiredService<IOptionsSnapshot<TestOptions>>());

        Assert.Contains("IOptionsSnapshot<TestOptions>", exception.Message);
        Assert.Contains("not supported", exception.Message);
        Assert.Contains("Use IOptions<T> instead", exception.Message);
        Assert.Contains("environment variables are static", exception.Message);
    }

    [Fact]
    public void IOptionsMonitor_ShouldThrowNotSupportedException_ByDefault()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            _serviceProvider.GetRequiredService<IOptionsMonitor<TestOptions>>());

        Assert.Contains("IOptionsMonitor<TestOptions>", exception.Message);
        Assert.Contains("not supported", exception.Message);
        Assert.Contains("Use IOptions<T> instead", exception.Message);
        Assert.Contains("environment variables are static", exception.Message);
    }

    [Fact]
    public void NamedOptions_IOptions_ShouldResolveCorrectly()
    {
        var services = new ServiceCollection();
        services.AddOptions<TestOptions>("MyName").BindFromEnvarAttributes();

        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptionsFactory<TestOptions>>();
        var namedOptions = options.Create("MyName");

        Assert.NotNull(namedOptions);
        Assert.Equal("test_value", namedOptions.TestSetting);
        Assert.Equal("optional_value", namedOptions.OptionalSetting);
    }

    [Fact]
    public void NamedOptions_IOptionsSnapshot_ShouldThrowNotSupportedException()
    {
        var services = new ServiceCollection();
        services.AddOptions<TestOptions>("MyName").BindFromEnvarAttributes();

        using var serviceProvider = services.BuildServiceProvider();

        var exception = Assert.Throws<NotSupportedException>(() =>
            serviceProvider.GetRequiredService<IOptionsSnapshot<TestOptions>>());

        Assert.Contains("IOptionsSnapshot<TestOptions>", exception.Message);
        Assert.Contains("not supported", exception.Message);
    }

    [Fact]
    public void NamedOptions_IOptionsMonitor_ShouldThrowNotSupportedException()
    {
        var services = new ServiceCollection();
        services.AddOptions<TestOptions>("MyName").BindFromEnvarAttributes();

        using var serviceProvider = services.BuildServiceProvider();

        var exception = Assert.Throws<NotSupportedException>(() =>
            serviceProvider.GetRequiredService<IOptionsMonitor<TestOptions>>());

        Assert.Contains("IOptionsMonitor<TestOptions>", exception.Message);
        Assert.Contains("not supported", exception.Message);
    }

    [Fact]
    public void IOptionsSnapshot_ShouldWork_WhenAllowed()
    {
        var services = new ServiceCollection();
        services.AddOptions<TestOptions>().BindFromEnvarAttributes(settings =>
        {
            settings.AllowOptionsSnapshot();
        });

        using var serviceProvider = services.BuildServiceProvider();

        // Should not throw when allowed
        var snapshot = serviceProvider.GetRequiredService<IOptionsSnapshot<TestOptions>>();
        Assert.NotNull(snapshot);
        Assert.Equal("test_value", snapshot.Value.TestSetting);
        Assert.Equal("optional_value", snapshot.Value.OptionalSetting);
    }

    [Fact]
    public void IOptionsMonitor_ShouldWork_WhenAllowed()
    {
        var services = new ServiceCollection();
        services.AddOptions<TestOptions>().BindFromEnvarAttributes(settings =>
        {
            settings.AllowOptionsMonitor();
        });

        using var serviceProvider = services.BuildServiceProvider();

        // Should not throw when allowed
        var monitor = serviceProvider.GetRequiredService<IOptionsMonitor<TestOptions>>();
        Assert.NotNull(monitor);
        Assert.Equal("test_value", monitor.CurrentValue.TestSetting);
        Assert.Equal("optional_value", monitor.CurrentValue.OptionalSetting);
    }

    [Fact]
    public void BothSnapshotAndMonitor_ShouldWork_WhenBothAllowed()
    {
        var services = new ServiceCollection();
        services.AddOptions<TestOptions>().BindFromEnvarAttributes(settings =>
        {
            settings.AllowOptionsSnapshot().AllowOptionsMonitor();
        });

        using var serviceProvider = services.BuildServiceProvider();

        // Both should work
        var snapshot = serviceProvider.GetRequiredService<IOptionsSnapshot<TestOptions>>();
        var monitor = serviceProvider.GetRequiredService<IOptionsMonitor<TestOptions>>();

        Assert.NotNull(snapshot);
        Assert.NotNull(monitor);
        Assert.Equal("test_value", snapshot.Value.TestSetting);
        Assert.Equal("test_value", monitor.CurrentValue.TestSetting);
    }

    [Fact]
    public void MultipleOptionsTypes_ShouldWorkIndependently()
    {
        var services = new ServiceCollection();
        services.AddOptions<TestOptions>().BindFromEnvarAttributes();

        // Add another options type that doesn't use FriendlyEnvars
        services.Configure<AnotherOptions>(opts => opts.SomeProperty = "configured");

        using var serviceProvider = services.BuildServiceProvider();

        // FriendlyEnvars options should work
        var friendlyOptions = serviceProvider.GetRequiredService<IOptions<TestOptions>>();
        Assert.Equal("test_value", friendlyOptions.Value.TestSetting);

        // Regular options should work normally
        var regularOptions = serviceProvider.GetRequiredService<IOptions<AnotherOptions>>();
        Assert.Equal("configured", regularOptions.Value.SomeProperty);

        // Only FriendlyEnvars options should throw for snapshot/monitor
        Assert.Throws<NotSupportedException>(() =>
            serviceProvider.GetRequiredService<IOptionsSnapshot<TestOptions>>());

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
