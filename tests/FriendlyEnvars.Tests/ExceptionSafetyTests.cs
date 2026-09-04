using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Xunit;

namespace FriendlyEnvars.Tests;

// Checks every eight-character sentinel window to catch partial value disclosure.
public class ExceptionSafetyTests : EnvarTestsBase
{
    private const int WindowLength = 8;

    private static readonly string Sentinel = BuildSentinel();

    private static readonly IReadOnlyCollection<string> SentinelWindows = BuildWindows(Sentinel);

    private static string BuildSentinel()
    {
        var builder = new StringBuilder(4608);

        // Token-shaped, but obviously fake and low entropy so it cannot be mistaken for a real credential.
        builder.Append("token-0000-1111-2222-3333-4444-5555-6666");
        builder.Append('\n');
        builder.Append("\u001b[31mANSI-ESCAPED-SEGMENT\u001b[0m");
        builder.Append("\r\n");

        // Deterministic filler avoids accidental matches and runtime differences.
        const string Alphabet = "QXZJKVW0123456789";
        uint state = 20260903;

        while (builder.Length < 4096 + 256)
        {
            state = (state * 1664525u) + 1013904223u;
            builder.Append(Alphabet[(int)(state >> 16) % Alphabet.Length]);
        }

        return builder.ToString();
    }

    private static IReadOnlyCollection<string> BuildWindows(string sentinel)
    {
        var windows = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i + WindowLength <= sentinel.Length; i++)
        {
            windows.Add(sentinel.Substring(i, WindowLength));
        }

        return windows;
    }

    private static void AssertNoValueDisclosure(EnvarsException exception)
    {
        // A library-generated failure never keeps the cause, because the cause's message quotes the value.
        Assert.Null(exception.InnerException);

        string structured = string.Join(
            '\u0001',
            exception.FailureKind?.ToString(),
            exception.EnvironmentVariableName,
            exception.OptionsType?.AssemblyQualifiedName,
            exception.OptionsName,
            exception.PropertyName,
            exception.TargetType?.AssemblyQualifiedName,
            exception.CultureName,
            exception.BinderType?.AssemblyQualifiedName,
            exception.CauseType);

        string[] haystacks = [exception.Message, exception.ToString(), structured];

        foreach (string haystack in haystacks)
        {
            Assert.DoesNotContain(Sentinel, haystack, StringComparison.Ordinal);

            foreach (string window in SentinelWindows)
            {
                Assert.DoesNotContain(window, haystack, StringComparison.Ordinal);
            }
        }
    }

    private static EnvarsException RegisterAndCaptureFailure<T>(string optionsName = "")
        where T : class, new()
    {
        var services = new ServiceCollection();

        return Assert.Throws<EnvarsException>(() => services.AddOptions<T>(optionsName).BindEnvars());
    }

    private static EnvarsException BindAndCaptureFailure<T>(Action<EnvarSettings>? configure = null, string optionsName = "")
        where T : class, new()
    {
        var services = new ServiceCollection();
        services.AddOptions<T>(optionsName).BindEnvars(configure);

        using var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IOptionsFactory<T>>();

        return Assert.Throws<EnvarsException>(() => factory.Create(optionsName));
    }

    public class NumericOptions
    {
        [Envar("H01_NUMERIC")]
        public int Value { get; set; }
    }

    public class TextOptions
    {
        [Envar("H01_TEXT")]
        public string? Value { get; set; }
    }

    public class ThrowingSetterOptions
    {
        [Envar("H01_SETTER")]
        public string? Value
        {
            get => null;
            set => throw new InvalidOperationException($"setter rejected the value: {value}");
        }
    }

    private sealed class ThrowingBinder : IEnvarPropertyBinder
    {
        public object? Convert(string value, Type targetType, CultureInfo culture)
        {
            throw new ArithmeticException($"binder rejected the value: {value}");
        }
    }

    private sealed class ThrowingEnvarsExceptionBinder : IEnvarPropertyBinder
    {
        public object? Convert(string value, Type targetType, CultureInfo culture)
        {
            // Sanitize EnvarsException thrown by a custom binder too.
            throw new EnvarsException($"binder rejected the value: {value}");
        }
    }

    [Fact]
    public void DefaultBinderConversionFailure_DisclosesNothingAboutTheValue()
    {
        SetEnvironmentVariable("H01_NUMERIC", Sentinel);

        var exception = BindAndCaptureFailure<NumericOptions>();

        AssertNoValueDisclosure(exception);

        Assert.Equal(EnvarFailureKind.Conversion, exception.FailureKind);
        Assert.Equal("H01_NUMERIC", exception.EnvironmentVariableName);
        Assert.Equal(typeof(NumericOptions), exception.OptionsType);
        Assert.Equal(Options.DefaultName, exception.OptionsName);
        Assert.Equal(nameof(NumericOptions.Value), exception.PropertyName);
        Assert.Equal(typeof(int), exception.TargetType);
        Assert.Equal(CultureInfo.InvariantCulture.Name, exception.CultureName);
        Assert.Equal(typeof(DefaultEnvarPropertyBinder), exception.BinderType);
        Assert.Equal(typeof(FormatException).FullName, exception.CauseType);

        Assert.Equal(
            "Failed to convert environment variable 'H01_NUMERIC' to 'System.Int32' for option " +
            $"'{typeof(NumericOptions).FullName}.Value' (options name '<default>').",
            exception.Message);
    }

    [Fact]
    public void CustomBinderFailure_DisclosesNothingAboutTheValue()
    {
        SetEnvironmentVariable("H01_TEXT", Sentinel);

        var exception = BindAndCaptureFailure<TextOptions>(
            static settings => settings.UseCustomEnvarPropertyBinder(new ThrowingBinder()));

        AssertNoValueDisclosure(exception);

        Assert.Equal(EnvarFailureKind.Conversion, exception.FailureKind);
        Assert.Equal("H01_TEXT", exception.EnvironmentVariableName);
        Assert.Equal(typeof(TextOptions), exception.OptionsType);
        Assert.Equal(typeof(string), exception.TargetType);
        Assert.Equal(typeof(ThrowingBinder), exception.BinderType);
        Assert.Equal(typeof(ArithmeticException).FullName, exception.CauseType);
    }

    [Fact]
    public void CustomBinderThrowingEnvarsException_IsStillSanitised()
    {
        SetEnvironmentVariable("H01_TEXT", Sentinel);

        var exception = BindAndCaptureFailure<TextOptions>(
            static settings => settings.UseCustomEnvarPropertyBinder(new ThrowingEnvarsExceptionBinder()));

        AssertNoValueDisclosure(exception);

        Assert.Equal(EnvarFailureKind.Conversion, exception.FailureKind);
        Assert.Equal(typeof(ThrowingEnvarsExceptionBinder), exception.BinderType);
        Assert.Equal(typeof(EnvarsException).FullName, exception.CauseType);
    }

    [Fact]
    public void ThrowingSetter_DisclosesNothingAboutTheValue()
    {
        SetEnvironmentVariable("H01_SETTER", Sentinel);

        var exception = BindAndCaptureFailure<ThrowingSetterOptions>();

        AssertNoValueDisclosure(exception);

        Assert.Equal(EnvarFailureKind.Assignment, exception.FailureKind);
        Assert.Equal("H01_SETTER", exception.EnvironmentVariableName);
        Assert.Equal(typeof(ThrowingSetterOptions), exception.OptionsType);
        Assert.Equal(nameof(ThrowingSetterOptions.Value), exception.PropertyName);
        Assert.Equal(typeof(string), exception.TargetType);

        // The setter's exception arrives wrapped by reflection; the reported cause is the real one.
        Assert.Equal(typeof(InvalidOperationException).FullName, exception.CauseType);

        // Assignment failures have no conversion metadata.
        Assert.Null(exception.CultureName);
        Assert.Null(exception.BinderType);

        Assert.Equal(
            "Failed to assign environment variable 'H01_SETTER' to option " +
            $"'{typeof(ThrowingSetterOptions).FullName}.Value' (options name '<default>').",
            exception.Message);
    }

    [Fact]
    public void UnsupportedPropertyShape_ReportsInvalidPropertyWithoutTheValue()
    {
        SetEnvironmentVariable("H01_READONLY", Sentinel);

        // Unsupported shapes fail during registration, before environment reads.
        var exception = RegisterAndCaptureFailure<ReadOnlyOptions>();

        AssertNoValueDisclosure(exception);

        Assert.Equal(EnvarFailureKind.InvalidProperty, exception.FailureKind);
        Assert.Equal(Options.DefaultName, exception.OptionsName);
        Assert.Equal("H01_READONLY", exception.EnvironmentVariableName);
        Assert.Equal(typeof(ReadOnlyOptions), exception.OptionsType);
        Assert.Equal(nameof(ReadOnlyOptions.Value), exception.PropertyName);
        Assert.Equal(typeof(string), exception.TargetType);
        Assert.Null(exception.CultureName);
        Assert.Null(exception.BinderType);
        Assert.Null(exception.CauseType);

        Assert.Equal(
            $"Property '{typeof(ReadOnlyOptions).FullName}.Value' mapped to environment variable " +
            "'H01_READONLY' is not a supported bind target.",
            exception.Message);
    }

    public class ReadOnlyOptions
    {
        [Envar("H01_READONLY")]
        public string Value { get; } = string.Empty;
    }

    [Fact]
    public void NamedOptions_AreReportedByTheirExactName()
    {
        SetEnvironmentVariable("H01_NUMERIC", Sentinel);

        var exception = BindAndCaptureFailure<NumericOptions>(optionsName: "primary");

        AssertNoValueDisclosure(exception);

        Assert.Equal("primary", exception.OptionsName);
        Assert.Contains("(options name 'primary')", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OptionsNameContainingQuotesBackslashesOrControls_IsEscapedInTheMessage()
    {
        SetEnvironmentVariable("H01_NUMERIC", Sentinel);

        var exception = BindAndCaptureFailure<NumericOptions>(optionsName: "n'a\\me\u0007");

        AssertNoValueDisclosure(exception);

        // The structured property keeps the exact registration name; only the message is escaped.
        Assert.Equal("n'a\\me\u0007", exception.OptionsName);
        Assert.Contains(@"(options name 'n\'a\\me\u0007')", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain('\u0007', exception.Message);
    }

    [Theory]
    [InlineData("", "<default>")]
    [InlineData("plain", "plain")]
    [InlineData("with space", "with space")]
    [InlineData("with'quote", @"with\'quote")]
    [InlineData(@"with\backslash", @"with\\backslash")]
    [InlineData("with\0null", @"with\u0000null")]
    [InlineData("with\u001bescape", @"with\u001Bescape")]
    [InlineData("with\nnewline", @"with\u000Anewline")]
    [InlineData("with\ttab", @"with\u0009tab")]
    [InlineData("Unicode\u00c5", "Unicode\u00c5")]
    public void FormatOptionsName_EscapesExactlyTheSpecifiedCharacters(string optionsName, string expected)
    {
        Assert.Equal(expected, EnvarsException.FormatOptionsName(optionsName));
    }

    [Fact]
    public void LegacyPublicConstructors_LeaveEveryStructuredPropertyNull()
    {
        foreach (var exception in new[]
                 {
                     new EnvarsException(),
                     new EnvarsException("message"),
                     new EnvarsException("message", new InvalidOperationException("inner"))
                 })
        {
            Assert.Null(exception.FailureKind);
            Assert.Null(exception.EnvironmentVariableName);
            Assert.Null(exception.OptionsType);
            Assert.Null(exception.OptionsName);
            Assert.Null(exception.PropertyName);
            Assert.Null(exception.TargetType);
            Assert.Null(exception.CultureName);
            Assert.Null(exception.BinderType);
            Assert.Null(exception.CauseType);
        }
    }

    [Fact]
    public void Sentinel_IsHostileEnoughToBeMeaningful()
    {
        Assert.True(Sentinel.Length > 4096, "the sentinel must exceed 4 KiB");
        Assert.Contains("token-0000-1111-2222-3333-4444-5555-6666", Sentinel, StringComparison.Ordinal);
        Assert.Contains('\n', Sentinel);
        Assert.Contains('\u001b', Sentinel);
        Assert.True(SentinelWindows.Count > 1000, "the sentinel must yield many distinct windows");
    }
}
