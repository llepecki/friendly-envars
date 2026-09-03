using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace FriendlyEnvars.RepositoryVerifier;

/// <summary>
/// Structured reader for a NuGet package. Entries and metadata are read through the ZIP and XML object
/// models rather than by matching text, so a gate can never pass on a coincidental substring.
/// </summary>
internal sealed class NuGetPackage : IDisposable
{
    private readonly ZipArchive _archive;

    private NuGetPackage(string path, ZipArchive archive, IReadOnlyList<string> entries, XDocument nuspec)
    {
        Path = path;
        _archive = archive;
        Entries = entries;
        Nuspec = nuspec;
    }

    public string Path { get; }

    /// <summary>All archive entry names, in archive order, normalised to forward slashes.</summary>
    public IReadOnlyList<string> Entries { get; }

    public XDocument Nuspec { get; }

    public static NuGetPackage Open(string path)
    {
        if (!File.Exists(path))
        {
            throw new VerificationException($"Package '{path}' does not exist.");
        }

        ZipArchive archive;

        try
        {
            archive = ZipFile.OpenRead(path);
        }
        catch (InvalidDataException ex)
        {
            throw new VerificationException($"Package '{path}' is not a readable ZIP archive: {ex.GetType().FullName}.");
        }

        try
        {
            var entries = archive.Entries.Select(static entry => entry.FullName.Replace('\\', '/')).ToArray();

            var nuspecEntries = entries
                .Where(static name => !name.Contains('/', StringComparison.Ordinal) && name.EndsWith(".nuspec", StringComparison.Ordinal))
                .ToArray();

            if (nuspecEntries.Length != 1)
            {
                throw new VerificationException(
                    $"Package '{path}' contains {nuspecEntries.Length} root .nuspec entries; exactly one is required.");
            }

            XDocument nuspec;

            using (var stream = archive.GetEntry(nuspecEntries[0])!.Open())
            {
                nuspec = XDocument.Load(stream, LoadOptions.None);
            }

            return new NuGetPackage(path, archive, entries, nuspec);
        }
        catch
        {
            archive.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Reads a single &lt;metadata&gt; child by local name, ignoring the nuspec schema namespace, which
    /// varies by SDK version.
    /// </summary>
    public string? GetMetadata(string localName)
    {
        var metadata = Nuspec.Root?.Elements().FirstOrDefault(static e => e.Name.LocalName == "metadata");

        if (metadata is null)
        {
            throw new VerificationException($"Package '{Path}' has a .nuspec with no <metadata> element.");
        }

        var matches = metadata.Elements().Where(e => e.Name.LocalName == localName).ToArray();

        if (matches.Length == 0)
        {
            return null;
        }

        if (matches.Length > 1)
        {
            throw new VerificationException($"Package '{Path}' declares <{localName}> {matches.Length} times in its .nuspec.");
        }

        return matches[0].Value;
    }

    public int CountEntries(string packagePath)
    {
        string normalised = NormalisePackagePath(packagePath);
        return Entries.Count(entry => string.Equals(entry, normalised, StringComparison.Ordinal));
    }

    /// <summary>
    /// Accepts the leading-slash form used by the specification (<c>/lib/net8.0/FriendlyEnvars.dll</c>)
    /// as well as the archive's own relative form.
    /// </summary>
    public static string NormalisePackagePath(string packagePath)
    {
        return packagePath.Replace('\\', '/').TrimStart('/');
    }

    public void Dispose()
    {
        _archive.Dispose();
    }
}
