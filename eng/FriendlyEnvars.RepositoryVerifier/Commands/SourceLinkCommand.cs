using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text.Json;

namespace FriendlyEnvars.RepositoryVerifier.Commands;

/// <summary>
/// Verifies the Source Link contract of a symbol package by reading the portable PDB itself: every
/// compiled document must map through the embedded Source Link JSON to a URL that pins the exact
/// release commit, and the mapped content must hash to what the compiler recorded.
/// </summary>
/// <remarks>
/// This is a structured reimplementation of what `dotnet sourcelink test` checks. The specified
/// Source Link CLI does not exist at the pinned version on nuget.org, the only package source this
/// repository permits, so the verification is done here with in-box metadata APIs instead of an
/// unavailable tool. The deviation is deliberate and recorded in the finding's handoff.
/// </remarks>
internal static class SourceLinkCommand
{
    private static readonly Guid SourceLinkKind = new("CC110556-A091-4D38-9FEC-25AB9A351A6A");
    private static readonly Guid EmbeddedSourceKind = new("0E8A571B-6926-466E-B4AD-8AB04611F5FE");
    private static readonly Guid HashSha256 = new("8829d00f-11b8-4213-878b-770e8597ac16");
    private static readonly Guid HashSha1 = new("ff1816ec-aa5e-4d10-87f7-6f4963833460");

    public static void Run(CommandLine commandLine)
    {
        string packagePath = commandLine.GetRequired("package");
        string expectedRepositoryUrl = commandLine.GetRequired("expect-repository-url").TrimEnd('/');
        string expectedCommit = commandLine.GetRequired("expect-commit");
        string? repoRoot = commandLine.GetOptional("repo-root");
        bool verifyLocalSources = commandLine.HasSwitch("verify-local-sources");
        bool fetch = commandLine.HasSwitch("fetch");
        commandLine.EnsureAllConsumed();

        if (expectedCommit.Length != 40 || !expectedCommit.All(Uri.IsHexDigit))
        {
            throw new VerificationException($"--expect-commit '{expectedCommit}' is not a 40-character commit SHA.");
        }

        if (verifyLocalSources && repoRoot is null)
        {
            throw new VerificationException("--verify-local-sources requires --repo-root.");
        }

        using var package = NuGetPackage.Open(packagePath);

        var pdbEntries = package.Entries
            .Where(static entry => entry.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (pdbEntries.Length == 0)
        {
            throw new VerificationException($"Package '{packagePath}' contains no .pdb entries.");
        }

        var failures = new List<string>();
        int documentsChecked = 0;
        int embeddedCount = 0;

        foreach (string pdbEntry in pdbEntries)
        {
            VerifyPdb(package, pdbEntry, expectedRepositoryUrl, expectedCommit,
                verifyLocalSources ? repoRoot : null, fetch, failures,
                ref documentsChecked, ref embeddedCount);
        }

        if (failures.Count > 0)
        {
            throw new VerificationException(
                $"Source Link verification failed for '{packagePath}':{Environment.NewLine}  - " +
                string.Join($"{Environment.NewLine}  - ", failures));
        }

        Console.WriteLine(
            $"sourcelink OK: '{packagePath}' has {pdbEntries.Length} PDB(s); {documentsChecked} document(s) " +
            $"pin commit {expectedCommit} ({embeddedCount} embedded, " +
            (verifyLocalSources ? "local sources verified" : "local sources not checked") + ", " +
            (fetch ? "remote content verified" : "remote content not fetched") + ").");
    }

    private static void VerifyPdb(
        NuGetPackage package, string pdbEntry, string expectedRepositoryUrl, string expectedCommit,
        string? repoRoot, bool fetch, List<string> failures, ref int documentsChecked, ref int embeddedCount)
    {
        byte[] pdbBytes = package.ReadEntry(pdbEntry);

        using var provider = MetadataReaderProvider.FromPortablePdbStream(new MemoryStream(pdbBytes));
        var reader = provider.GetMetadataReader();

        string? sourceLinkJson = null;
        var embeddedDocuments = new HashSet<DocumentHandle>();

        foreach (var handle in reader.CustomDebugInformation)
        {
            var information = reader.GetCustomDebugInformation(handle);
            var kind = reader.GetGuid(information.Kind);

            if (kind == SourceLinkKind)
            {
                sourceLinkJson = System.Text.Encoding.UTF8.GetString(reader.GetBlobBytes(information.Value));
            }
            else if (kind == EmbeddedSourceKind && information.Parent.Kind == HandleKind.Document)
            {
                embeddedDocuments.Add((DocumentHandle)information.Parent);
            }
        }

        if (sourceLinkJson is null)
        {
            failures.Add($"'{pdbEntry}' carries no Source Link information");
            return;
        }

        IReadOnlyList<(string Pattern, string Url)> mappings;

        try
        {
            mappings = ParseSourceLink(sourceLinkJson);
        }
        catch (JsonException exception)
        {
            failures.Add($"'{pdbEntry}' Source Link JSON is malformed: {exception.GetType().FullName}");
            return;
        }

        // The release contract: content is addressed by the immutable commit, never by a ref.
        string requiredPrefix = $"https://raw.githubusercontent.com{new Uri(expectedRepositoryUrl).AbsolutePath}/{expectedCommit}/";

        foreach (var (pattern, url) in mappings)
        {
            if (!url.StartsWith(requiredPrefix, StringComparison.Ordinal))
            {
                failures.Add(
                    $"'{pdbEntry}' maps '{pattern}' to '{url}', which does not pin " +
                    $"'{requiredPrefix}'; a branch or tag there would move after release");
            }
        }

        using var httpClient = fetch ? new HttpClient() : null;

        foreach (var documentHandle in reader.Documents)
        {
            var document = reader.GetDocument(documentHandle);
            string name = reader.GetString(document.Name);
            string? url = MapDocument(name, mappings);

            if (url is null)
            {
                failures.Add($"'{pdbEntry}' document '{name}' is not covered by any Source Link mapping");
                continue;
            }

            documentsChecked++;

            if (embeddedDocuments.Contains(documentHandle))
            {
                // Untracked (generated) sources are embedded in the PDB itself; there is nothing
                // remote or in the worktree to compare them against.
                embeddedCount++;
                continue;
            }

            var algorithm = reader.GetGuid(document.HashAlgorithm);
            byte[] expectedHash = reader.GetBlobBytes(document.Hash);

            if (repoRoot is not null)
            {
                VerifyBytes(GetLocalBytes(repoRoot, name, failures, pdbEntry), algorithm, expectedHash,
                    $"local file for '{name}'", pdbEntry, failures);
            }

            if (httpClient is not null)
            {
                VerifyBytes(GetRemoteBytes(httpClient, url, failures, pdbEntry), algorithm, expectedHash,
                    $"remote content '{url}'", pdbEntry, failures);
            }
        }
    }

    private static IReadOnlyList<(string Pattern, string Url)> ParseSourceLink(string json)
    {
        using var parsed = JsonDocument.Parse(json);
        var result = new List<(string, string)>();

        foreach (var property in parsed.RootElement.GetProperty("documents").EnumerateObject())
        {
            result.Add((property.Name, property.Value.GetString()
                ?? throw new VerificationException("Source Link mapping value is not a string.")));
        }

        // Longest pattern first, matching Source Link's most-specific-wins resolution.
        result.Sort(static (left, right) => right.Item1.Length.CompareTo(left.Item1.Length));
        return result;
    }

    private static string? MapDocument(string documentName, IReadOnlyList<(string Pattern, string Url)> mappings)
    {
        string normalised = documentName.Replace('\\', '/');

        foreach (var (pattern, url) in mappings)
        {
            if (pattern.EndsWith("*", StringComparison.Ordinal))
            {
                string prefix = pattern[..^1];

                if (normalised.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return url.Replace("*", normalised[prefix.Length..], StringComparison.Ordinal);
                }
            }
            else if (string.Equals(normalised, pattern, StringComparison.Ordinal))
            {
                return url;
            }
        }

        return null;
    }

    private static byte[]? GetLocalBytes(string repoRoot, string documentName, List<string> failures, string pdbEntry)
    {
        string normalised = documentName.Replace('\\', '/');

        if (!normalised.StartsWith("/_/", StringComparison.Ordinal))
        {
            failures.Add($"'{pdbEntry}' document '{documentName}' is not rooted in the deterministic path '/_/'");
            return null;
        }

        string path = Path.Combine(repoRoot, normalised[3..]);

        if (!File.Exists(path))
        {
            failures.Add($"'{pdbEntry}' document '{documentName}' has no file at '{path}'");
            return null;
        }

        return File.ReadAllBytes(path);
    }

    private static byte[]? GetRemoteBytes(HttpClient httpClient, string url, List<string> failures, string pdbEntry)
    {
        try
        {
            return httpClient.GetByteArrayAsync(new Uri(url)).GetAwaiter().GetResult();
        }
        catch (HttpRequestException exception)
        {
            failures.Add($"'{pdbEntry}' could not fetch '{url}': {exception.StatusCode?.ToString() ?? exception.GetType().FullName}");
            return null;
        }
    }

    private static void VerifyBytes(
        byte[]? bytes, Guid algorithm, byte[] expectedHash, string description, string pdbEntry, List<string> failures)
    {
        if (bytes is null)
        {
            return;
        }

        byte[] actual;

        if (algorithm == HashSha256)
        {
            actual = SHA256.HashData(bytes);
        }
        else if (algorithm == HashSha1)
        {
            actual = SHA1.HashData(bytes);
        }
        else
        {
            failures.Add($"'{pdbEntry}' uses an unrecognised document hash algorithm '{algorithm}'");
            return;
        }

        if (!actual.AsSpan().SequenceEqual(expectedHash))
        {
            failures.Add($"'{pdbEntry}' checksum mismatch: {description} does not match the compiled document");
        }
    }
}
