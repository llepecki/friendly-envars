using System;
using System.Collections.Generic;

namespace FriendlyEnvars.Tests;

public abstract class EnvarTestsBase : IDisposable
{
    private readonly List<string> _environmentVariablesToCleanup = [];

    protected void SetEnvironmentVariable(string name, string? value)
    {
        Environment.SetEnvironmentVariable(name, value);
        _environmentVariablesToCleanup.Add(name);
    }

    public virtual void Dispose()
    {
        foreach (var envVar in _environmentVariablesToCleanup)
        {
            Environment.SetEnvironmentVariable(envVar, null);
        }
    }
}
