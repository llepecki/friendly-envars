using System;

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
/// If the environment variable is not set or is empty, the property will retain its default value.
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
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or empty.</exception>
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
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Name can't be null or empty", nameof(name));
        }

        Name = name;
    }
}
