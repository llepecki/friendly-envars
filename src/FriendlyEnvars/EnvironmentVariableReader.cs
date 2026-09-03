using System;

namespace FriendlyEnvars;

/// <summary>
/// Reads a single environment variable. Exists so the one place the library touches the process
/// environment can be substituted in tests, which makes read counts and read failures observable.
/// </summary>
/// <remarks>
/// This seam is internal on purpose. The public surface must not offer a way to redirect where
/// configuration comes from.
/// </remarks>
internal interface IEnvironmentVariableReader
{
    /// <summary>
    /// Returns the value of the named environment variable, or <see langword="null"/> when it is not set.
    /// </summary>
    string? GetEnvironmentVariable(string name);
}

/// <summary>
/// The production reader. Delegates straight to the process environment.
/// </summary>
internal sealed class ProcessEnvironmentVariableReader : IEnvironmentVariableReader
{
    internal static readonly ProcessEnvironmentVariableReader Instance = new();

    private ProcessEnvironmentVariableReader()
    {
    }

    public string? GetEnvironmentVariable(string name)
    {
        return Environment.GetEnvironmentVariable(name);
    }
}
