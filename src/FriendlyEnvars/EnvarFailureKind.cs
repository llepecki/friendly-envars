namespace FriendlyEnvars;

/// <summary>
/// Identifies which stage of environment-variable binding produced an <see cref="EnvarsException"/>.
/// </summary>
/// <remarks>
/// The value is exposed through <see cref="EnvarsException.FailureKind"/>. It lets callers branch on the
/// cause of a failure without parsing the message, which deliberately never contains the offending value.
/// </remarks>
public enum EnvarFailureKind
{
    /// <summary>
    /// A property decorated with <see cref="EnvarAttribute"/> cannot be used as a bind target, or its
    /// binding metadata could not be inspected.
    /// </summary>
    InvalidProperty,

    /// <summary>
    /// Reading the environment variable itself failed.
    /// </summary>
    EnvironmentRead,

    /// <summary>
    /// The environment variable's value could not be converted to the property's type.
    /// </summary>
    Conversion,

    /// <summary>
    /// The converted value could not be assigned to the property.
    /// </summary>
    Assignment
}
