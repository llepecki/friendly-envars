using System;
using System.Diagnostics.CodeAnalysis;

namespace FriendlyEnvars;

/// <summary>
/// Marks a property to be bound from an environment variable.
/// </summary>
/// <remarks>
/// <para>
/// This attribute specifies which environment variable should be used to populate the decorated property.
/// The property must have a setter (either <c>set</c> or <c>init</c>).
/// </para>
/// <para>
/// If the environment variable is not set, the property keeps whatever value the type gives it. An
/// empty string is a value, not an absence: it is captured and passed to the binder, which means a
/// <see cref="string"/> property becomes empty and a non-string property fails to convert.
/// </para>
/// <para>
/// The value is captured once, while <c>BindEnvars</c> runs. Changing the variable afterwards does not
/// affect any options instance.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public record DatabaseSettings
/// {
///     [Envar("DB_HOST")]
///     public string Host { get; init; } = "localhost";
///
///     [Envar("DB_PORT")]
///     public int Port { get; init; } = 5432;
///
///     [Envar("DB_SSL_ENABLED")]
///     public bool SslEnabled { get; init; } = true;
///
///     [Envar("DB_CONNECTION_TIMEOUT")]
///     public TimeSpan ConnectionTimeout { get; init; } = TimeSpan.FromSeconds(30);
/// }
/// </code>
/// <para>Usage with environment variables:</para>
/// <code>
/// DB_HOST=production.example.com
/// DB_PORT=5433
/// DB_SSL_ENABLED=false
/// DB_CONNECTION_TIMEOUT=00:01:00
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property)]
public sealed class EnvarAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the environment variable to bind from.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvarAttribute"/> class.
    /// </summary>
    /// <param name="name">The name of the environment variable to bind from.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is null, empty, whitespace only, contains <c>=</c>, or
    /// contains a Unicode control character.
    /// </exception>
    /// <example>
    /// <para>Binds the Port property to the DB_PORT environment variable:</para>
    /// <code>
    /// [Envar("DB_PORT")]
    /// public int Port { get; init; }
    /// </code>
    /// <para>Binds the ApiKey property to the API_SECRET_KEY environment variable:</para>
    /// <code>
    /// [Envar("API_SECRET_KEY")]
    /// public string ApiKey { get; init; } = string.Empty;
    /// </code>
    /// </example>
    public EnvarAttribute(string name)
    {
        if (!IsValidName(name))
        {
            throw new ArgumentException(
                "An environment-variable name must not be null, empty or whitespace only, and must not contain '=' or a Unicode control character.",
                nameof(name));
        }

        Name = name;
    }

    /// <summary>
    /// The single definition of what counts as a usable environment-variable name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Shared by this constructor and by binding, which reads the name out of metadata without
    /// constructing the attribute. Both paths therefore agree by construction.
    /// </para>
    /// <para>
    /// The rule is deliberately permissive about content and strict only about what no platform can
    /// carry: <c>=</c> separates a name from its value in every environment block, and a control
    /// character cannot survive a round trip through one. Everything else is preserved, including
    /// ordinary embedded spaces and non-ASCII letters, because operating systems differ on what they
    /// accept and the library should not invent a stricter portable subset than the contract states.
    /// </para>
    /// </remarks>
    internal static bool IsValidName([NotNullWhen(true)] string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        for (int i = 0; i < name.Length; i++)
        {
            char character = name[i];

            if (character == '=' || char.IsControl(character))
            {
                return false;
            }
        }

        return true;
    }
}
