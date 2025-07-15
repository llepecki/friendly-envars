using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace FriendlyEnvars.Tests;

public class EnvarValidationTests : IDisposable
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

    public class TestOptions
    {
        [Required]
        [Envar("VALIDATION_REQUIRED_SETTING")]
        public string RequiredSetting { get; set; } = string.Empty;

        [StringLength(10)]
        [Envar("VALIDATION_STRING_LENGTH_SETTING")]
        public string StringLengthSetting { get; set; } = string.Empty;

        [Range(1, 100)]
        [Envar("VALIDATION_RANGE_SETTING")]
        public int RangeSetting { get; set; }

        [EmailAddress]
        [Envar("VALIDATION_EMAIL_SETTING")]
        public string EmailSetting { get; set; } = string.Empty;

        [Url]
        [Envar("VALIDATION_URL_SETTING")]
        public string UrlSetting { get; set; } = string.Empty;

        [Envar("VALIDATION_OPTIONAL_SETTING")]
        public string OptionalSetting { get; set; } = string.Empty;
    }

    [Fact]
    public void BindFromEnvironmentVariables_WithValidData_ShouldCreateValidInstance()
    {
        SetEnvironmentVariable("VALIDATION_REQUIRED_SETTING", "test");
        SetEnvironmentVariable("VALIDATION_STRING_LENGTH_SETTING", "short");
        SetEnvironmentVariable("VALIDATION_RANGE_SETTING", "50");
        SetEnvironmentVariable("VALIDATION_EMAIL_SETTING", "test@example.com");
        SetEnvironmentVariable("VALIDATION_URL_SETTING", "https://example.com");

        var options = OptionsBuilderExtensions.BindFromEnvironmentVariables<TestOptions>(new EnvarPropertyBinder());
        var validationContext = new ValidationContext(options);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, validationContext, validationResults, true);

        Assert.True(isValid);
        Assert.Empty(validationResults);
        Assert.Equal("test", options.RequiredSetting);
        Assert.Equal("short", options.StringLengthSetting);
        Assert.Equal(50, options.RangeSetting);
        Assert.Equal("test@example.com", options.EmailSetting);
        Assert.Equal("https://example.com", options.UrlSetting);

    }

    [Fact]
    public void BindFromEnvironmentVariables_WithMissingRequiredProperty_ShouldFailValidation()
    {
        SetEnvironmentVariable("VALIDATION_STRING_LENGTH_SETTING", "short");
        SetEnvironmentVariable("VALIDATION_RANGE_SETTING", "50");
        SetEnvironmentVariable("VALIDATION_EMAIL_SETTING", "test@example.com");

        var options = OptionsBuilderExtensions.BindFromEnvironmentVariables<TestOptions>(new EnvarPropertyBinder());
        var validationContext = new ValidationContext(options);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, validationContext, validationResults, true);

        Assert.False(isValid);
        Assert.Contains(validationResults, r => r.ErrorMessage!.Contains("RequiredSetting"));

    }

    [Fact]
    public void BindFromEnvironmentVariables_WithStringTooLong_ShouldFailValidation()
    {
        SetEnvironmentVariable("VALIDATION_REQUIRED_SETTING", "test");
        SetEnvironmentVariable("VALIDATION_STRING_LENGTH_SETTING", "this string is too long");
        SetEnvironmentVariable("VALIDATION_RANGE_SETTING", "50");
        SetEnvironmentVariable("VALIDATION_EMAIL_SETTING", "test@example.com");

        var options = OptionsBuilderExtensions.BindFromEnvironmentVariables<TestOptions>(new EnvarPropertyBinder());
        var validationContext = new ValidationContext(options);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, validationContext, validationResults, true);

        Assert.False(isValid);
        Assert.Contains(validationResults, r => r.ErrorMessage!.Contains("StringLengthSetting"));

    }

    [Fact]
    public void BindFromEnvironmentVariables_WithOutOfRangeValue_ShouldFailValidation()
    {
        SetEnvironmentVariable("VALIDATION_REQUIRED_SETTING", "test");
        SetEnvironmentVariable("VALIDATION_STRING_LENGTH_SETTING", "short");
        SetEnvironmentVariable("VALIDATION_RANGE_SETTING", "200");
        SetEnvironmentVariable("VALIDATION_EMAIL_SETTING", "test@example.com");

        var options = OptionsBuilderExtensions.BindFromEnvironmentVariables<TestOptions>(new EnvarPropertyBinder());
        var validationContext = new ValidationContext(options);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, validationContext, validationResults, true);

        Assert.False(isValid);
        Assert.Contains(validationResults, r => r.ErrorMessage!.Contains("RangeSetting"));

    }

    [Fact]
    public void BindFromEnvironmentVariables_WithInvalidEmail_ShouldFailValidation()
    {
        SetEnvironmentVariable("VALIDATION_REQUIRED_SETTING", "test");
        SetEnvironmentVariable("VALIDATION_STRING_LENGTH_SETTING", "short");
        SetEnvironmentVariable("VALIDATION_RANGE_SETTING", "50");
        SetEnvironmentVariable("VALIDATION_EMAIL_SETTING", "invalid-email");

        var options = OptionsBuilderExtensions.BindFromEnvironmentVariables<TestOptions>(new EnvarPropertyBinder());
        var validationContext = new ValidationContext(options);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, validationContext, validationResults, true);

        Assert.False(isValid);
        Assert.Contains(validationResults, r => r.ErrorMessage!.Contains("EmailSetting"));

    }

    [Fact]
    public void BindFromEnvironmentVariables_WithOptionsPattern_ShouldWorkWithValidateOnStart()
    {
        SetEnvironmentVariable("VALIDATION_REQUIRED_SETTING", "test");
        SetEnvironmentVariable("VALIDATION_STRING_LENGTH_SETTING", "short");
        SetEnvironmentVariable("VALIDATION_RANGE_SETTING", "50");
        SetEnvironmentVariable("VALIDATION_EMAIL_SETTING", "test@example.com");
        SetEnvironmentVariable("VALIDATION_URL_SETTING", "https://example.com");

        var services = new ServiceCollection();
        services.AddOptions<TestOptions>()
            .Configure(options =>
            {
                var boundOptions = OptionsBuilderExtensions.BindFromEnvironmentVariables<TestOptions>(new EnvarPropertyBinder());
                options.RequiredSetting = boundOptions.RequiredSetting;
                options.StringLengthSetting = boundOptions.StringLengthSetting;
                options.RangeSetting = boundOptions.RangeSetting;
                options.EmailSetting = boundOptions.EmailSetting;
                options.UrlSetting = boundOptions.UrlSetting;
                options.OptionalSetting = boundOptions.OptionalSetting;
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<TestOptions>>();

        Assert.Equal("test", options.Value.RequiredSetting);
        Assert.Equal("short", options.Value.StringLengthSetting);
        Assert.Equal(50, options.Value.RangeSetting);
        Assert.Equal("test@example.com", options.Value.EmailSetting);
        Assert.Equal("https://example.com", options.Value.UrlSetting);

    }

    [Fact]
    public void BindFromEnvironmentVariables_WithInvalidUrl_ShouldFailValidation()
    {
        SetEnvironmentVariable("VALIDATION_REQUIRED_SETTING", "test");
        SetEnvironmentVariable("VALIDATION_STRING_LENGTH_SETTING", "short");
        SetEnvironmentVariable("VALIDATION_RANGE_SETTING", "50");
        SetEnvironmentVariable("VALIDATION_EMAIL_SETTING", "test@example.com");
        SetEnvironmentVariable("VALIDATION_URL_SETTING", "not-a-valid-url");

        var options = OptionsBuilderExtensions.BindFromEnvironmentVariables<TestOptions>(new EnvarPropertyBinder());
        var validationContext = new ValidationContext(options);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, validationContext, validationResults, true);

        Assert.False(isValid);
        Assert.Contains(validationResults, r => r.ErrorMessage!.Contains("UrlSetting"));

    }

    [Fact]
    public void BindFromEnvironmentVariables_WithValidHttpUrl_ShouldPassValidation()
    {
        SetEnvironmentVariable("VALIDATION_REQUIRED_SETTING", "test");
        SetEnvironmentVariable("VALIDATION_STRING_LENGTH_SETTING", "short");
        SetEnvironmentVariable("VALIDATION_RANGE_SETTING", "50");
        SetEnvironmentVariable("VALIDATION_EMAIL_SETTING", "test@example.com");
        SetEnvironmentVariable("VALIDATION_URL_SETTING", "http://example.com");

        var options = OptionsBuilderExtensions.BindFromEnvironmentVariables<TestOptions>(new EnvarPropertyBinder());
        var validationContext = new ValidationContext(options);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, validationContext, validationResults, true);

        Assert.True(isValid);
        Assert.Empty(validationResults);
        Assert.Equal("test", options.RequiredSetting);
        Assert.Equal("http://example.com", options.UrlSetting);

    }

    [Fact]
    public void BindFromEnvironmentVariables_WithValidFtpUrl_ShouldPassValidation()
    {
        SetEnvironmentVariable("VALIDATION_REQUIRED_SETTING", "test");
        SetEnvironmentVariable("VALIDATION_STRING_LENGTH_SETTING", "short");
        SetEnvironmentVariable("VALIDATION_RANGE_SETTING", "50");
        SetEnvironmentVariable("VALIDATION_EMAIL_SETTING", "test@example.com");
        SetEnvironmentVariable("VALIDATION_URL_SETTING", "ftp://files.example.com");

        var options = OptionsBuilderExtensions.BindFromEnvironmentVariables<TestOptions>(new EnvarPropertyBinder());
        var validationContext = new ValidationContext(options);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, validationContext, validationResults, true);

        Assert.True(isValid);
        Assert.Empty(validationResults);
        Assert.Equal("test", options.RequiredSetting);
        Assert.Equal("ftp://files.example.com", options.UrlSetting);

    }

    [Fact]
    public void BindFromEnvironmentVariables_WithOptionsPatternAndInvalidData_ShouldThrowOnStart()
    {
        SetEnvironmentVariable("VALIDATION_STRING_LENGTH_SETTING", "this string is too long");
        SetEnvironmentVariable("VALIDATION_RANGE_SETTING", "50");
        SetEnvironmentVariable("VALIDATION_EMAIL_SETTING", "test@example.com");

        var services = new ServiceCollection();
        services.AddOptions<TestOptions>()
            .Configure(options =>
            {
                var boundOptions = OptionsBuilderExtensions.BindFromEnvironmentVariables<TestOptions>(new EnvarPropertyBinder());
                options.RequiredSetting = boundOptions.RequiredSetting;
                options.StringLengthSetting = boundOptions.StringLengthSetting;
                options.RangeSetting = boundOptions.RangeSetting;
                options.EmailSetting = boundOptions.EmailSetting;
                options.UrlSetting = boundOptions.UrlSetting;
                options.OptionalSetting = boundOptions.OptionalSetting;
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var serviceProvider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => serviceProvider.GetRequiredService<IOptions<TestOptions>>().Value);

    }

}
