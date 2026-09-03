using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace FriendlyEnvars.Tests;

public class DefaultValueTests : EnvarTestsBase
{
    public class DefaultValueOptions
    {
        [Envar("DEFAULT_STRING")]
        public string StringWithDefaultValue { get; set; } = "DefaultString";

        [Envar("DEFAULT_INT")]
        public int IntWithDefaultValue { get; set; } = 42;

        [Envar("DEFAULT_BOOL")]
        public bool BoolWithDefaultValue { get; set; } = true;

        public string NoEnvarAttributeProperty { get; set; } = "UntouchedDefault";
    }

    [Fact]
    public void BindFromEnvironmentVariables_WithMissingEnvars_ShouldPreserveDefaults()
    {
        var services = new ServiceCollection();
        services.AddOptions<DefaultValueOptions>()
            .BindEnvars();

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<DefaultValueOptions>>().Value;

        Assert.Equal("DefaultString", options.StringWithDefaultValue);
        Assert.Equal(42, options.IntWithDefaultValue);
        Assert.True(options.BoolWithDefaultValue);
        Assert.Equal("UntouchedDefault", options.NoEnvarAttributeProperty);
    }

    [Fact]
    public void BindFromEnvironmentVariables_WithEmptyStringValue_ShouldBindEmptyString()
    {
        SetEnvironmentVariable("DEFAULT_STRING", "");

        var services = new ServiceCollection();
        services.AddOptions<DefaultValueOptions>()
            .BindEnvars();

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<DefaultValueOptions>>().Value;

        Assert.Equal("", options.StringWithDefaultValue);
        Assert.Equal(42, options.IntWithDefaultValue);
        Assert.True(options.BoolWithDefaultValue);
    }

    [Fact]
    public void BindFromEnvironmentVariables_WithEmptyNonStringValue_ShouldThrow()
    {
        SetEnvironmentVariable("DEFAULT_INT", "");

        var services = new ServiceCollection();
        services.AddOptions<DefaultValueOptions>()
            .BindEnvars();

        var serviceProvider = services.BuildServiceProvider();
        var exception = Assert.Throws<EnvarsException>(() => serviceProvider.GetRequiredService<IOptions<DefaultValueOptions>>().Value);

        Assert.Equal(EnvarFailureKind.Conversion, exception.FailureKind);
        Assert.Contains("DEFAULT_INT", exception.Message);
        Assert.Contains("System.Int32", exception.Message);
    }

    [Fact]
    public void BindFromEnvironmentVariables_WithSomeSetValues_ShouldOverrideOnlyThose()
    {
        SetEnvironmentVariable("DEFAULT_STRING", "NewString");
        SetEnvironmentVariable("DEFAULT_BOOL", "false");

        var services = new ServiceCollection();
        services.AddOptions<DefaultValueOptions>()
            .BindEnvars();

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<DefaultValueOptions>>().Value;

        Assert.Equal("NewString", options.StringWithDefaultValue);
        Assert.Equal(42, options.IntWithDefaultValue); // Default preserved
        Assert.False(options.BoolWithDefaultValue); // Overridden
        Assert.Equal("UntouchedDefault", options.NoEnvarAttributeProperty);
    }
}
