using System;
using Xunit;

namespace FriendlyEnvars.Tests;

public class EnvarAttributeTests
{
    [Fact]
    public void Constructor_WithNullName_ShouldThrowArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => new EnvarAttribute(null!));

        Assert.Equal("name", exception.ParamName);
        Assert.Contains("null or empty", exception.Message);
    }

    [Fact]
    public void Constructor_WithEmptyName_ShouldThrowArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => new EnvarAttribute(string.Empty));

        Assert.Equal("name", exception.ParamName);
        Assert.Contains("null or empty", exception.Message);
    }

    [Fact]
    public void Constructor_WithValidName_ShouldSetNameProperty()
    {
        var attribute = new EnvarAttribute("VALID_NAME");

        Assert.Equal("VALID_NAME", attribute.Name);
    }
}
