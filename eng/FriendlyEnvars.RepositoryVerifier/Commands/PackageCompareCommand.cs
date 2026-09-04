using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace FriendlyEnvars.RepositoryVerifier.Commands;

internal static class PackageCompareCommand
{
    public static void Run(CommandLine commandLine, bool exemptRepositorySignature)
    {
        string leftPath = commandLine.GetRequired("left");
        string rightPath = commandLine.GetRequired("right");
        commandLine.EnsureAllConsumed();

        using var left = NuGetPackage.Open(leftPath);
        using var right = NuGetPackage.Open(rightPath);

        var failures = new List<string>();

        var leftManifest = Manifest(left, exemptRepositorySignature);
        var rightManifest = Manifest(right, exemptRepositorySignature);

        foreach (string missing in leftManifest.Keys.Except(rightManifest.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            failures.Add($"'{missing}' is present in '{leftPath}' but missing from '{rightPath}'");
        }

        foreach (string extra in rightManifest.Keys.Except(leftManifest.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            failures.Add($"'{extra}' is present in '{rightPath}' but missing from '{leftPath}'");
        }

        foreach (string common in leftManifest.Keys.Intersect(rightManifest.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!string.Equals(leftManifest[common], rightManifest[common], StringComparison.Ordinal))
            {
                failures.Add($"'{common}' differs: {leftManifest[common]} vs {rightManifest[common]}");
            }
        }

        if (failures.Count > 0)
        {
            throw new VerificationException(
                $"Packages '{leftPath}' and '{rightPath}' are not equivalent:{Environment.NewLine}  - " +
                string.Join($"{Environment.NewLine}  - ", failures));
        }

        string subcommand = exemptRepositorySignature ? "published-package" : "reproducible-package";

        Console.WriteLine(
            $"{subcommand} OK: {leftManifest.Count} entr(ies) identical between '{leftPath}' and '{rightPath}'" +
            (exemptRepositorySignature ? " (.signature.p7s exempt)." : "."));
    }

    private static Dictionary<string, string> Manifest(NuGetPackage package, bool exemptRepositorySignature)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (string entry in package.Entries)
        {
            if (exemptRepositorySignature && string.Equals(entry, ".signature.p7s", StringComparison.Ordinal))
            {
                continue;
            }

            result[entry] = Convert.ToHexString(SHA256.HashData(package.ReadEntry(entry)));
        }

        return result;
    }
}
