using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FriendlyEnvars.RepositoryVerifier.Commands;

internal static class PackageManifestCommand
{
    public static void Run(CommandLine commandLine)
    {
        string packagePath = commandLine.GetRequired("package");
        var required = commandLine.GetMany("require");
        var forbidden = commandLine.GetMany("forbid");
        commandLine.EnsureAllConsumed();

        if (required.Count == 0)
        {
            throw new VerificationException("At least one --require package path must be supplied.");
        }

        using var package = NuGetPackage.Open(packagePath);

        var failures = new List<string>();

        var duplicates = package.Entries
            .GroupBy(static entry => entry, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        foreach (string duplicate in duplicates)
        {
            failures.Add($"archive entry '{duplicate}' appears more than once");
        }

        foreach (string entry in required)
        {
            int count = package.CountEntries(entry);

            if (count != 1)
            {
                failures.Add(
                    $"required package path '{entry}' appears {count.ToString(CultureInfo.InvariantCulture)} time(s); exactly 1 is required");
            }
        }

        foreach (string entry in forbidden)
        {
            int count = package.CountEntries(entry);

            if (count != 0)
            {
                failures.Add($"forbidden package path '{entry}' is present");
            }
        }

        if (failures.Count > 0)
        {
            Console.Error.WriteLine($"Package manifest of '{packagePath}':");

            foreach (string entry in package.Entries.OrderBy(static e => e, StringComparer.Ordinal))
            {
                Console.Error.WriteLine($"  {entry}");
            }

            throw new VerificationException(
                $"Package manifest verification failed for '{packagePath}':{Environment.NewLine}  - " +
                string.Join($"{Environment.NewLine}  - ", failures));
        }

        Console.WriteLine(
            $"package-manifest OK: '{packagePath}' contains all {required.Count.ToString(CultureInfo.InvariantCulture)} required path(s) exactly once.");
    }
}
