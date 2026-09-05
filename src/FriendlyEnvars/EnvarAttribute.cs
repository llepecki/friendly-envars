using System;
using System.Diagnostics.CodeAnalysis;

namespace FriendlyEnvars;

/// <summary>
/// Maps a property to an environment variable.
/// </summary>
/// <remarks>
/// The property must be public and have a public <c>set</c> or <c>init</c> accessor.
/// <c>BindEnvars</c> captures the value once. An unset variable keeps the property default; an empty
/// string is passed to the binder.
/// </remarks>
/// <example>
/// <code>
/// public record DatabaseSettings
/// {
///     [Envar("DB_HOST")]
///     public string Host { get; init; } = "localhost";
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property)]
public sealed class EnvarAttribute : Attribute
{
    /// <summary>
    /// Gets the environment-variable name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Creates a property mapping.
    /// </summary>
    /// <param name="name">The name of the environment variable to bind from.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is null, empty, whitespace only, contains <c>=</c>, or
    /// contains a Unicode control character.
    /// </exception>
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
