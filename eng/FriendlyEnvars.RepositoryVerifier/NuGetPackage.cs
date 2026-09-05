using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace FriendlyEnvars.RepositoryVerifier;

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

    public XElement Metadata =>
        Nuspec.Root?.Elements().FirstOrDefault(static e => e.Name.LocalName == "metadata")
        ?? throw new VerificationException($"Package '{Path}' has a .nuspec with no <metadata> element.");

    public string? GetMetadata(string localName)
    {
        var matches = Metadata.Elements().Where(e => e.Name.LocalName == localName).ToArray();

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

    public byte[] ReadEntry(string packagePath)
    {
        string normalised = NormalisePackagePath(packagePath);
        int count = CountEntries(normalised);

        if (count != 1)
        {
            throw new VerificationException(
                $"Package '{Path}' contains '{normalised}' {count} time(s); exactly 1 is required.");
        }

        using var stream = _archive.GetEntry(normalised)!.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        return buffer.ToArray();
    }

    public int CountEntries(string packagePath)
    {
        string normalised = NormalisePackagePath(packagePath);
        return Entries.Count(entry => string.Equals(entry, normalised, StringComparison.Ordinal));
    }

    public static string NormalisePackagePath(string packagePath)
    {
        return packagePath.Replace('\\', '/').TrimStart('/');
    }

    public void Dispose()
    {
        _archive.Dispose();
    }
}
