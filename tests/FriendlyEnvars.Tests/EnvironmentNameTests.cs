using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using Xunit;

namespace FriendlyEnvars.Tests;

/// <summary>
/// The environment-variable name policy, asserted through both routes that read a name: constructing
/// <see cref="EnvarAttribute"/> directly, and binding, which decodes the name out of metadata without
/// constructing the attribute at all.
/// </summary>
/// <remarks>
/// Marked as a portability contract because what a name may contain is exactly where operating systems
/// disagree. CI runs this class on Ubuntu, Windows and macOS under both target frameworks.
/// </remarks>
[Trait("Category", "Portability")]
public class EnvironmentNameTests : EnvarTestsBase
{
    // ----- Rejected names, as metadata that binding must decode and reject -----

    public class NullName
    {
        [Envar(null!)]
        public string? Value { get; set; }
    }

    public class EmptyName
    {
        [Envar("")]
        public string? Value { get; set; }
    }

    public class SingleSpaceName
    {
        [Envar(" ")]
        public string? Value { get; set; }
    }

    public class ManySpacesName
    {
        [Envar("   ")]
        public string? Value { get; set; }
    }

    public class TabOnlyName
    {
        [Envar("\t")]
        public string? Value { get; set; }
    }

    public class NewlineOnlyName
    {
        [Envar("\n")]
        public string? Value { get; set; }
    }

    public class TabAndNewlineOnlyName
    {
        [Envar("\t\n")]
        public string? Value { get; set; }
    }

    public class NulInsideName
    {
        [Envar("A\0B")]
        public string? Value { get; set; }
    }

    public class NewlineInsideName
    {
        [Envar("A\nB")]
        public string? Value { get; set; }
    }

    public class TabInsideName
    {
        [Envar("A\tB")]
        public string? Value { get; set; }
    }

    public class EqualsInsideName
    {
        [Envar("A=B")]
        public string? Value { get; set; }
    }

    public class EqualsOnlyName
    {
        [Envar("=")]
        public string? Value { get; set; }
    }

    // ----- Accepted names -----

    public class SingleLetterName
    {
        [Envar("A")]
        public string? Value { get; set; }
    }

    public class UnderscoreAndDigitName
    {
        [Envar("A_B1")]
        public string? Value { get; set; }
    }

    public class EmbeddedSpaceName
    {
        [Envar("A B")]
        public string? Value { get; set; }
    }

    public class UnicodeName
    {
        [Envar("Å_VAR")]
        public string? Value { get; set; }
    }

    /// <summary>The exact rejected corpus. Null is passed through a nullable parameter.</summary>
    public static TheoryData<string?> RejectedNames() =>
    [
        null, "", " ", "   ", "\t", "\n", "\t\n", "A\0B", "A\nB", "A\tB", "A=B", "="
    ];

    public static TheoryData<string> AcceptedNames() => ["A", "A_B1", "A B", "Å_VAR"];

    [Theory]
    [MemberData(nameof(RejectedNames))]
    public void DirectConstruction_RejectsTheNameCorpus(string? name)
    {
        var exception = Assert.Throws<ArgumentException>(() => new EnvarAttribute(name!));

        Assert.Equal("name", exception.ParamName);
    }

    [Theory]
    [MemberData(nameof(AcceptedNames))]
    public void DirectConstruction_AcceptsTheNameCorpusAndPreservesItExactly(string name)
    {
        var attribute = new EnvarAttribute(name);

        Assert.Equal(name, attribute.Name);
    }

    [Fact]
    public void DirectConstruction_AcceptsAFourKibibyteName()
    {
        // Long names are a metadata and attribute-construction concern only: operating systems impose
        // different limits on the environment block, so this name is never written to the environment.
        string name = new('A', 4096);

        var attribute = new EnvarAttribute(name);

        Assert.Equal(name, attribute.Name);
        Assert.Equal(4096, attribute.Name.Length);

        // Binding decodes names from metadata and validates them with this same predicate, so accepting
        // the name here means the metadata route accepts it too.
        Assert.True(EnvarAttribute.IsValidName(name));
    }

    private static void AssertBindingRejectsTheName<T>() where T : class, new()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<EnvarsException>(
            () => BindCapturedEnvironment<T>(services, new Dictionary<string, string?>()));

        Assert.Equal(EnvarFailureKind.InvalidProperty, exception.FailureKind);
        Assert.Equal(typeof(T), exception.OptionsType);
        Assert.Equal(Options.DefaultName, exception.OptionsName);
        Assert.Equal("Value", exception.PropertyName);
        Assert.Equal(typeof(string), exception.TargetType);

        // The invalid name itself is never reported, in either the message or the metadata.
        Assert.Null(exception.EnvironmentVariableName);
        Assert.Null(exception.CauseType);
        Assert.Null(exception.CultureName);
        Assert.Null(exception.BinderType);
        Assert.Null(exception.InnerException);

        Assert.Equal(
            $"Property '{typeof(T).FullName}.Value' has an invalid environment-variable name.",
            exception.Message);
    }

    [Fact]
    public void Binding_RejectsANullName() => AssertBindingRejectsTheName<NullName>();

    [Fact]
    public void Binding_RejectsAnEmptyName() => AssertBindingRejectsTheName<EmptyName>();

    [Fact]
    public void Binding_RejectsASingleSpaceName() => AssertBindingRejectsTheName<SingleSpaceName>();

    [Fact]
    public void Binding_RejectsAManySpacesName() => AssertBindingRejectsTheName<ManySpacesName>();

    [Fact]
    public void Binding_RejectsATabOnlyName() => AssertBindingRejectsTheName<TabOnlyName>();

    [Fact]
    public void Binding_RejectsANewlineOnlyName() => AssertBindingRejectsTheName<NewlineOnlyName>();

    [Fact]
    public void Binding_RejectsATabAndNewlineOnlyName() => AssertBindingRejectsTheName<TabAndNewlineOnlyName>();

    [Fact]
    public void Binding_RejectsANulInsideTheName() => AssertBindingRejectsTheName<NulInsideName>();

    [Fact]
    public void Binding_RejectsANewlineInsideTheName() => AssertBindingRejectsTheName<NewlineInsideName>();

    [Fact]
    public void Binding_RejectsATabInsideTheName() => AssertBindingRejectsTheName<TabInsideName>();

    [Fact]
    public void Binding_RejectsAnEqualsInsideTheName() => AssertBindingRejectsTheName<EqualsInsideName>();

    [Fact]
    public void Binding_RejectsAnEqualsOnlyName() => AssertBindingRejectsTheName<EqualsOnlyName>();

    [Fact]
    public void Binding_RejectsAnInvalidNameThroughThePublicApiToo()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<EnvarsException>(() => services.AddOptions<EqualsInsideName>().BindEnvars());

        Assert.Equal(EnvarFailureKind.InvalidProperty, exception.FailureKind);
        Assert.Null(exception.EnvironmentVariableName);
    }

    private static void AssertBindsThroughTheRealEnvironment<T>(string name, Func<T, string?> read)
        where T : class, new()
    {
        // Goes through the process environment rather than the reader seam: which names survive a round
        // trip is exactly the part that differs between operating systems.
        Environment.SetEnvironmentVariable(name, "round-tripped");

        try
        {
            var services = new ServiceCollection();
            services.AddOptions<T>().BindEnvars();

            using var provider = services.BuildServiceProvider();

            Assert.Equal("round-tripped", read(provider.GetRequiredService<IOptions<T>>().Value));
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Fact]
    public void Binding_AcceptsASingleLetterName() =>
        AssertBindsThroughTheRealEnvironment<SingleLetterName>("A", static options => options.Value);

    [Fact]
    public void Binding_AcceptsAnUnderscoreAndDigitName() =>
        AssertBindsThroughTheRealEnvironment<UnderscoreAndDigitName>("A_B1", static options => options.Value);

    [Fact]
    public void Binding_AcceptsAnEmbeddedSpaceInTheName() =>
        AssertBindsThroughTheRealEnvironment<EmbeddedSpaceName>("A B", static options => options.Value);

    [Fact]
    public void Binding_AcceptsANonAsciiName() =>
        AssertBindsThroughTheRealEnvironment<UnicodeName>("Å_VAR", static options => options.Value);

    // ----- The attribute is never constructed during binding -----

    public class ThrowingAttributeShapeOptions
    {
        [Envar("M10_DECODED_FROM_METADATA")]
        public string? Value { get; set; }
    }

    [Fact]
    public void Binding_ReadsTheNameFromMetadataWithoutConstructingTheAttribute()
    {
        var services = new ServiceCollection();
        BindCapturedEnvironment<ThrowingAttributeShapeOptions>(services, new Dictionary<string, string?>
        {
            ["M10_DECODED_FROM_METADATA"] = "decoded"
        });

        using var provider = services.BuildServiceProvider();

        Assert.Equal("decoded", provider.GetRequiredService<IOptions<ThrowingAttributeShapeOptions>>().Value.Value);

        // A type carrying a name the attribute constructor would reject still binds far enough to report
        // a structured failure, which is only possible because the constructor is never invoked.
        var rejected = new ServiceCollection();
        Assert.Throws<EnvarsException>(
            () => BindCapturedEnvironment<EqualsInsideName>(rejected, new Dictionary<string, string?>()));
    }

    // ----- Attribute inheritance must survive the switch to metadata decoding -----

    public abstract class DecoratedBase
    {
        [Envar("M10_OVERRIDDEN")]
        public virtual string? Overridden { get; set; }
    }

    public class OverridingDerived : DecoratedBase
    {
        public override string? Overridden { get; set; }
    }

    /// <summary>A three-level chain with the attribute on the MIDDLE declaration.</summary>
    public abstract class ChainRoot
    {
        public virtual string? Chained { get; set; }
    }

    public class ChainMiddle : ChainRoot
    {
        [Envar("M10_CHAINED")]
        public override string? Chained { get; set; }
    }

    public class ChainLeaf : ChainMiddle
    {
        public override string? Chained { get; set; }
    }

    public class IndexerBase
    {
        [Envar("M10_INDEXER")]
        public virtual string this[int index]
        {
            get => string.Empty;
            set => _ = value;
        }

        public virtual string this[string key]
        {
            get => string.Empty;
            set => _ = value;
        }
    }

    public class IndexerDerived : IndexerBase
    {
        public override string this[int index]
        {
            get => string.Empty;
            set => _ = value;
        }

        public override string this[string key]
        {
            get => string.Empty;
            set => _ = value;
        }
    }

    /// <summary>Both an invalid name and an unsupported shape, to pin which is reported.</summary>
    public class InvalidNameAndShape
    {
        [Envar("A=B")]
        public string Value { get; } = "untouched";
    }

    [Fact]
    public void Binding_FindsAnAttributeDeclaredOnAnIntermediateOverride()
    {
        // The base chain must be walked one level at a time. MethodInfo.GetBaseDefinition returns the
        // ROOT declaration, so jumping straight there would skip ChainMiddle and silently stop binding.
        var services = new ServiceCollection();
        BindCapturedEnvironment<ChainLeaf>(services, new Dictionary<string, string?>
        {
            ["M10_CHAINED"] = "from-middle-attribute"
        });

        using var provider = services.BuildServiceProvider();

        Assert.Equal("from-middle-attribute", provider.GetRequiredService<IOptions<ChainLeaf>>().Value.Chained);
    }

    [Fact]
    public void Binding_RejectsAnOverriddenDecoratedIndexerWhoseBaseIsOverloaded()
    {
        // Looking the base property up by name alone would raise AmbiguousMatchException here. Swallowing
        // that would drop the indexer from discovery entirely instead of rejecting it as an unsupported
        // bind target.
        var services = new ServiceCollection();

        var exception = Assert.Throws<EnvarsException>(
            () => BindCapturedEnvironment<IndexerDerived>(services, new Dictionary<string, string?>()));

        Assert.Equal(EnvarFailureKind.InvalidProperty, exception.FailureKind);
        Assert.Equal("Item", exception.PropertyName);
        Assert.Equal("M10_INDEXER", exception.EnvironmentVariableName);
    }

    [Fact]
    public void Binding_ReportsTheInvalidNameBeforeTheUnsupportedShape()
    {
        // The unsupported-shape failure carries a validated attribute name, so the name has to be checked
        // first; otherwise that row would have to echo malformed metadata.
        var services = new ServiceCollection();

        var exception = Assert.Throws<EnvarsException>(
            () => BindCapturedEnvironment<InvalidNameAndShape>(services, new Dictionary<string, string?>()));

        Assert.Equal(EnvarFailureKind.InvalidProperty, exception.FailureKind);
        Assert.Null(exception.EnvironmentVariableName);
        Assert.Equal(
            $"Property '{typeof(InvalidNameAndShape).FullName}.Value' has an invalid environment-variable name.",
            exception.Message);
    }

    [Fact]
    public void Binding_StillFindsAnAttributeOnAnOverriddenVirtualProperty()
    {
        // GetCustomAttributesData does not walk the inheritance chain, so this would silently stop
        // binding if the override were not followed explicitly.
        var services = new ServiceCollection();
        BindCapturedEnvironment<OverridingDerived>(services, new Dictionary<string, string?>
        {
            ["M10_OVERRIDDEN"] = "from-base-attribute"
        });

        using var provider = services.BuildServiceProvider();

        Assert.Equal("from-base-attribute", provider.GetRequiredService<IOptions<OverridingDerived>>().Value.Overridden);
    }
}
