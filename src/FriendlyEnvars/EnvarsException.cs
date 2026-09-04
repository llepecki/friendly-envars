using System;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace FriendlyEnvars;

/// <summary>
/// Reports an environment-variable binding failure.
/// </summary>
/// <remarks>
/// Library-generated instances never retain the raw value, cause message, or cause object.
/// Their <see cref="Exception.InnerException"/> is <see langword="null"/>; use <see cref="CauseType"/>.
/// Instances created through the public constructors have no structured metadata.
/// </remarks>
public sealed class EnvarsException : Exception
{
    /// <summary>
    /// Creates an exception without a message.
    /// </summary>
    public EnvarsException()
    {
    }

    /// <summary>
    /// Creates an exception with a message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public EnvarsException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates an exception with a message and cause.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public EnvarsException(string message, Exception innerException) : base(message, innerException)
    {
    }

    internal EnvarsException(
        string message,
        EnvarFailureKind failureKind,
        string? environmentVariableName,
        Type? optionsType,
        string? optionsName,
        string? propertyName,
        Type? targetType,
        string? cultureName,
        Type? binderType,
        string? causeType) : base(message)
    {
        FailureKind = failureKind;
        EnvironmentVariableName = environmentVariableName;
        OptionsType = optionsType;
        OptionsName = optionsName;
        PropertyName = propertyName;
        TargetType = targetType;
        CultureName = cultureName;
        BinderType = binderType;
        CauseType = causeType;
    }

    /// <summary>
    /// Gets the failed stage, or <see langword="null"/> for a manually created exception.
    /// </summary>
    public EnvarFailureKind? FailureKind { get; }

    /// <summary>
    /// Gets the environment-variable name, if available.
    /// </summary>
    public string? EnvironmentVariableName { get; }

    /// <summary>
    /// Gets the options type, if available.
    /// </summary>
    public Type? OptionsType { get; }

    /// <summary>
    /// Gets the options name, if available. Default options use <see cref="string.Empty"/>.
    /// </summary>
    public string? OptionsName { get; }

    /// <summary>
    /// Gets the property name, if available.
    /// </summary>
    public string? PropertyName { get; }

    /// <summary>
    /// Gets the property's declared type, if available.
    /// </summary>
    public Type? TargetType { get; }

    /// <summary>
    /// Gets the conversion culture name, if applicable.
    /// </summary>
    public string? CultureName { get; }

    /// <summary>
    /// Gets the binder type, if applicable.
    /// </summary>
    public Type? BinderType { get; }

    /// <summary>
    /// Gets the cause's full type name, if available.
    /// </summary>
    public string? CauseType { get; }

    internal static EnvarsException InvalidPropertyShape(
        string environmentVariableName,
        Type optionsType,
        string optionsName,
        string propertyName,
        Type targetType)
    {
        string message =
            $"Property '{optionsType.FullName}.{propertyName}' mapped to environment variable " +
            $"'{environmentVariableName}' is not a supported bind target.";

        return new EnvarsException(
            message,
            EnvarFailureKind.InvalidProperty,
            environmentVariableName,
            optionsType,
            optionsName,
            propertyName,
            targetType,
            cultureName: null,
            binderType: null,
            causeType: null);
    }

    internal static EnvarsException InvalidAttributeName(
        Type optionsType,
        string optionsName,
        string propertyName,
        Type targetType)
    {
        // Never reproduce an invalid name in logs.
        string message = $"Property '{optionsType.FullName}.{propertyName}' has an invalid environment-variable name.";

        return new EnvarsException(
            message,
            EnvarFailureKind.InvalidProperty,
            environmentVariableName: null,
            optionsType,
            optionsName,
            propertyName,
            targetType,
            cultureName: null,
            binderType: null,
            causeType: null);
    }

    internal static EnvarsException TypeDiscoveryFailure(Type optionsType, string optionsName, string causeType)
    {
        string message =
            $"Failed to inspect environment-variable bindings for options type '{optionsType.FullName}' " +
            $"(options name '{FormatOptionsName(optionsName)}').";

        return new EnvarsException(
            message,
            EnvarFailureKind.InvalidProperty,
            environmentVariableName: null,
            optionsType,
            optionsName,
            propertyName: null,
            targetType: null,
            cultureName: null,
            binderType: null,
            causeType);
    }

    internal static EnvarsException PropertyMetadataFailure(
        Type optionsType,
        string optionsName,
        string propertyName,
        Type? targetType,
        string causeType)
    {
        string message =
            $"Failed to inspect environment-variable binding metadata for property " +
            $"'{optionsType.FullName}.{propertyName}' (options name '{FormatOptionsName(optionsName)}').";

        return new EnvarsException(
            message,
            EnvarFailureKind.InvalidProperty,
            environmentVariableName: null,
            optionsType,
            optionsName,
            propertyName,
            targetType,
            cultureName: null,
            binderType: null,
            causeType);
    }

    internal static EnvarsException EnvironmentReadFailure(
        string environmentVariableName,
        Type optionsType,
        string optionsName,
        string propertyName,
        Type targetType,
        string causeType)
    {
        string message =
            $"Failed to read environment variable '{environmentVariableName}' for option " +
            $"'{optionsType.FullName}.{propertyName}' (options name '{FormatOptionsName(optionsName)}').";

        return new EnvarsException(
            message,
            EnvarFailureKind.EnvironmentRead,
            environmentVariableName,
            optionsType,
            optionsName,
            propertyName,
            targetType,
            cultureName: null,
            binderType: null,
            causeType);
    }

    internal static EnvarsException ConversionFailure(
        string environmentVariableName,
        Type optionsType,
        string optionsName,
        string propertyName,
        Type targetType,
        string cultureName,
        Type binderType,
        string causeType)
    {
        string message =
            $"Failed to convert environment variable '{environmentVariableName}' to '{targetType.FullName}' " +
            $"for option '{optionsType.FullName}.{propertyName}' (options name '{FormatOptionsName(optionsName)}').";

        return new EnvarsException(
            message,
            EnvarFailureKind.Conversion,
            environmentVariableName,
            optionsType,
            optionsName,
            propertyName,
            targetType,
            cultureName,
            binderType,
            causeType);
    }

    internal static EnvarsException AssignmentFailure(
        string environmentVariableName,
        Type optionsType,
        string optionsName,
        string propertyName,
        Type targetType,
        string causeType)
    {
        string message =
            $"Failed to assign environment variable '{environmentVariableName}' to option " +
            $"'{optionsType.FullName}.{propertyName}' (options name '{FormatOptionsName(optionsName)}').";

        return new EnvarsException(
            message,
            EnvarFailureKind.Assignment,
            environmentVariableName,
            optionsType,
            optionsName,
            propertyName,
            targetType,
            cultureName: null,
            binderType: null,
            causeType);
    }

    internal static string DescribeCause(Exception exception)
    {
        var cause = exception is TargetInvocationException { InnerException: { } inner } ? inner : exception;
        return cause.GetType().FullName ?? cause.GetType().Name;
    }

    internal static string FormatOptionsName(string optionsName)
    {
        if (optionsName.Length == 0)
        {
            return "<default>";
        }

        var builder = new StringBuilder(optionsName.Length);

        foreach (char character in optionsName)
        {
            switch (character)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;

                case '\'':
                    builder.Append("\\'");
                    break;

                default:
                    if (char.IsControl(character))
                    {
                        builder.Append("\\u").Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        return builder.ToString();
    }
}
