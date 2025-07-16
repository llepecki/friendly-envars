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
        // Don't set any environment variables

        var services = new ServiceCollection();
        services.AddOptions<DefaultValueOptions>()
            .BindFromEnvarAttributes();

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<DefaultValueOptions>>().Value;

        Assert.Equal("DefaultString", options.StringWithDefaultValue);
        Assert.Equal(42, options.IntWithDefaultValue);
        Assert.True(options.BoolWithDefaultValue);
        Assert.Equal("UntouchedDefault", options.NoEnvarAttributeProperty);
    }

    [Fact]
    public void BindFromEnvironmentVariables_WithEmptyValues_ShouldSkipBinding()
    {
        SetEnvironmentVariable("DEFAULT_STRING", "");
        SetEnvironmentVariable("DEFAULT_INT", "");
        SetEnvironmentVariable("DEFAULT_BOOL", "");

        var services = new ServiceCollection();
        services.AddOptions<DefaultValueOptions>()
            .BindFromEnvarAttributes();

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<DefaultValueOptions>>().Value;

        Assert.Equal("DefaultString", options.StringWithDefaultValue);
        Assert.Equal(42, options.IntWithDefaultValue);
        Assert.True(options.BoolWithDefaultValue);
    }

    [Fact]
    public void BindFromEnvironmentVariables_WithSomeSetValues_ShouldOverrideOnlyThose()
    {
        SetEnvironmentVariable("DEFAULT_STRING", "NewString");
        // Don't set DEFAULT_INT
        SetEnvironmentVariable("DEFAULT_BOOL", "false");

        var services = new ServiceCollection();
        services.AddOptions<DefaultValueOptions>()
            .BindFromEnvarAttributes();

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<DefaultValueOptions>>().Value;

        Assert.Equal("NewString", options.StringWithDefaultValue);
        Assert.Equal(42, options.IntWithDefaultValue); // Default preserved
        Assert.False(options.BoolWithDefaultValue); // Overridden
        Assert.Equal("UntouchedDefault", options.NoEnvarAttributeProperty);
    }
}
