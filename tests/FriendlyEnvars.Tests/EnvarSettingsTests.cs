using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Globalization;
using Xunit;

namespace FriendlyEnvars.Tests;

public class EnvarSettingsTests : EnvarTestsBase
{
    public class NumericOptions
    {
        [Envar("M03_NUMBER")]
        public double Number { get; set; }
    }

    public class NoDecoratedProperties
    {
        public string Untouched { get; set; } = "untouched";
    }

    private sealed class RecordingBinder : IEnvarPropertyBinder
    {
        private readonly DefaultEnvarPropertyBinder _default = new();

        public CultureInfo? LastCulture { get; private set; }

        public object? Convert(string value, Type targetType, CultureInfo culture)
        {
            LastCulture = culture;
            return _default.Convert(value, targetType, culture);
        }
    }

    [Fact]
    public void NullBinder_ThrowsSynchronouslyFromBindEnvars_WithTheParameterName()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentNullException>(
            () => services.AddOptions<NumericOptions>().BindEnvars(
                static settings => settings.UseCustomEnvarPropertyBinder(null!)));

        Assert.Equal("binder", exception.ParamName);
    }

    [Fact]
    public void NullCulture_ThrowsSynchronouslyFromBindEnvars_WithTheParameterName()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentNullException>(
            () => services.AddOptions<NumericOptions>().BindEnvars(
                static settings => settings.UseCulture(null!)));

        Assert.Equal("culture", exception.ParamName);
    }

    [Fact]
    public void NullBinder_ThrowsEvenWhenTheTypeHasNoDecoratedProperties()
    {
        // The guard is on configuration, not on there being something to bind.
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentNullException>(
            () => services.AddOptions<NoDecoratedProperties>().BindEnvars(
                static settings => settings.UseCustomEnvarPropertyBinder(null!)));

        Assert.Equal("binder", exception.ParamName);
    }

    [Fact]
    public void NullCulture_ThrowsEvenWhenTheTypeHasNoDecoratedProperties()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentNullException>(
            () => services.AddOptions<NoDecoratedProperties>().BindEnvars(
                static settings => settings.UseCulture(null!)));

        Assert.Equal("culture", exception.ParamName);
    }

    [Fact]
    public void NullBinder_LeavesNothingRegistered()
    {
        var services = new ServiceCollection();
        var builder = services.AddOptions<NumericOptions>();
        int descriptorCountBeforeBind = services.Count;

        Assert.Throws<ArgumentNullException>(
            () => builder.BindEnvars(static settings => settings.UseCustomEnvarPropertyBinder(null!)));

        Assert.Equal(descriptorCountBeforeBind, services.Count);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IConfigureOptions<NumericOptions>));
    }

    [Fact]
    public void MutatingTheSuppliedCultureAfterRegistration_CannotAlterParsing()
    {
        SetEnvironmentVariable("M03_NUMBER", "1.5");

        // A constructed CultureInfo is mutable, unlike the cached instance GetCultureInfo returns.
        var culture = new CultureInfo("en-US");

        var services = new ServiceCollection();
        services.AddOptions<NumericOptions>().BindEnvars(settings => settings.UseCulture(culture));

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IOptionsFactory<NumericOptions>>();

        Assert.Equal(1.5d, factory.Create(Options.DefaultName).Number);

        // Swapping the decimal separator would make "1.5" parse as 15 if the live instance were used.
        culture.NumberFormat.NumberDecimalSeparator = ",";
        culture.NumberFormat.NumberGroupSeparator = ".";

        Assert.Equal(1.5d, factory.Create(Options.DefaultName).Number);
        Assert.Equal(1.5d, provider.GetRequiredService<IOptions<NumericOptions>>().Value.Number);
    }

    [Fact]
    public void TheCapturedCultureIsAReadOnlyCloneOfTheSuppliedOne()
    {
        SetEnvironmentVariable("M03_NUMBER", "1.5");

        var culture = new CultureInfo("en-US");
        var binder = new RecordingBinder();

        var services = new ServiceCollection();
        services.AddOptions<NumericOptions>().BindEnvars(settings => settings
            .UseCulture(culture)
            .UseCustomEnvarPropertyBinder(binder));

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IOptions<NumericOptions>>().Value;

        Assert.NotNull(binder.LastCulture);

        // A clone, not the caller's instance, and frozen so nothing can mutate it later.
        Assert.NotSame(culture, binder.LastCulture);
        Assert.True(binder.LastCulture!.IsReadOnly);
        Assert.Equal(culture.Name, binder.LastCulture.Name);
        Assert.Throws<InvalidOperationException>(() => binder.LastCulture.NumberFormat.NumberDecimalSeparator = ",");
    }

    [Fact]
    public void MutatingASuppliedNeutralCultureAfterRegistration_CannotAlterParsing()
    {
        SetEnvironmentVariable("M03_NUMBER", "1.5");

        // CultureInfo.ReadOnly only freezes NumberFormat and DateTimeFormat when the culture is NOT
        // neutral, so freezing a neutral culture without cloning it first would leave the caller's
        // mutable NumberFormatInfo shared with the registration. This pins the Clone().
        var culture = new CultureInfo("en");
        _ = culture.NumberFormat;

        var services = new ServiceCollection();
        services.AddOptions<NumericOptions>().BindEnvars(settings => settings.UseCulture(culture));

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IOptionsFactory<NumericOptions>>();

        Assert.Equal(1.5d, factory.Create(Options.DefaultName).Number);

        culture.NumberFormat.NumberDecimalSeparator = ",";
        culture.NumberFormat.NumberGroupSeparator = ".";

        Assert.Equal(1.5d, factory.Create(Options.DefaultName).Number);
    }

    [Fact]
    public void RetainingAndReconfiguringTheSettingsObjectAfterRegistration_CannotAlterParsing()
    {
        SetEnvironmentVariable("M03_NUMBER", "1.5");

        EnvarSettings? retained = null;
        var replacementBinder = new RecordingBinder();

        var services = new ServiceCollection();
        services.AddOptions<NumericOptions>().BindEnvars(settings =>
        {
            retained = settings;
            settings.UseCulture(new CultureInfo("en-US"));
        });

        Assert.NotNull(retained);

        // The settings object is not consulted again after BindEnvars returns.
        retained!.UseCulture(new CultureInfo("de-DE"));
        retained.UseCustomEnvarPropertyBinder(replacementBinder);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IOptionsFactory<NumericOptions>>();

        Assert.Equal(1.5d, factory.Create(Options.DefaultName).Number);
        Assert.Null(replacementBinder.LastCulture);
    }

    [Fact]
    public void TheDefaultCultureIsInvariant()
    {
        SetEnvironmentVariable("M03_NUMBER", "1.5");

        var binder = new RecordingBinder();

        var services = new ServiceCollection();
        services.AddOptions<NumericOptions>().BindEnvars(settings => settings.UseCustomEnvarPropertyBinder(binder));

        using var provider = services.BuildServiceProvider();

        Assert.Equal(1.5d, provider.GetRequiredService<IOptions<NumericOptions>>().Value.Number);
        Assert.Equal(CultureInfo.InvariantCulture.Name, binder.LastCulture!.Name);
    }

    [Fact]
    public void ConfigurationMethodsChainOnTheSameInstance()
    {
        var services = new ServiceCollection();
        EnvarSettings? first = null;

        services.AddOptions<NumericOptions>().BindEnvars(settings =>
        {
            first = settings;

            // Documented as returning the same instance rather than a new one.
            Assert.Same(settings, settings.UseCulture(CultureInfo.InvariantCulture));
            Assert.Same(settings, settings.UseCustomEnvarPropertyBinder(new DefaultEnvarPropertyBinder()));
        });

        Assert.NotNull(first);
    }
}
