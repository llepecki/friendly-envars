using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace FriendlyEnvars.Tests;

public class PropertyAccessibilityTests : EnvarTestsBase
{

    public class InvalidPropertyOptions
    {
        [Envar("READONLY_PROPERTY")]
        public string ReadOnlyProperty { get; } = string.Empty;
    }

    public class SkippedPropertyOptions
    {
        [Envar("WITH_ATTRIBUTE")]
        public string WithAttribute { get; set; } = string.Empty;

        public string WithoutAttribute { get; set; } = "DefaultUnchanged";
    }

    [Fact]
    public void BindFromEnvironmentVariables_WithReadOnlyProperty_ShouldThrow()
    {
        SetEnvironmentVariable("READONLY_PROPERTY", "Value");

        var services = new ServiceCollection();
        services.AddOptions<InvalidPropertyOptions>()
            .BindEnvars();

        var serviceProvider = services.BuildServiceProvider();
        var exception = Assert.Throws<EnvarsException>(() => serviceProvider.GetRequiredService<IOptions<InvalidPropertyOptions>>().Value);

        Assert.Contains("ReadOnlyProperty", exception.Message);
        Assert.Contains("setter", exception.Message);
    }

    [Fact]
    public void BindFromEnvironmentVariables_WithoutAttribute_ShouldSkipProperty()
    {
        SetEnvironmentVariable("WITH_ATTRIBUTE", "ChangedValue");

        var services = new ServiceCollection();
        services.AddOptions<SkippedPropertyOptions>()
            .BindEnvars();

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<SkippedPropertyOptions>>().Value;

        Assert.Equal("ChangedValue", options.WithAttribute);
        Assert.Equal("DefaultUnchanged", options.WithoutAttribute);
    }
}
