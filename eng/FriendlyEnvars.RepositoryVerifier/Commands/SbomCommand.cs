using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FriendlyEnvars.RepositoryVerifier.Commands;

/// <summary>
/// Verifies the generated SBOM: valid SPDX 2.2 JSON, the document identity, both NuGet artifact file
/// names, and a DEPENDS_ON relationship from the root package to each declared dependency.
/// </summary>
internal static class SbomCommand
{
    public static void Run(CommandLine commandLine)
    {
        string sbomPath = commandLine.GetRequired("sbom");
        string expectedName = commandLine.GetRequired("expect-name");
        string expectedVersion = commandLine.GetRequired("expect-version");
        var expectedFiles = commandLine.GetMany("expect-file");
        var expectedDependencies = commandLine.GetMany("expect-dependency");
        commandLine.EnsureAllConsumed();

        if (expectedFiles.Count == 0)
        {
            throw new VerificationException("At least one --expect-file is required.");
        }

        if (!File.Exists(sbomPath))
        {
            throw new VerificationException($"SBOM '{sbomPath}' does not exist.");
        }

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(File.ReadAllText(sbomPath));
        }
        catch (JsonException exception)
        {
            throw new VerificationException($"SBOM '{sbomPath}' is not valid JSON: {exception.GetType().FullName}.");
        }

        var failures = new List<string>();

        using (document)
        {
            var root = document.RootElement;

            string? spdxVersion = GetString(root, "spdxVersion");

            if (!string.Equals(spdxVersion, "SPDX-2.2", StringComparison.Ordinal))
            {
                failures.Add($"spdxVersion is '{spdxVersion ?? "<absent>"}', expected 'SPDX-2.2'");
            }

            string? name = GetString(root, "name");
            string expectedDocumentName = $"{expectedName} {expectedVersion}";

            if (!string.Equals(name, expectedDocumentName, StringComparison.Ordinal))
            {
                failures.Add($"document name is '{name ?? "<absent>"}', expected '{expectedDocumentName}'");
            }

            VerifyFiles(root, expectedFiles, failures);
            VerifyDependencies(root, expectedName, expectedVersion, expectedDependencies, failures);
        }

        if (failures.Count > 0)
        {
            throw new VerificationException(
                $"SBOM verification failed for '{sbomPath}':{Environment.NewLine}  - " +
                string.Join($"{Environment.NewLine}  - ", failures));
        }

        Console.WriteLine($"sbom OK: '{sbomPath}' describes {expectedName} {expectedVersion} with all expected files and dependency relationships.");
    }

    private static void VerifyFiles(JsonElement root, IReadOnlyList<string> expectedFiles, List<string> failures)
    {
        var fileNames = new HashSet<string>(StringComparer.Ordinal);

        if (root.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array)
        {
            foreach (var file in files.EnumerateArray())
            {
                string? fileName = GetString(file, "fileName");

                if (fileName is not null)
                {
                    // The generator prefixes relative names with "./".
                    fileNames.Add(fileName.StartsWith("./", StringComparison.Ordinal) ? fileName[2..] : fileName);
                }
            }
        }

        foreach (string expected in expectedFiles)
        {
            if (!fileNames.Contains(expected))
            {
                failures.Add($"file '{expected}' is not listed; listed: {string.Join(", ", fileNames.Order(StringComparer.Ordinal))}");
            }
        }
    }

    /// <summary>
    /// Resolves the root package through documentDescribes and requires each expected "id=version"
    /// dependency to be reachable from it through the DEPENDS_ON closure. Reachability rather than a
    /// direct edge, measured against the generator's real output: sbom-tool 4.1.5 omits a direct
    /// dependency from the root's edges when the same package also appears transitively, so the
    /// declared Microsoft.Extensions.Options dependency is reachable only through
    /// Microsoft.Extensions.Hosting. A dependency absent from the closure still fails.
    /// </summary>
    private static void VerifyDependencies(
        JsonElement root, string expectedRootName, string expectedRootVersion,
        IReadOnlyList<string> expectedDependencies, List<string> failures)
    {
        if (!root.TryGetProperty("documentDescribes", out var describes) ||
            describes.ValueKind != JsonValueKind.Array || describes.GetArrayLength() != 1)
        {
            failures.Add("documentDescribes does not name exactly one root package");
            return;
        }

        string rootId = describes[0].GetString() ?? string.Empty;
        var packagesById = new Dictionary<string, (string? Name, string? Version)>(StringComparer.Ordinal);

        if (root.TryGetProperty("packages", out var packages) && packages.ValueKind == JsonValueKind.Array)
        {
            foreach (var package in packages.EnumerateArray())
            {
                string? id = GetString(package, "SPDXID");

                if (id is not null)
                {
                    packagesById[id] = (GetString(package, "name"), GetString(package, "versionInfo"));
                }
            }
        }

        if (!packagesById.TryGetValue(rootId, out var rootPackage))
        {
            failures.Add($"root package '{rootId}' has no package node");
            return;
        }

        if (!string.Equals(rootPackage.Name, expectedRootName, StringComparison.Ordinal))
        {
            failures.Add($"root package name is '{rootPackage.Name ?? "<absent>"}', expected '{expectedRootName}'");
        }

        if (!string.Equals(rootPackage.Version, expectedRootVersion, StringComparison.Ordinal))
        {
            failures.Add($"root package version is '{rootPackage.Version ?? "<absent>"}', expected '{expectedRootVersion}'");
        }

        var edges = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        if (root.TryGetProperty("relationships", out var relationships) && relationships.ValueKind == JsonValueKind.Array)
        {
            foreach (var relationship in relationships.EnumerateArray())
            {
                if (string.Equals(GetString(relationship, "relationshipType"), "DEPENDS_ON", StringComparison.Ordinal))
                {
                    string? source = GetString(relationship, "spdxElementId");
                    string? target = GetString(relationship, "relatedSpdxElement");

                    if (source is not null && target is not null)
                    {
                        (edges.TryGetValue(source, out var targets) ? targets : edges[source] = []).Add(target);
                    }
                }
            }
        }

        var dependencyTargets = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>();
        pending.Enqueue(rootId);

        while (pending.TryDequeue(out string? current))
        {
            if (!edges.TryGetValue(current, out var targets))
            {
                continue;
            }

            foreach (string target in targets)
            {
                if (dependencyTargets.Add(target))
                {
                    pending.Enqueue(target);
                }
            }
        }

        foreach (string expected in expectedDependencies)
        {
            int separator = expected.IndexOf('=', StringComparison.Ordinal);

            if (separator <= 0 || separator == expected.Length - 1)
            {
                throw new VerificationException($"--expect-dependency '{expected}' is not of the form id=version.");
            }

            string expectedId = expected[..separator];
            string expectedVersion = expected[(separator + 1)..];

            bool found = dependencyTargets
                .Select(target => packagesById.GetValueOrDefault(target))
                .Any(package => string.Equals(package.Name, expectedId, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(package.Version, expectedVersion, StringComparison.Ordinal));

            if (!found)
            {
                failures.Add($"'{expectedId}' {expectedVersion} is not reachable from the root package through DEPENDS_ON");
            }
        }
    }

    private static string? GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
