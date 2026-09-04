using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using Xunit;

namespace FriendlyEnvars.Tests;

public class RegistrationTests : EnvarTestsBase
{
    public class TestOptions
    {
        [Envar("L02_VALUE")]
        public string Value { get; set; } = "default";
    }

    public class OtherOptions
    {
        [Envar("L02_OTHER")]
        public string Value { get; set; } = "default";
    }

    private static int CountConfigureOptions<T>(IServiceCollection services) where T : class =>
        services.Count(descriptor => descriptor.ServiceType == typeof(IConfigureOptions<T>));

    [Fact]
    public void ARegistrationAddsExactlyOneConfigurator()
    {
        SetEnvironmentVariable("L02_VALUE", "bound");

        var services = new ServiceCollection();
        services.AddOptions<TestOptions>().BindEnvars();

        // One binding configurator, and no second no-op one alongside it.
        Assert.Equal(1, CountConfigureOptions<TestOptions>(services));
    }

    [Fact]
    public void TheRegisteredConfiguratorActuallyBinds()
    {
        SetEnvironmentVariable("L02_VALUE", "bound");

        var services = new ServiceCollection();
        services.AddOptions<TestOptions>().BindEnvars();

        var configurators = services
            .Where(descriptor => descriptor.ServiceType == typeof(IConfigureOptions<TestOptions>))
            .Select(descriptor => descriptor.ImplementationInstance)
            .OfType<ConfigureNamedOptions<TestOptions>>()
            .ToArray();

        // The single configurator is a real one, not a no-op: running it changes the instance.
        var single = Assert.Single(configurators);
        Assert.Equal(Options.DefaultName, single.Name);

        var options = new TestOptions();
        single.Action!(options);

        Assert.Equal("bound", options.Value);
    }

    [Fact]
    public void ARegistrationAddsExactlyOneMarker()
    {
        var services = new ServiceCollection();
        services.AddOptions<TestOptions>().BindEnvars();

        Assert.Single(services, descriptor => descriptor.ImplementationInstance is FriendlyEnvarsRegistrationMarker);
    }

    [Fact]
    public void RegisteringTheSameTypeAndNameTwiceThrows()
    {
        var services = new ServiceCollection();
        services.AddOptions<TestOptions>().BindEnvars();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddOptions<TestOptions>().BindEnvars());

        Assert.Equal(
            $"FriendlyEnvars is already registered for options type '{typeof(TestOptions).FullName}' and options name '<default>'.",
            exception.Message);
    }

    [Fact]
    public void RegisteringTheSameNamedPairTwiceThrows()
    {
        var services = new ServiceCollection();
        services.AddOptions<TestOptions>("primary").BindEnvars();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddOptions<TestOptions>("primary").BindEnvars());

        Assert.Equal(
            $"FriendlyEnvars is already registered for options type '{typeof(TestOptions).FullName}' and options name 'primary'.",
            exception.Message);
    }

    [Fact]
    public void TheDuplicateMessageEscapesTheOptionsName()
    {
        var services = new ServiceCollection();
        services.AddOptions<TestOptions>("n'a\\me").BindEnvars();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddOptions<TestOptions>("n'a\\me").BindEnvars());

        // Same escaping rule as the exception contract: backslash, apostrophe and control characters.
        Assert.Equal(
            $@"FriendlyEnvars is already registered for options type '{typeof(TestOptions).FullName}' and options name 'n\'a\\me'.",
            exception.Message);
    }

    [Fact]
    public void ARejectedDuplicateLeavesTheServiceCollectionUnchanged()
    {
        var services = new ServiceCollection();
        services.AddOptions<TestOptions>().BindEnvars();

        var builder = services.AddOptions<TestOptions>();
        int descriptorCountBeforeSecondBind = services.Count;

        Assert.Throws<InvalidOperationException>(() => builder.BindEnvars());

        Assert.Equal(descriptorCountBeforeSecondBind, services.Count);
        Assert.Equal(1, CountConfigureOptions<TestOptions>(services));
    }

    [Fact]
    public void DifferentNamesOfTheSameTypeRegisterAndResolveIndependently()
    {
        SetEnvironmentVariable("L02_VALUE", "shared");

        var services = new ServiceCollection();
        services.AddOptions<TestOptions>().BindEnvars();
        services.AddOptions<TestOptions>("primary").BindEnvars();
        services.AddOptions<TestOptions>("secondary").BindEnvars();

        Assert.Equal(3, CountConfigureOptions<TestOptions>(services));

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IOptionsFactory<TestOptions>>();

        Assert.Equal("shared", factory.Create(Options.DefaultName).Value);
        Assert.Equal("shared", factory.Create("primary").Value);
        Assert.Equal("shared", factory.Create("secondary").Value);
    }

    [Fact]
    public void DifferentTypesRegisterIndependently()
    {
        SetEnvironmentVariable("L02_VALUE", "one");
        SetEnvironmentVariable("L02_OTHER", "two");

        var services = new ServiceCollection();
        services.AddOptions<TestOptions>().BindEnvars();
        services.AddOptions<OtherOptions>().BindEnvars();

        using var provider = services.BuildServiceProvider();

        Assert.Equal("one", provider.GetRequiredService<IOptions<TestOptions>>().Value.Value);
        Assert.Equal("two", provider.GetRequiredService<IOptions<OtherOptions>>().Value.Value);
    }

    [Fact]
    public void StandardOptionsRegistrationsForTheSameTypeRemainAllowed()
    {
        SetEnvironmentVariable("L02_VALUE", "bound");

        var services = new ServiceCollection();
        services.AddOptions<TestOptions>().BindEnvars();

        // Only a second FriendlyEnvars registration is rejected; ordinary Configure calls are not.
        services.Configure<TestOptions>(static options => options.Value += "-then-configured");

        using var provider = services.BuildServiceProvider();

        Assert.Equal("bound-then-configured", provider.GetRequiredService<IOptions<TestOptions>>().Value.Value);
    }

    [Fact]
    public void TheDefaultBinderInstanceIsSharedAcrossRegistrations()
    {
        var first = CaptureBinder();
        var second = CaptureBinder();

        // Stateless, so one instance serves every registration rather than one being allocated per call.
        Assert.Same(first, second);
        Assert.IsType<DefaultEnvarPropertyBinder>(first);

        static IEnvarPropertyBinder CaptureBinder()
        {
            IEnvarPropertyBinder? captured = null;

            var services = new ServiceCollection();
            services.AddOptions<TestOptions>().BindEnvars(settings => captured = settings.EnvarPropertyBinder);

            Assert.NotNull(captured);
            return captured!;
        }
    }

    [Fact]
    public void MarkersFromOneCollectionDoNotAffectAnother()
    {
        // The marker is scoped to the collection, so a second container may register the same pair.
        var first = new ServiceCollection();
        first.AddOptions<TestOptions>().BindEnvars();

        var second = new ServiceCollection();
        second.AddOptions<TestOptions>().BindEnvars();

        Assert.Equal(1, CountConfigureOptions<TestOptions>(first));
        Assert.Equal(1, CountConfigureOptions<TestOptions>(second));
    }
}
