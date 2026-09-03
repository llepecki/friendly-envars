using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using Xunit;

namespace FriendlyEnvars.Tests;

/// <summary>
/// Covers which decorated property shapes are accepted as bind targets, and proves that an unsupported
/// shape is rejected while <c>BindEnvars</c> runs rather than lazily when options are first created.
/// </summary>
/// <remarks>
/// Every rejection case is asserted for all three states the environment variable can be in - missing,
/// intentionally empty, and populated - because the shape is a property of the type, not of the
/// environment. The captured-environment seam is used so an empty value is expressible identically on
/// every supported target framework.
/// </remarks>
public class PropertyAccessibilityTests : EnvarTestsBase
{
    private const string VariableName = "M01_VALUE";

    // ----- Unsupported shapes -----

    public class GetterOnlyOptions
    {
        [Envar(VariableName)]
        public string Value { get; } = "untouched";
    }

    public class PrivateSetOptions
    {
        [Envar(VariableName)]
        public string Value { get; private set; } = "untouched";
    }

    public class ProtectedSetOptions
    {
        [Envar(VariableName)]
        public string Value { get; protected set; } = "untouched";
    }

    public class InternalSetOptions
    {
        [Envar(VariableName)]
        public string Value { get; internal set; } = "untouched";
    }

    public class StaticPropertyOptions
    {
        [Envar(VariableName)]
        public static string Value { get; set; } = "untouched";
    }

    public class IndexerOptions
    {
        [Envar(VariableName)]
        public string this[int index]
        {
            get => "untouched";
            set => _ = value;
        }
    }

    // ----- Supported shapes -----

    public class PublicSetOptions
    {
        [Envar(VariableName)]
        public string Value { get; set; } = "default";
    }

    public class InitOnlyOptions
    {
        [Envar(VariableName)]
        public string Value { get; init; } = "default";
    }

    public class PrivateGetPublicSetOptions
    {
        private string _value = "default";

        [Envar(VariableName)]
        public string Value
        {
            private get => _value;
            set => _value = value;
        }

        public string Read() => _value;
    }

    public class BaseWithPublicSetter
    {
        [Envar("M01_INHERITED")]
        public string Inherited { get; set; } = "default";
    }

    public class DerivedFromPublicSetter : BaseWithPublicSetter
    {
        [Envar(VariableName)]
        public string Value { get; set; } = "default";
    }

    public class SkippedPropertyOptions
    {
        [Envar("M01_WITH_ATTRIBUTE")]
        public string WithAttribute { get; set; } = string.Empty;

        public string WithoutAttribute { get; set; } = "DefaultUnchanged";

        // Undecorated shapes that would be rejected if they were decorated must simply be ignored.
        public string GetterOnly { get; } = "ignored";

        public static string Static { get; set; } = "ignored";
    }

    private static EnvarsException AssertShapeRejected<T>(string? capturedValue, string expectedPropertyName)
        where T : class, new()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<EnvarsException>(() => BindCapturedEnvironment<T>(
            services, new Dictionary<string, string?> { [VariableName] = capturedValue }));

        Assert.Equal(EnvarFailureKind.InvalidProperty, exception.FailureKind);
        Assert.Equal(typeof(T), exception.OptionsType);
        Assert.Equal(Options.DefaultName, exception.OptionsName);
        Assert.Equal(expectedPropertyName, exception.PropertyName);
        Assert.Equal(VariableName, exception.EnvironmentVariableName);
        Assert.Null(exception.CauseType);
        Assert.Null(exception.InnerException);

        // The failure never mentions the value, whatever the value happened to be.
        if (!string.IsNullOrEmpty(capturedValue))
        {
            Assert.DoesNotContain(capturedValue, exception.Message, System.StringComparison.Ordinal);
            Assert.DoesNotContain(capturedValue, exception.ToString(), System.StringComparison.Ordinal);
        }

        // A failed registration adds nothing to the service collection.
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IConfigureOptions<T>));

        return exception;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("populated")]
    public void GetterOnlyProperty_IsRejected(string? capturedValue)
    {
        AssertShapeRejected<GetterOnlyOptions>(capturedValue, nameof(GetterOnlyOptions.Value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("populated")]
    public void PrivateSetProperty_IsRejected(string? capturedValue)
    {
        AssertShapeRejected<PrivateSetOptions>(capturedValue, nameof(PrivateSetOptions.Value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("populated")]
    public void ProtectedSetProperty_IsRejected(string? capturedValue)
    {
        AssertShapeRejected<ProtectedSetOptions>(capturedValue, nameof(ProtectedSetOptions.Value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("populated")]
    public void InternalSetProperty_IsRejected(string? capturedValue)
    {
        AssertShapeRejected<InternalSetOptions>(capturedValue, nameof(InternalSetOptions.Value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("populated")]
    public void StaticProperty_IsRejected_AndIsNeverMutated(string? capturedValue)
    {
        AssertShapeRejected<StaticPropertyOptions>(capturedValue, nameof(StaticPropertyOptions.Value));

        // Assigning a static property would mutate state shared by every options instance.
        Assert.Equal("untouched", StaticPropertyOptions.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("populated")]
    public void Indexer_IsRejected(string? capturedValue)
    {
        // The compiler names an indexer "Item" unless [IndexerName] says otherwise.
        AssertShapeRejected<IndexerOptions>(capturedValue, "Item");
    }

    [Fact]
    public void PublicSetter_Binds()
    {
        var services = new ServiceCollection();
        BindCapturedEnvironment<PublicSetOptions>(services, new Dictionary<string, string?> { [VariableName] = "bound" });

        using var provider = services.BuildServiceProvider();

        Assert.Equal("bound", provider.GetRequiredService<IOptions<PublicSetOptions>>().Value.Value);
    }

    [Fact]
    public void InitOnlySetter_Binds()
    {
        var services = new ServiceCollection();
        BindCapturedEnvironment<InitOnlyOptions>(services, new Dictionary<string, string?> { [VariableName] = "bound" });

        using var provider = services.BuildServiceProvider();

        Assert.Equal("bound", provider.GetRequiredService<IOptions<InitOnlyOptions>>().Value.Value);
    }

    [Fact]
    public void PublicSetterWithANonPublicGetter_Binds()
    {
        // Only the set accessor's visibility is part of the rule.
        var services = new ServiceCollection();
        BindCapturedEnvironment<PrivateGetPublicSetOptions>(services, new Dictionary<string, string?> { [VariableName] = "bound" });

        using var provider = services.BuildServiceProvider();

        Assert.Equal("bound", provider.GetRequiredService<IOptions<PrivateGetPublicSetOptions>>().Value.Read());
    }

    [Fact]
    public void InheritedPublicProperty_Binds()
    {
        var services = new ServiceCollection();
        BindCapturedEnvironment<DerivedFromPublicSetter>(services, new Dictionary<string, string?>
        {
            [VariableName] = "bound",
            ["M01_INHERITED"] = "inherited"
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<DerivedFromPublicSetter>>().Value;

        Assert.Equal("bound", options.Value);
        Assert.Equal("inherited", options.Inherited);
    }

    [Fact]
    public void UndecoratedPropertiesAreIgnoredWhateverTheirShape()
    {
        var services = new ServiceCollection();
        BindCapturedEnvironment<SkippedPropertyOptions>(services, new Dictionary<string, string?>
        {
            ["M01_WITH_ATTRIBUTE"] = "ChangedValue"
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<SkippedPropertyOptions>>().Value;

        Assert.Equal("ChangedValue", options.WithAttribute);
        Assert.Equal("DefaultUnchanged", options.WithoutAttribute);
        Assert.Equal("ignored", options.GetterOnly);
        Assert.Equal("ignored", SkippedPropertyOptions.Static);
    }

    [Fact]
    public void UnsupportedShapeIsRejectedThroughThePublicApiToo()
    {
        // The rule is not an artefact of the internal test seam.
        SetEnvironmentVariable(VariableName, "populated");

        var services = new ServiceCollection();

        var exception = Assert.Throws<EnvarsException>(() => services.AddOptions<GetterOnlyOptions>().BindEnvars());

        Assert.Equal(EnvarFailureKind.InvalidProperty, exception.FailureKind);
        Assert.Equal(nameof(GetterOnlyOptions.Value), exception.PropertyName);
        Assert.Contains("is not a supported bind target", exception.Message, System.StringComparison.Ordinal);
    }
}
