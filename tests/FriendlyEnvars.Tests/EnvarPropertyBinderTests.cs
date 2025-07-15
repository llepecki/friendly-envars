using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace FriendlyEnvars.Tests;

public class EnvarPropertyBinderTests : IDisposable
{
    private readonly List<string> _environmentVariablesToCleanup = [];
    private readonly IEnvarPropertyBinder _binder = new EnvarPropertyBinder();

    public void Dispose()
    {
        foreach (var envVar in _environmentVariablesToCleanup)
        {
            Environment.SetEnvironmentVariable(envVar, null);
        }
    }

    private void SetEnvironmentVariable(string name, string? value)
    {
        Environment.SetEnvironmentVariable(name, value);
        _environmentVariablesToCleanup.Add(name);
    }

    private record TestConfiguration
    {
        [Envar("BINDER_STRING_SETTING")]
        public string? StringProperty { get; init; }

        [Envar("BINDER_INT_SETTING")]
        public int IntProperty { get; init; }

        [Envar("BINDER_REQUIRED_SETTING")]
        public string? RequiredProperty { get; init; }

        [Envar("BINDER_OPTIONAL_SETTING")]
        public string? OptionalProperty { get; init; }

        public string? PropertyWithoutAttribute { get; init; }
    }

    [Fact]
    public void Given_AllPropertiesHaveValidEnvironmentVariables_When_BindingFromEnvironmentVariables_Then_AllPropertiesAreSetCorrectly()
    {
        SetEnvironmentVariable("BINDER_STRING_SETTING", "test value");
        SetEnvironmentVariable("BINDER_INT_SETTING", "42");
        SetEnvironmentVariable("BINDER_REQUIRED_SETTING", "required value");
        SetEnvironmentVariable("BINDER_OPTIONAL_SETTING", "optional value");

        var result = OptionsBuilderExtensions.BindFromEnvironmentVariables<TestConfiguration>(_binder);

        Assert.Equal("test value", result.StringProperty);
        Assert.Equal(42, result.IntProperty);
        Assert.Equal("required value", result.RequiredProperty);
        Assert.Equal("optional value", result.OptionalProperty);
        Assert.Null(result.PropertyWithoutAttribute);
    }

    [Fact]
    public void Given_RequiredEnvironmentVariableIsMissing_When_BindingFromEnvironmentVariables_Then_PropertyRemainsDefault()
    {
        SetEnvironmentVariable("BINDER_STRING_SETTING", "test value");
        SetEnvironmentVariable("BINDER_INT_SETTING", "42");

        var result = OptionsBuilderExtensions.BindFromEnvironmentVariables<TestConfiguration>(_binder);

        Assert.Equal("test value", result.StringProperty);
        Assert.Equal(42, result.IntProperty);
        Assert.Null(result.RequiredProperty);
    }

    [Fact]
    public void Given_InvalidIntegerValue_When_BindingFromEnvironmentVariables_Then_ThrowsEnvarsException()
    {
        SetEnvironmentVariable("BINDER_INT_SETTING", "not a number");

        var exception = Assert.Throws<EnvarsException>(() => OptionsBuilderExtensions.BindFromEnvironmentVariables<TestConfiguration>(_binder));

        Assert.Contains("Failed to convert environment variable 'BINDER_INT_SETTING'", exception.Message);
        Assert.Contains("not a number", exception.Message);
        Assert.Contains("Int32", exception.Message);
    }

    [Fact]
    public void Given_ValidBoolValue_When_BindingFromEnvironmentVariables_Then_BoolPropertyIsSetCorrectly()
    {
        SetEnvironmentVariable("BINDER_BOOL_SETTING", "true");

        var result = OptionsBuilderExtensions.BindFromEnvironmentVariables<BoolConfiguration>(_binder);

        Assert.True(result.BoolProperty);
    }

    [Fact]
    public void Given_InvalidBoolValue_When_BindingFromEnvironmentVariables_Then_ThrowsEnvarsException()
    {
        SetEnvironmentVariable("BINDER_BOOL_SETTING", "maybe");

        var exception = Assert.Throws<EnvarsException>(() => OptionsBuilderExtensions.BindFromEnvironmentVariables<BoolConfiguration>(_binder));

        Assert.Contains("Failed to convert environment variable 'BINDER_BOOL_SETTING'", exception.Message);
        Assert.Contains("Boolean", exception.Message);
    }

    [Fact]
    public void Given_ValidGuidValue_When_BindingFromEnvironmentVariables_Then_GuidPropertyIsSetCorrectly()
    {
        var expectedGuid = Guid.NewGuid();
        SetEnvironmentVariable("BINDER_GUID_SETTING", expectedGuid.ToString());

        var result = OptionsBuilderExtensions.BindFromEnvironmentVariables<GuidConfiguration>(_binder);

        Assert.Equal(expectedGuid, result.GuidProperty);
    }

    [Fact]
    public void Given_InvalidGuidValue_When_BindingFromEnvironmentVariables_Then_ThrowsEnvarsException()
    {
        SetEnvironmentVariable("BINDER_GUID_SETTING", "not-a-guid");

        var exception = Assert.Throws<EnvarsException>(() => OptionsBuilderExtensions.BindFromEnvironmentVariables<GuidConfiguration>(_binder));

        Assert.Contains("Failed to convert environment variable 'BINDER_GUID_SETTING'", exception.Message);
        Assert.Contains("Guid", exception.Message);
    }

    [Fact]
    public void Given_ValidUriValue_When_BindingFromEnvironmentVariables_Then_UriPropertyIsSetCorrectly()
    {
        SetEnvironmentVariable("BINDER_URI_SETTING", "https://example.com");

        var result = OptionsBuilderExtensions.BindFromEnvironmentVariables<UriConfiguration>(_binder);

        Assert.Equal(new Uri("https://example.com"), result.UriProperty);
    }

    [Fact]
    public void Given_InvalidUriValue_When_BindingFromEnvironmentVariables_Then_ThrowsEnvarsException()
    {
        SetEnvironmentVariable("BINDER_URI_SETTING", "not a valid uri");

        var exception = Assert.Throws<EnvarsException>(() => OptionsBuilderExtensions.BindFromEnvironmentVariables<UriConfiguration>(_binder));

        Assert.Contains("Failed to convert environment variable 'BINDER_URI_SETTING'", exception.Message);
        Assert.Contains("Uri", exception.Message);
    }

    private record BoolConfiguration
    {
        [Envar("BINDER_BOOL_SETTING")]
        public bool BoolProperty { get; init; }
    }

    private record GuidConfiguration
    {
        [Envar("BINDER_GUID_SETTING")]
        public Guid GuidProperty { get; init; }
    }

    private record UriConfiguration
    {
        [Envar("BINDER_URI_SETTING")]
        public Uri? UriProperty { get; init; }
    }
}
