using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace FriendlyEnvars.Tests;

public class WhitespaceAndEmptyValueTests : IDisposable
{
    private readonly List<string> _environmentVariablesToCleanup = [];

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

    public class WhitespaceOptions
    {
        [Required]
        [Envar("WHITESPACE_REQUIRED")]
        public string RequiredSetting { get; set; } = string.Empty;

        [Envar("WHITESPACE_OPTIONAL")]
        public string OptionalSetting { get; set; } = "DefaultValue";

        [Envar("WHITESPACE_TRIMMED")]
        public string TrimmedSetting { get; set; } = string.Empty;
    }

    [Fact]
    public void BindFromEnvironmentVariables_WithWhitespaceValue_ShouldUseWhitespaceAsValue()
    {
        SetEnvironmentVariable("WHITESPACE_REQUIRED", "   ");
        SetEnvironmentVariable("WHITESPACE_TRIMMED", "  Padded  ");

        var services = new ServiceCollection();
        services.AddOptions<WhitespaceOptions>()
            .BindFromEnvarAttributes();

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<WhitespaceOptions>>().Value;

        Assert.Equal("   ", options.RequiredSetting);
        Assert.Equal("DefaultValue", options.OptionalSetting);
        Assert.Equal("  Padded  ", options.TrimmedSetting); // Whitespace is preserved
    }

    [Fact]
    public void BindFromEnvironmentVariables_WithEmptyValues_ShouldSkipBinding()
    {
        SetEnvironmentVariable("WHITESPACE_REQUIRED", "NonEmpty");
        SetEnvironmentVariable("WHITESPACE_OPTIONAL", "");

        var services = new ServiceCollection();
        services.AddOptions<WhitespaceOptions>()
            .BindFromEnvarAttributes();

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<WhitespaceOptions>>().Value;

        Assert.Equal("NonEmpty", options.RequiredSetting);
        Assert.Equal("DefaultValue", options.OptionalSetting); // Empty value is skipped, default preserved
    }

    [Fact]
    public void BindFromEnvironmentVariables_WithNullValue_ShouldSkipBinding()
    {
        SetEnvironmentVariable("WHITESPACE_REQUIRED", "NonEmpty");
        SetEnvironmentVariable("WHITESPACE_OPTIONAL", null);

        var services = new ServiceCollection();
        services.AddOptions<WhitespaceOptions>()
            .BindFromEnvarAttributes();

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<WhitespaceOptions>>().Value;

        Assert.Equal("NonEmpty", options.RequiredSetting);
        Assert.Equal("DefaultValue", options.OptionalSetting); // Null value is skipped, default preserved
    }
}
