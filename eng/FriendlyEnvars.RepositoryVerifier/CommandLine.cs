using System;
using System.Collections.Generic;
using System.Globalization;

namespace FriendlyEnvars.RepositoryVerifier;

internal sealed class CommandLine
{
    private readonly Dictionary<string, List<string>> _values = new(StringComparer.Ordinal);
    private readonly HashSet<string> _consumed = new(StringComparer.Ordinal);

    private CommandLine()
    {
    }

    public static CommandLine Parse(IReadOnlyList<string> args)
    {
        var result = new CommandLine();

        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];

            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                throw new VerificationException($"Unexpected positional argument '{arg}'; every option must be passed as '--name value'.");
            }

            string name = arg[2..];

            if (name.Length == 0)
            {
                throw new VerificationException("Encountered an empty option name '--'.");
            }

            string? value = null;

            if (i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[i + 1];
                i++;
            }

            if (!result._values.TryGetValue(name, out var list))
            {
                list = new List<string>();
                result._values[name] = list;
            }

            list.Add(value ?? string.Empty);
        }

        return result;
    }

    public bool HasSwitch(string name)
    {
        _consumed.Add(name);
        return _values.ContainsKey(name);
    }

    public string? GetOptional(string name)
    {
        _consumed.Add(name);

        if (!_values.TryGetValue(name, out var list))
        {
            return null;
        }

        if (list.Count != 1)
        {
            throw new VerificationException($"Option '--{name}' was supplied {list.Count.ToString(CultureInfo.InvariantCulture)} times; exactly one value is required.");
        }

        if (list[0].Length == 0)
        {
            throw new VerificationException($"Option '--{name}' requires a value.");
        }

        return list[0];
    }

    public string GetRequired(string name)
    {
        return GetOptional(name) ?? throw new VerificationException($"Required option '--{name}' was not supplied.");
    }

    public IReadOnlyList<string> GetMany(string name)
    {
        _consumed.Add(name);

        if (!_values.TryGetValue(name, out var list))
        {
            return Array.Empty<string>();
        }

        foreach (string value in list)
        {
            if (value.Length == 0)
            {
                throw new VerificationException($"Option '--{name}' requires a value.");
            }
        }

        return list;
    }

    public void EnsureAllConsumed()
    {
        var unknown = new List<string>();

        foreach (string name in _values.Keys)
        {
            if (!_consumed.Contains(name))
            {
                unknown.Add("--" + name);
            }
        }

        if (unknown.Count > 0)
        {
            unknown.Sort(StringComparer.Ordinal);
            throw new VerificationException($"Unknown option(s): {string.Join(", ", unknown)}.");
        }
    }
}
