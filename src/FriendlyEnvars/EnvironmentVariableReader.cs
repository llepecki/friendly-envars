using System;

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
