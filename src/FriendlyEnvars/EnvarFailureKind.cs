namespace FriendlyEnvars;

/// <summary>
/// Identifies the binding stage that failed.
/// </summary>
public enum EnvarFailureKind
{
    /// <summary>
    /// A mapped property or its metadata is invalid.
    /// </summary>
    InvalidProperty,

    /// <summary>
    /// Reading the environment variable failed.
    /// </summary>
    EnvironmentRead,

    /// <summary>
    /// Converting the value failed.
    /// </summary>
    Conversion,

    /// <summary>
    /// Assigning the converted value failed.
    /// </summary>
    Assignment
}
