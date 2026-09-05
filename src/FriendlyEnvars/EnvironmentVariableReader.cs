using System;
using System.Collections.Generic;

namespace FriendlyEnvars;

internal interface IEnvironmentVariableReader
{
    string? GetEnvironmentVariable(string name);
}

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

/// <summary>Serves EnvarSettings.UseEnvironmentSource: a fixed snapshot instead of the process environment.</summary>
internal sealed class SnapshotEnvironmentVariableReader : IEnvironmentVariableReader
{
    private readonly Dictionary<string, string?> _variables;

    internal SnapshotEnvironmentVariableReader(Dictionary<string, string?> variables)
    {
        _variables = variables;
    }

    public string? GetEnvironmentVariable(string name)
    {
        return _variables.GetValueOrDefault(name);
    }
}
