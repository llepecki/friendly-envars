using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace FriendlyEnvars.Tests;

public class DocumentationConsistencyTests
{
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
        // Values are captured once.
        { @"refresh(es|ed)?\s+at\s+runtime", "claims values refresh at runtime" },
        { @"values?\s+(will\s+)?refresh", "claims values refresh" },
        // Allow correct negations such as "never re-read."
        { @"(?<!not\s)(?<!never\s)re-?reads?\s+the\s+environment", "claims the environment is re-read" },
        { @"environment\s+variables?\s+do\s+not\s+change\s+while", "asserts the environment cannot change, rather than that values are captured" },

        // Empty strings are values.
        { @"not\s+set\s+or\s+is\s+empty", "treats empty as equivalent to unset" },
        { @"empty\s+(values?|strings?)\s+(are|is)\s+ignored", "claims empty values are ignored" },
        { @"empty\s+(values?|strings?)\s+(are|is)\s+treated\s+the\s+same\s+as\s+unset", "claims empty is treated as unset" },

        // Fluent methods return the same instance.
        { @"A\s+new\s+<see\s+cref=""EnvarSettings""", "claims a fluent method returns a new settings instance" },
        { @"returns?\s+a\s+new\s+.{0,20}settings\s+instance", "claims a fluent method returns a new settings instance" },

        // The API-removal gate checks removed identifiers; these patterns check stale prose.
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
        var sources = DocumentationSources().ToArray();

        Assert.Contains(sources, source => source.Path == "README.md");
        Assert.True(sources.Length >= 8, $"expected the library and sample sources, found {sources.Length}");
        Assert.All(sources, source => Assert.False(string.IsNullOrWhiteSpace(source.Text)));

        // Prove the matcher reads real content.
        string readme = sources.Single(static source => source.Path == "README.md").Text;

        Assert.Matches(new Regex(@"captured\s+once", RegexOptions.IgnoreCase), readme);
    }
}
