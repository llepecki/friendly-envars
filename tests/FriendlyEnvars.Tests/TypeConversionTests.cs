using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using Xunit;

namespace FriendlyEnvars.Tests;

public class TypeConversionTests : EnvarTestsBase
{

    public class TypeOptions
    {
        [Envar("TYPES_BOOL")]
        public bool BoolValue { get; set; }

        [Envar("TYPES_BOOL_NULLABLE")]
        public bool? NullableBoolValue { get; set; }

        [Envar("TYPES_BYTE")]
        public byte ByteValue { get; set; }

        [Envar("TYPES_DOUBLE")]
        public double DoubleValue { get; set; }

        [Envar("TYPES_GUID")]
        public Guid GuidValue { get; set; }

        [Envar("TYPES_DATETIME")]
        public DateTime DateTimeValue { get; set; }

        [Envar("TYPES_TIMESPAN")]
        public TimeSpan TimeSpanValue { get; set; }

        [Envar("TYPES_URI")]
        public Uri? UriValue { get; set; }

        [Envar("TYPES_CHAR")]
        public char CharValue { get; set; }

        [Envar("TYPES_ENUM")]
        public TestEnum EnumValue { get; set; }
    }

    public enum TestEnum
    {
        One,
        Two,
        Three
    }

    [Fact]
    public void BindFromEnvironmentVariables_WithDifferentTypes_ShouldConvertAndBind()
    {
        // Arrange
        SetEnvironmentVariable("TYPES_BOOL", "true");
        SetEnvironmentVariable("TYPES_BOOL_NULLABLE", "false");
        SetEnvironmentVariable("TYPES_BYTE", "42");
        SetEnvironmentVariable("TYPES_DOUBLE", "3.14159");
        SetEnvironmentVariable("TYPES_GUID", "00112233-4455-6677-8899-AABBCCDDEEFF");
        SetEnvironmentVariable("TYPES_DATETIME", "2025-07-16T12:30:45");
        SetEnvironmentVariable("TYPES_TIMESPAN", "1:45:59.89");
        SetEnvironmentVariable("TYPES_URI", "https://example.com/path");
        SetEnvironmentVariable("TYPES_CHAR", "X");
        SetEnvironmentVariable("TYPES_ENUM", "Two");

        var services = new ServiceCollection();
        services.AddOptions<TypeOptions>()
            .BindFromEnvarAttributes();

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<TypeOptions>>().Value;

        // Assert
        Assert.True(options.BoolValue);
        Assert.False(options.NullableBoolValue);
        Assert.Equal((byte)42, options.ByteValue);
        Assert.Equal(3.14159, options.DoubleValue);
        Assert.Equal(Guid.Parse("00112233-4455-6677-8899-AABBCCDDEEFF"), options.GuidValue);
        Assert.Equal(DateTime.Parse("2025-07-16T12:30:45"), options.DateTimeValue);
        Assert.Equal(TimeSpan.Parse("1:45:59.89"), options.TimeSpanValue);
        Assert.Equal(new Uri("https://example.com/path"), options.UriValue);
        Assert.Equal('X', options.CharValue);
        Assert.Equal(TestEnum.Two, options.EnumValue);
    }

    [Fact]
    public void BindFromEnvironmentVariables_WithInvalidBool_ShouldThrow()
    {
        SetEnvironmentVariable("TYPES_BOOL", "not-a-bool");

        var services = new ServiceCollection();
        services.AddOptions<TypeOptions>()
            .BindFromEnvarAttributes();

        var serviceProvider = services.BuildServiceProvider();
        var exception = Assert.Throws<EnvarsException>(() => serviceProvider.GetRequiredService<IOptions<TypeOptions>>().Value);

        Assert.Contains("TYPES_BOOL", exception.Message);
        Assert.Contains("not-a-bool", exception.Message);
        Assert.Contains("Boolean", exception.Message);
    }

    [Fact]
    public void BindFromEnvironmentVariables_WithInvalidEnum_ShouldThrow()
    {
        SetEnvironmentVariable("TYPES_BOOL", "true");
        SetEnvironmentVariable("TYPES_ENUM", "Four");

        var services = new ServiceCollection();
        services.AddOptions<TypeOptions>()
            .BindFromEnvarAttributes();

        var serviceProvider = services.BuildServiceProvider();
        var exception = Assert.Throws<EnvarsException>(() => serviceProvider.GetRequiredService<IOptions<TypeOptions>>().Value);

        Assert.Contains("TYPES_ENUM", exception.Message);
        Assert.Contains("Four", exception.Message);
        Assert.Contains("TestEnum", exception.Message);
    }

    [Fact]
    public void BindFromEnvironmentVariables_WithInvalidChar_ShouldThrow()
    {
        SetEnvironmentVariable("TYPES_BOOL", "true");
        SetEnvironmentVariable("TYPES_CHAR", "TooLong");

        var services = new ServiceCollection();
        services.AddOptions<TypeOptions>()
            .BindFromEnvarAttributes();

        var serviceProvider = services.BuildServiceProvider();
        var exception = Assert.Throws<EnvarsException>(() => serviceProvider.GetRequiredService<IOptions<TypeOptions>>().Value);

        Assert.Contains("TYPES_CHAR", exception.Message);
        Assert.Contains("TooLong", exception.Message);
        Assert.Contains("Char", exception.Message);
    }
}
