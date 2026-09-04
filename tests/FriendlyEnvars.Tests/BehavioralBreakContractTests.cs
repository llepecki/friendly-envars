using System;
using System.Globalization;
using Xunit;

namespace FriendlyEnvars.Tests;

// Pins intentional enum-parsing breaks from 1.1.0.
public class BehavioralBreakContractTests
{
    private readonly DefaultEnvarPropertyBinder _binder = new();
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public enum NonFlagsWithNegative
    {
        All = -1,
        None = 0,
        Read = 1
    }

    public enum NonFlagsWithCombination
    {
        None = 0,
        Read = 1,
        Write = 2,
        ReadWrite = 3
    }

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
        // 1.1.0 accepted the numeric form.
        Assert.Throws<FormatException>(() => _binder.Convert("-1", typeof(NonFlagsWithNegative), Invariant));

        // The value itself remains reachable by its declared name.
        Assert.Equal(NonFlagsWithNegative.All, _binder.Convert("All", typeof(NonFlagsWithNegative), Invariant));
    }

    [Fact]
    public void NonFlagsEnum_RejectsACommaSeparatedList_EvenWhenTheCombinedValueIsDeclared()
    {
        // 1.1.0 accepted lists for non-flags enums.
        Assert.Throws<FormatException>(() => _binder.Convert("Read,Write", typeof(NonFlagsWithCombination), Invariant));

        // The combined value remains reachable by its own declared name.
        Assert.Equal(
            NonFlagsWithCombination.ReadWrite,
            _binder.Convert("ReadWrite", typeof(NonFlagsWithCombination), Invariant));
    }

    [Fact]
    public void FlagsEnum_RejectsNegativeNumericText_ButAcceptsTheDeclaredMemberName()
    {
        // 1.1.0 accepted the negative numeric form.
        Assert.Throws<FormatException>(() => _binder.Convert("-1", typeof(FlagsWithNegative), Invariant));

        // The same value, written as the declared member name, is still accepted.
        Assert.Equal(FlagsWithNegative.All, _binder.Convert("All", typeof(FlagsWithNegative), Invariant));
        Assert.Equal(FlagsWithNegative.All, _binder.Convert("all", typeof(FlagsWithNegative), Invariant));
    }

    [Fact]
    public void TheseRejectionsDoNotDiscloseTheValue()
    {
        // Rejection must still protect the raw value.
        const string Sentinel = "QZXJKVWYPLMB-NOT-A-MEMBER";

        var exception = Assert.Throws<FormatException>(
            () => _binder.Convert(Sentinel, typeof(NonFlagsWithCombination), Invariant));

        Assert.DoesNotContain(Sentinel, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Sentinel, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void TheEmptyStringBehaviourIsUnchangedFromTheOneOneZeroBaseline()
    {
        // Empty strings have reached the binder since 1.1.0.
        Assert.Equal(string.Empty, _binder.Convert(string.Empty, typeof(string), Invariant));
        Assert.Throws<FormatException>(() => _binder.Convert(string.Empty, typeof(int), Invariant));
    }
}
