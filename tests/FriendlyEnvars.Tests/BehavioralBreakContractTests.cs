using System;
using System.Globalization;
using Xunit;

namespace FriendlyEnvars.Tests;

/// <summary>
/// Pins the three enum-parsing differences from 1.1.0 that are deliberate rather than accidental.
/// </summary>
/// <remarks>
/// <para>
/// 1.1.0 delegated enum syntax to <see cref="Enum.Parse(Type, string, bool)"/>, which accepts a
/// comma-separated list for every enum and accepts signed numeric text. Each case below therefore
/// SUCCEEDED in 1.1.0 and now fails. They are called out here, and in the package release notes, so the
/// change is recorded as intended rather than discovered by a consumer.
/// </para>
/// <para>
/// The declared-name route is asserted alongside each rejection, so these tests pin a narrowing of the
/// accepted syntax rather than a loss of the underlying value.
/// </para>
/// </remarks>
public class BehavioralBreakContractTests
{
    private readonly DefaultEnvarPropertyBinder _binder = new();
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>A non-flags enum declaring a negative member, the shape 1.1.0 would accept "-1" for.</summary>
    public enum NonFlagsWithNegative
    {
        All = -1,
        None = 0,
        Read = 1
    }

    /// <summary>A non-flags enum whose combined value is declared, so 1.1.0 accepted "Read,Write".</summary>
    public enum NonFlagsWithCombination
    {
        None = 0,
        Read = 1,
        Write = 2,
        ReadWrite = 3
    }

    /// <summary>A flags enum declaring a negative member.</summary>
    [Flags]
    public enum FlagsWithNegative
    {
        None = 0,
        Read = 1,
        Write = 2,
        All = -1
    }

    [Fact]
    public void NonFlagsEnum_RejectsNegativeNumericText_EvenWhenThatValueIsDeclared()
    {
        // 1.1.0: Enum.Parse("-1") returned All, and Enum.IsDefined agreed, so it was accepted.
        Assert.Throws<FormatException>(() => _binder.Convert("-1", typeof(NonFlagsWithNegative), Invariant));

        // The value itself remains reachable by its declared name.
        Assert.Equal(NonFlagsWithNegative.All, _binder.Convert("All", typeof(NonFlagsWithNegative), Invariant));
    }

    [Fact]
    public void NonFlagsEnum_RejectsACommaSeparatedList_EvenWhenTheCombinedValueIsDeclared()
    {
        // 1.1.0: Enum.Parse("Read,Write") produced 3, which IsDefined accepted because ReadWrite = 3.
        Assert.Throws<FormatException>(() => _binder.Convert("Read,Write", typeof(NonFlagsWithCombination), Invariant));

        // The combined value remains reachable by its own declared name.
        Assert.Equal(
            NonFlagsWithCombination.ReadWrite,
            _binder.Convert("ReadWrite", typeof(NonFlagsWithCombination), Invariant));
    }

    [Fact]
    public void FlagsEnum_RejectsNegativeNumericText_ButAcceptsTheDeclaredMemberName()
    {
        // 1.1.0: Enum.Parse("-1") on a flags enum returned All.
        Assert.Throws<FormatException>(() => _binder.Convert("-1", typeof(FlagsWithNegative), Invariant));

        // The same value, written as the declared member name, is still accepted.
        Assert.Equal(FlagsWithNegative.All, _binder.Convert("All", typeof(FlagsWithNegative), Invariant));
        Assert.Equal(FlagsWithNegative.All, _binder.Convert("all", typeof(FlagsWithNegative), Invariant));
    }

    [Fact]
    public void TheseRejectionsDoNotDiscloseTheValue()
    {
        // The narrowing must not come at the cost of the 2.0 secret-safety contract.
        const string Sentinel = "QZXJKVWYPLMB-NOT-A-MEMBER";

        var exception = Assert.Throws<FormatException>(
            () => _binder.Convert(Sentinel, typeof(NonFlagsWithCombination), Invariant));

        Assert.DoesNotContain(Sentinel, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Sentinel, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void TheEmptyStringBehaviourIsUnchangedFromTheOneOneZeroBaseline()
    {
        // Listed in the release notes under "Not new in 2.0": an empty value has been passed to the
        // binder rather than treated as unset since 1.1.0, so a string takes it and a number rejects it.
        Assert.Equal(string.Empty, _binder.Convert(string.Empty, typeof(string), Invariant));
        Assert.Throws<FormatException>(() => _binder.Convert(string.Empty, typeof(int), Invariant));
    }
}
