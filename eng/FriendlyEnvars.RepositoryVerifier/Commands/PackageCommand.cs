using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FriendlyEnvars.RepositoryVerifier.Commands;

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
        var expectedDependencies = commandLine.GetMany("expect-dependency");
        var expectedMetadata = commandLine.GetMany("expect-metadata");
        string? expectedRepositoryUrl = commandLine.GetOptional("expect-repository-url");
        var expectedProperties = commandLine.GetMany("expect-property");
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

        if (expectedDependencies.Count > 0)
        {
            VerifyDependencies(package, expectedDependencies, failures);
        }

        foreach (string expectation in expectedMetadata)
        {
            var (name, value) = SplitExpectation(expectation, "expect-metadata");
            string? actual = package.GetMetadata(name);

            if (!string.Equals(actual, value, StringComparison.Ordinal))
            {
                failures.Add($"<{name}> is '{actual ?? "<absent>"}', expected '{value}'");
            }
        }

        if (expectedRepositoryUrl is not null)
        {
            VerifyRepository(package, expectedRepositoryUrl, failures);
        }

        if (suppressionsFile is not null)
        {
            VerifySuppressions(suppressionsFile, failures);
        }

        if (projectFile is not null)
        {
            VerifyProject(projectFile, expectedBaseline, suppressionsFile is null ? null : Path.GetFileName(suppressionsFile), expectedProperties, failures);
        }
        else if (expectedProperties.Count > 0)
        {
            failures.Add("--expect-property requires --project");
        }

        if (failures.Count > 0)
        {
            throw new VerificationException(
                $"Package metadata verification failed for '{packagePath}':{Environment.NewLine}  - " +
                string.Join($"{Environment.NewLine}  - ", failures));
        }

        Console.WriteLine($"package OK: '{packagePath}' is {expectedId} {expectedVersion} with all declared metadata assets present.");
    }

    private static (string Name, string Value) SplitExpectation(string expectation, string optionName)
    {
        int separator = expectation.IndexOf('=', StringComparison.Ordinal);

        if (separator <= 0 || separator == expectation.Length - 1)
        {
            throw new VerificationException($"--{optionName} '{expectation}' is not of the form name=value.");
        }

        return (expectation[..separator], expectation[(separator + 1)..]);
    }

    private static void VerifyRepository(NuGetPackage package, string expectedUrl, List<string> failures)
    {
        var repositories = package.Metadata
            .Elements()
            .Where(static element => element.Name.LocalName == "repository")
            .ToArray();

        if (repositories.Length != 1)
        {
            failures.Add($"the .nuspec declares {repositories.Length} <repository> elements; exactly 1 is required");
            return;
        }

        string? url = repositories[0].Attribute("url")?.Value;

        if (!string.Equals(url, expectedUrl, StringComparison.Ordinal))
        {
            failures.Add($"<repository> url is '{url ?? "<absent>"}', expected '{expectedUrl}'");
        }

        string? commit = repositories[0].Attribute("commit")?.Value;

        if (commit is null || commit.Length != 40 || !commit.All(static character => Uri.IsHexDigit(character)))
        {
            failures.Add($"<repository> commit is '{commit ?? "<absent>"}', expected a 40-character commit SHA");
        }
    }

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

            // Keep each justification next to its suppression.
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

    private static void VerifyProject(string projectFile, string? expectedBaseline, string? suppressionsFileName, IReadOnlyList<string> expectedProperties, List<string> failures)
    {
        if (!File.Exists(projectFile))
        {
            failures.Add($"project '{projectFile}' does not exist");
            return;
        }

        var document = XDocument.Load(projectFile);

        // Conditional or repeated properties have no single value this file-level gate can verify.
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

        foreach (string expectation in expectedProperties)
        {
            var (name, value) = SplitExpectation(expectation, "expect-property");
            string? actual = Property(name);

            if (!string.Equals(actual, value, StringComparison.Ordinal))
            {
                failures.Add($"<{name}> is '{actual ?? "<absent>"}', expected '{value}'");
            }
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
            // The build and this gate must use the same suppression file.
            failures.Add(
                $"no <ApiCompatSuppressionFile> item points at '{suppressionsFileName}'; " +
                $"declared: {string.Join(", ", suppressionItems)}");
        }

        // Suppressions must be reviewed and committed.
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

    private static void VerifyDependencies(NuGetPackage package, IReadOnlyList<string> expected, List<string> failures)
    {
        var actual = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        var declared = package.Metadata
            .Elements()
            .Where(static element => element.Name.LocalName == "dependencies")
            .SelectMany(static group => group.Descendants())
            .Where(static element => element.Name.LocalName == "dependency");

        foreach (var dependency in declared)
        {
            string? id = dependency.Attribute("id")?.Value?.Trim();

            if (string.IsNullOrEmpty(id))
            {
                failures.Add("a <dependency> element declares no id");
                continue;
            }

            actual.Add(id);
        }

        var expectedSet = new SortedSet<string>(expected, StringComparer.OrdinalIgnoreCase);

        foreach (string missing in expectedSet.Except(actual, StringComparer.OrdinalIgnoreCase))
        {
            failures.Add($"expected package dependency '{missing}' is not declared");
        }

        foreach (string unexpected in actual.Except(expectedSet, StringComparer.OrdinalIgnoreCase))
        {
            failures.Add(
                $"package declares an unexpected dependency '{unexpected}'; a development-only reference " +
                "must keep <PrivateAssets>all</PrivateAssets> so it never reaches consumers");
        }
    }

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
