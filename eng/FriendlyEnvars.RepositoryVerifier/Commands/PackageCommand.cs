using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

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
        string? suppressionsFile = commandLine.GetOptional("suppressions-file");
        string? projectFile = commandLine.GetOptional("project");
        string? expectedBaseline = commandLine.GetOptional("expect-validation-baseline");
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

        if (suppressionsFile is not null)
        {
            VerifySuppressions(suppressionsFile, failures);
        }

        if (projectFile is not null)
        {
            VerifyProject(projectFile, expectedBaseline, suppressionsFile is null ? null : Path.GetFileName(suppressionsFile), failures);
        }

        if (failures.Count > 0)
        {
            throw new VerificationException(
                $"Package metadata verification failed for '{packagePath}':{Environment.NewLine}  - " +
                string.Join($"{Environment.NewLine}  - ", failures));
        }

        Console.WriteLine($"package OK: '{packagePath}' is {expectedId} {expectedVersion} with all declared metadata assets present.");
    }

    /// <summary>
    /// Enforces the accountability rules on the API-compatibility suppression file: one diagnostic and
    /// one concrete target per entry, marked as a baseline suppression, and justified by a comment that
    /// names the finding responsible.
    /// </summary>
    /// <remarks>
    /// ApiCompat itself rejects a suppression that no longer corresponds to a real break, so "unused"
    /// is already covered by the toolchain; what it cannot check is whether a human accounted for the
    /// break at all. That is what this adds.
    /// </remarks>
    private static void VerifySuppressions(string suppressionsFile, List<string> failures)
    {
        if (!File.Exists(suppressionsFile))
        {
            failures.Add($"suppression file '{suppressionsFile}' does not exist");
            return;
        }

        XDocument document;

        try
        {
            document = XDocument.Load(suppressionsFile, LoadOptions.SetLineInfo);
        }
        catch (System.Xml.XmlException exception)
        {
            failures.Add($"suppression file '{suppressionsFile}' is not well-formed XML: {exception.GetType().FullName}");
            return;
        }

        var justification = new Regex(@"^\s*REV-[A-Z]\d+:\s*\S", RegexOptions.CultureInvariant);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;

        foreach (var suppression in document.Descendants().Where(static element => element.Name.LocalName == "Suppression"))
        {
            index++;

            var diagnostics = suppression.Elements().Where(static e => e.Name.LocalName == "DiagnosticId").ToArray();
            var targets = suppression.Elements().Where(static e => e.Name.LocalName == "Target").ToArray();

            if (diagnostics.Length != 1)
            {
                failures.Add($"suppression #{index} declares {diagnostics.Length} <DiagnosticId> elements; exactly 1 is required");
                continue;
            }

            if (targets.Length != 1)
            {
                failures.Add($"suppression #{index} declares {targets.Length} <Target> elements; exactly 1 is required");
                continue;
            }

            string diagnosticId = diagnostics[0].Value.Trim();
            string target = targets[0].Value.Trim();

            if (target.Length == 0 || target.Contains('*', StringComparison.Ordinal))
            {
                failures.Add($"suppression #{index} ('{diagnosticId}') has a blanket target '{target}'; each entry must name one member");
            }

            string key = diagnosticId + "\u0001" + target;

            if (!seen.Add(key))
            {
                failures.Add($"suppression for '{diagnosticId}' on '{target}' is declared more than once");
            }

            var baseline = suppression.Elements().FirstOrDefault(static e => e.Name.LocalName == "IsBaselineSuppression");

            if (baseline is null || !string.Equals(baseline.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"suppression for '{diagnosticId}' on '{target}' is missing <IsBaselineSuppression>true</IsBaselineSuppression>");
            }

            // The justification must be the comment immediately before the entry, so it cannot drift
            // away from what it justifies.
            var previous = suppression.PreviousNode;

            while (previous is XText text && string.IsNullOrWhiteSpace(text.Value))
            {
                previous = previous.PreviousNode;
            }

            if (previous is not XComment comment || !justification.IsMatch(comment.Value))
            {
                failures.Add(
                    $"suppression for '{diagnosticId}' on '{target}' is not immediately preceded by a " +
                    "comment of the form 'REV-ID: justification'");
            }
        }

        if (index == 0)
        {
            failures.Add($"suppression file '{suppressionsFile}' declares no suppressions; remove the file instead");
        }
    }

    /// <summary>
    /// Checks the packaging properties that decide whether validation runs at all, and that no
    /// compatibility diagnostic has been silenced through NoWarn.
    /// </summary>
    private static void VerifyProject(string projectFile, string? expectedBaseline, string? suppressionsFileName, List<string> failures)
    {
        if (!File.Exists(projectFile))
        {
            failures.Add($"project '{projectFile}' does not exist");
            return;
        }

        var document = XDocument.Load(projectFile);

        // MSBuild evaluates properties last-wins, so reading the first occurrence would let a later
        // PropertyGroup silently override a value this gate believes it has checked. A conditional or
        // repeated declaration is rejected outright rather than guessed at, because its effective value
        // depends on the build rather than on the file.
        string? Property(string name)
        {
            var elements = document
                .Descendants()
                .Where(element => element.Name.LocalName == name)
                .ToArray();

            if (elements.Length == 0)
            {
                return null;
            }

            if (elements.Length > 1)
            {
                failures.Add($"<{name}> is declared {elements.Length} times; declare it exactly once");
            }

            foreach (var element in elements)
            {
                if (element.Attribute("Condition") is not null)
                {
                    failures.Add($"<{name}> carries a Condition, so its effective value cannot be determined from the file");
                }
            }

            return elements[^1].Value.Trim();
        }

        if (!string.Equals(Property("EnablePackageValidation"), "true", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("<EnablePackageValidation> is not set to true");
        }

        if (expectedBaseline is not null &&
            !string.Equals(Property("PackageValidationBaselineVersion"), expectedBaseline, StringComparison.Ordinal))
        {
            failures.Add(
                $"<PackageValidationBaselineVersion> is '{Property("PackageValidationBaselineVersion") ?? "<absent>"}', " +
                $"expected '{expectedBaseline}'");
        }

        var suppressionItems = document
            .Descendants()
            .Where(static element => element.Name.LocalName == "ApiCompatSuppressionFile")
            .Select(static element => element.Attribute("Include")?.Value)
            .Where(static include => !string.IsNullOrWhiteSpace(include))
            .ToArray();

        if (suppressionItems.Length == 0)
        {
            failures.Add("no <ApiCompatSuppressionFile> item is declared");
        }
        else if (suppressionsFileName is not null &&
                 !suppressionItems.Any(include => string.Equals(
                     Path.GetFileName(include!.Replace('\\', '/')), suppressionsFileName, StringComparison.Ordinal)))
        {
            // Otherwise the build could consume one file while this gate audits another.
            failures.Add(
                $"no <ApiCompatSuppressionFile> item points at '{suppressionsFileName}'; " +
                $"declared: {string.Join(", ", suppressionItems)}");
        }

        // Generating suppressions automatically would let a break be waved through by a rebuild.
        if (Property("ApiCompatGenerateSuppressionFile") is not null)
        {
            failures.Add("<ApiCompatGenerateSuppressionFile> must not be set in committed configuration");
        }

        var compatibilityDiagnostic = new Regex(@"\b(CP|PKV)\d{4}\b", RegexOptions.CultureInvariant);

        foreach (var noWarn in document.Descendants().Where(static element => element.Name.LocalName == "NoWarn"))
        {
            var match = compatibilityDiagnostic.Match(noWarn.Value);

            if (match.Success)
            {
                failures.Add($"<NoWarn> silences the compatibility diagnostic '{match.Value}'");
            }
        }
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
