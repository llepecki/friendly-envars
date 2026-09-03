using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace FriendlyEnvars.Tests;

/// <summary>
/// Guards the documentation against claims that were true of an earlier design and are not true now.
/// </summary>
/// <remarks>
/// Each pattern below corresponds to a behaviour the 2.0 remediation changed or removed. The search
/// covers the library's own source comments, the README and the sample, because a stale claim in any of
/// them misleads a reader just as effectively.
/// </remarks>
public class DocumentationConsistencyTests
{
    /// <summary>Walks up from the test assembly to the directory that holds the solution.</summary>
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FriendlyEnvars.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root from " + AppContext.BaseDirectory);
    }

    private static IEnumerable<(string Path, string Text)> DocumentationSources()
    {
        string root = FindRepositoryRoot();

        yield return ("README.md", File.ReadAllText(Path.Combine(root, "README.md")));

        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
                     .Concat(Directory.EnumerateFiles(Path.Combine(root, "sample"), "*.cs", SearchOption.AllDirectories))
                     .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                     .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                     .OrderBy(static path => path, StringComparer.Ordinal))
        {
            yield return (Path.GetRelativePath(root, file), File.ReadAllText(file));
        }
    }

    public static TheoryData<string, string> ForbiddenClaims() => new()
    {
        // Values are captured once, at registration, and never re-read.
        { @"refresh(es|ed)?\s+at\s+runtime", "claims values refresh at runtime" },
        { @"values?\s+(will\s+)?refresh", "claims values refresh" },
        // Negations such as "do not re-read the environment" are the correct claim, so they are excluded.
        { @"(?<!not\s)(?<!never\s)re-?reads?\s+the\s+environment", "claims the environment is re-read" },
        { @"environment\s+variables?\s+do\s+not\s+change\s+while", "asserts the environment cannot change, rather than that values are captured" },

        // An empty string is a captured value, not an absence.
        { @"not\s+set\s+or\s+is\s+empty", "treats empty as equivalent to unset" },
        { @"empty\s+(values?|strings?)\s+(are|is)\s+ignored", "claims empty values are ignored" },
        { @"empty\s+(values?|strings?)\s+(are|is)\s+treated\s+the\s+same\s+as\s+unset", "claims empty is treated as unset" },

        // The fluent methods mutate and return the same instance.
        { @"A\s+new\s+<see\s+cref=""EnvarSettings""", "claims a fluent method returns a new settings instance" },
        { @"returns?\s+a\s+new\s+.{0,20}settings\s+instance", "claims a fluent method returns a new settings instance" },

        // The blocking API was removed outright in 2.0. The removed identifiers are deliberately NOT
        // written out here: eng/verify-api-removals.sh searches tests/ for exactly those names, and the
        // specification sanctions only two exclusions from that search, neither of which is this file.
        // That gate is also strictly stronger, covering source, README, package release notes and the
        // compiled public surface. What is left here is the prose that used to describe the feature.
        { @"can\s+be\s+disabled", "claims an options abstraction can be disabled" },
        { @"(blocked|blocking)\s+by\s+calling", "describes the removed blocking configuration" },
        { @"explicitly\s+blocked", "describes the removed blocking configuration" }
    };

    [Theory]
    [MemberData(nameof(ForbiddenClaims))]
    public void DocumentationMakesNoStaleClaim(string pattern, string description)
    {
        var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var offenders = new List<string>();

        foreach (var (path, text) in DocumentationSources())
        {
            foreach (Match match in regex.Matches(text))
            {
                int line = text.Take(match.Index).Count(static character => character == '\n') + 1;
                offenders.Add($"{path}:{line}: {description} -- \"{match.Value}\"");
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void TheSearchActuallyReadsTheDocumentation()
    {
        // Guards against the search silently covering nothing, which would make every case above vacuous.
        var sources = DocumentationSources().ToArray();

        Assert.Contains(sources, source => source.Path == "README.md");
        Assert.True(sources.Length >= 8, $"expected the library and sample sources, found {sources.Length}");
        Assert.All(sources, source => Assert.False(string.IsNullOrWhiteSpace(source.Text)));

        // A claim that IS present must be found, so the matcher is known to work.
        string readme = sources.Single(static source => source.Path == "README.md").Text;

        Assert.Matches(new Regex(@"captured\s+once", RegexOptions.IgnoreCase), readme);
    }
}
