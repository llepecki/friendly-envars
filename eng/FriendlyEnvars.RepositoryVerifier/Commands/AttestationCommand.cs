using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace FriendlyEnvars.RepositoryVerifier.Commands;

/// <summary>
/// Verifies the JSON that `gh attestation verify --format json` produced for one artifact: exactly one
/// verified result per file, the requested predicate type, exactly one subject whose name and SHA-256
/// match the artifact on disk and its SHA256SUMS entry, and the exact source repository, ref, commit
/// and signer workflow.
/// </summary>
/// <remarks>
/// Identity is read exclusively from the verification result's certificate fields, which gh checked
/// against the signature. Predicate fields are attacker-controlled input to the attestation and are
/// never used as proof of signer identity.
/// </remarks>
internal static class AttestationCommand
{
    private const string ProvenancePredicateType = "https://slsa.dev/provenance/v1";
    private const string SbomPredicateType = "https://spdx.dev/Document/v2.3";

    public static void Run(CommandLine commandLine)
    {
        string artifactPath = commandLine.GetRequired("artifact");
        string provenancePath = commandLine.GetRequired("provenance");
        string sbomAttestationPath = commandLine.GetRequired("sbom-attestation");
        string checksumsPath = commandLine.GetRequired("checksums");
        string repository = commandLine.GetRequired("repository");
        string sourceRef = commandLine.GetRequired("source-ref");
        string commit = commandLine.GetRequired("commit");
        string signerWorkflow = commandLine.GetRequired("signer-workflow");
        commandLine.EnsureAllConsumed();

        if (!File.Exists(artifactPath))
        {
            throw new VerificationException($"Artifact '{artifactPath}' does not exist.");
        }

        if (commit.Length != 40 || !commit.All(Uri.IsHexDigit))
        {
            throw new VerificationException($"--commit '{commit}' is not a 40-character commit SHA.");
        }

        var failures = new List<string>();

        string artifactName = Path.GetFileName(artifactPath);
        string actualDigest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(artifactPath))).ToLowerInvariant();

        VerifyChecksumsEntry(checksumsPath, artifactName, actualDigest, failures);

        VerifyFile(provenancePath, ProvenancePredicateType, artifactName, actualDigest,
            repository, sourceRef, commit, signerWorkflow, failures);
        VerifyFile(sbomAttestationPath, SbomPredicateType, artifactName, actualDigest,
            repository, sourceRef, commit, signerWorkflow, failures);

        if (failures.Count > 0)
        {
            throw new VerificationException(
                $"Attestation verification failed for '{artifactPath}':{Environment.NewLine}  - " +
                string.Join($"{Environment.NewLine}  - ", failures));
        }

        Console.WriteLine(
            $"attestation OK: '{artifactName}' ({actualDigest}) has one verified provenance and one verified " +
            $"SBOM attestation from {signerWorkflow}@{sourceRef} at {commit}.");
    }

    private static void VerifyChecksumsEntry(string checksumsPath, string artifactName, string actualDigest, List<string> failures)
    {
        if (!File.Exists(checksumsPath))
        {
            failures.Add($"checksums file '{checksumsPath}' does not exist");
            return;
        }

        var entries = File.ReadAllLines(checksumsPath)
            .Select(static line => line.Trim())
            .Where(static line => line.Length > 0)
            .Select(static line => line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(static parts => parts.Length == 2)
            .ToDictionary(
                static parts => parts[1].TrimStart('*'),
                static parts => parts[0].ToLowerInvariant(),
                StringComparer.Ordinal);

        if (!entries.TryGetValue(artifactName, out string? recorded))
        {
            failures.Add($"'{checksumsPath}' has no entry for '{artifactName}'");
        }
        else if (!string.Equals(recorded, actualDigest, StringComparison.Ordinal))
        {
            failures.Add($"'{checksumsPath}' records {recorded} for '{artifactName}' but the file hashes to {actualDigest}");
        }
    }

    private static void VerifyFile(
        string path, string expectedPredicateType, string artifactName, string expectedDigest,
        string repository, string sourceRef, string commit, string signerWorkflow, List<string> failures)
    {
        if (!File.Exists(path))
        {
            failures.Add($"verification output '{path}' does not exist");
            return;
        }

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (JsonException exception)
        {
            failures.Add($"verification output '{path}' is not valid JSON: {exception.GetType().FullName}");
            return;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                failures.Add($"'{path}' is not the JSON array gh emits");
                return;
            }

            if (document.RootElement.GetArrayLength() != 1)
            {
                failures.Add($"'{path}' contains {document.RootElement.GetArrayLength()} verified results; exactly 1 is required");
                return;
            }

            var result = document.RootElement[0];

            if (result.ValueKind != JsonValueKind.Object)
            {
                failures.Add($"'{path}' verified result is not an object");
                return;
            }

            if (!result.TryGetProperty("verificationResult", out var verification) ||
                verification.ValueKind != JsonValueKind.Object)
            {
                failures.Add($"'{path}' has no verificationResult object");
                return;
            }

            VerifyStatement(path, verification, expectedPredicateType, artifactName, expectedDigest, failures);
            VerifyCertificate(path, verification, repository, sourceRef, commit, signerWorkflow, failures);
        }
    }

    private static void VerifyStatement(
        string path, JsonElement verification, string expectedPredicateType,
        string artifactName, string expectedDigest, List<string> failures)
    {
        if (!verification.TryGetProperty("statement", out var statement) || statement.ValueKind != JsonValueKind.Object)
        {
            failures.Add($"'{path}' has no statement");
            return;
        }

        string? predicateType = GetString(statement, "predicateType");

        if (!string.Equals(predicateType, expectedPredicateType, StringComparison.Ordinal))
        {
            failures.Add($"'{path}' predicateType is '{predicateType ?? "<absent>"}', expected '{expectedPredicateType}'");
        }

        if (!statement.TryGetProperty("subject", out var subjects) || subjects.ValueKind != JsonValueKind.Array)
        {
            failures.Add($"'{path}' statement has no subject array");
            return;
        }

        if (subjects.GetArrayLength() != 1)
        {
            failures.Add($"'{path}' statement has {subjects.GetArrayLength()} subjects; exactly 1 is required");
            return;
        }

        var subject = subjects[0];
        string? name = GetString(subject, "name");

        if (!string.Equals(name, artifactName, StringComparison.Ordinal))
        {
            failures.Add($"'{path}' subject name is '{name ?? "<absent>"}', expected '{artifactName}'");
        }

        string? digest = subject.TryGetProperty("digest", out var digests) && digests.ValueKind == JsonValueKind.Object
            ? GetString(digests, "sha256")?.ToLowerInvariant()
            : null;

        if (!string.Equals(digest, expectedDigest, StringComparison.Ordinal))
        {
            failures.Add($"'{path}' subject sha256 is '{digest ?? "<absent>"}', expected '{expectedDigest}'");
        }
    }

    private static void VerifyCertificate(
        string path, JsonElement verification, string repository, string sourceRef, string commit,
        string signerWorkflow, List<string> failures)
    {
        var certificate = verification.TryGetProperty("signature", out var signature) &&
                          signature.ValueKind == JsonValueKind.Object &&
                          signature.TryGetProperty("certificate", out var inner) &&
                          inner.ValueKind == JsonValueKind.Object
            ? inner
            : default;

        if (certificate.ValueKind != JsonValueKind.Object)
        {
            failures.Add($"'{path}' has no signature certificate; identity cannot be proven from predicate fields");
            return;
        }

        string expectedRepositoryUri = $"https://github.com/{repository}";

        Check(path, certificate, "sourceRepositoryURI", expectedRepositoryUri, failures);
        Check(path, certificate, "sourceRepositoryRef", sourceRef, failures);
        Check(path, certificate, "sourceRepositoryDigest", commit, failures);

        // The signer workflow lands in the certificate's build signer URI as
        // https://github.com/<owner>/<repo>/<workflow path>@<ref>.
        string expectedSigner = $"https://github.com/{signerWorkflow}@{sourceRef}";
        string? buildSigner = GetString(certificate, "buildSignerURI");

        if (!string.Equals(buildSigner, expectedSigner, StringComparison.Ordinal))
        {
            failures.Add($"'{path}' buildSignerURI is '{buildSigner ?? "<absent>"}', expected '{expectedSigner}'");
        }
    }

    private static void Check(string path, JsonElement certificate, string field, string expected, List<string> failures)
    {
        string? actual = GetString(certificate, field);

        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            failures.Add($"'{path}' certificate {field} is '{actual ?? "<absent>"}', expected '{expected}'");
        }
    }

    private static string? GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
