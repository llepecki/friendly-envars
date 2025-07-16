using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace FriendlyEnvars.Tests;

public class InheritanceTests : EnvarTestsBase
{

    public class BaseOptions
    {
        [Envar("BASE_SETTING")]
        public string BaseSetting { get; set; } = string.Empty;
    }

    public class DerivedOptions : BaseOptions
    {
        [Envar("DERIVED_SETTING")]
        public string DerivedSetting { get; set; } = string.Empty;
    }

    [Fact]
    public void BindFromEnvironmentVariables_WithInheritedClass_ShouldBindBaseProperties()
    {
        SetEnvironmentVariable("BASE_SETTING", "BaseValue");
        SetEnvironmentVariable("DERIVED_SETTING", "DerivedValue");

        var services = new ServiceCollection();
        services.AddOptions<DerivedOptions>()
            .BindFromEnvarAttributes();

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<DerivedOptions>>().Value;

        Assert.Equal("BaseValue", options.BaseSetting);
        Assert.Equal("DerivedValue", options.DerivedSetting);
    }
}
