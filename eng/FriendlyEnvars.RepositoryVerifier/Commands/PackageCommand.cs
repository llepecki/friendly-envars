using System;
using System.Collections.Generic;

namespace FriendlyEnvars.RepositoryVerifier.Commands;

/// <summary>
/// Asserts NuGet package metadata read from the packed .nuspec, including that every declared metadata
/// asset (icon, readme) is actually carried in the package. A declared-but-absent asset is the NU5046
/// condition that made the reviewed package unbuildable.
/// </summary>
internal static class PackageCommand
{
    public static void Run(CommandLine commandLine)
    {
        string packagePath = commandLine.GetRequired("package");
        string expectedId = commandLine.GetRequired("expect-id");
        string expectedVersion = commandLine.GetRequired("expect-version");
        string? expectedIcon = commandLine.GetOptional("expect-icon");
        string? expectedReadme = commandLine.GetOptional("expect-readme");
        commandLine.EnsureAllConsumed();

        using var package = NuGetPackage.Open(packagePath);

        var failures = new List<string>();

        string? actualId = package.GetMetadata("id");

        if (!string.Equals(actualId, expectedId, StringComparison.Ordinal))
        {
            failures.Add($"package id is '{actualId ?? "<absent>"}', expected '{expectedId}'");
        }

        string? actualVersion = package.GetMetadata("version");

        if (!string.Equals(actualVersion, expectedVersion, StringComparison.Ordinal))
        {
            failures.Add($"package version is '{actualVersion ?? "<absent>"}', expected '{expectedVersion}'");
        }

        VerifyMetadataAsset(package, "icon", expectedIcon, failures);
        VerifyMetadataAsset(package, "readme", expectedReadme, failures);

        if (failures.Count > 0)
        {
            throw new VerificationException(
                $"Package metadata verification failed for '{packagePath}':{Environment.NewLine}  - " +
                string.Join($"{Environment.NewLine}  - ", failures));
        }

        Console.WriteLine($"package OK: '{packagePath}' is {expectedId} {expectedVersion} with all declared metadata assets present.");
    }

    /// <summary>
    /// Verifies a metadata element that names a file inside the package. The declared value must match
    /// what the repository expects, and the named file must be carried exactly once.
    /// </summary>
    private static void VerifyMetadataAsset(NuGetPackage package, string elementName, string? expectedValue, List<string> failures)
    {
        string? declared = package.GetMetadata(elementName);

        if (expectedValue is not null && !string.Equals(declared, expectedValue, StringComparison.Ordinal))
        {
            failures.Add($"<{elementName}> is '{declared ?? "<absent>"}', expected '{expectedValue}'");
            return;
        }

        if (declared is null)
        {
            return;
        }

        int count = package.CountEntries(declared);

        if (count != 1)
        {
            failures.Add(
                $"<{elementName}> declares '{declared}' but that file is present {count} time(s) in the package; exactly 1 is required");
        }
    }
}
