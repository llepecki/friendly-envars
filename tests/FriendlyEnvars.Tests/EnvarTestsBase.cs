using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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

    // Explicit values preserve empty strings consistently across target frameworks.
    protected static OptionsBuilder<T> BindCapturedEnvironment<T>(
        IServiceCollection services,
        Dictionary<string, string?> capturedValues,
        string optionsName = "") where T : class, new()
    {
        return OptionsBuilderExtensions.BindEnvarsCore(
            services.AddOptions<T>(optionsName),
            configure: null,
            new CapturedEnvironmentReader(capturedValues),
            NullBindingPlanObserver.Instance);
    }

    private sealed class CapturedEnvironmentReader : IEnvironmentVariableReader
    {
        private readonly Dictionary<string, string?> _values;

        public CapturedEnvironmentReader(Dictionary<string, string?> values)
        {
            _values = values;
        }

        public string? GetEnvironmentVariable(string name)
        {
            return _values.GetValueOrDefault(name);
        }
    }

    public virtual void Dispose()
    {
        foreach (var envVar in _environmentVariablesToCleanup)
        {
            Environment.SetEnvironmentVariable(envVar, null);
        }
    }
}
