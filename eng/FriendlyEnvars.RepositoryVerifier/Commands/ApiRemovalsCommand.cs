using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace FriendlyEnvars.RepositoryVerifier.Commands;

internal static class ApiRemovalsCommand
{
    private static readonly string[] SearchedExtensions =
    [
        ".cs", ".csproj", ".props", ".targets", ".md", ".json", ".yml", ".yaml", ".sh", ".slnx"
    ];

    public static void Run(CommandLine commandLine)
    {
        string root = commandLine.GetRequired("root");
        var identifiers = commandLine.GetMany("identifier");
        var searchPaths = commandLine.GetMany("search-path");
        var excludes = commandLine.GetMany("exclude");
        var assemblies = commandLine.GetMany("assembly");
        var releaseNotesProjects = commandLine.GetMany("release-notes-project");
        commandLine.EnsureAllConsumed();

        if (identifiers.Count == 0)
        {
            throw new VerificationException("At least one --identifier must be supplied.");
        }

        if (searchPaths.Count == 0)
        {
            throw new VerificationException("At least one --search-path must be supplied.");
        }

        string rootFull = Path.GetFullPath(root);

        if (!Directory.Exists(rootFull))
        {
            throw new VerificationException($"Repository root '{root}' does not exist.");
        }

        var excludeFull = new HashSet<string>(
            excludes.Select(relative => Path.GetFullPath(Path.Combine(rootFull, relative))),
            StringComparer.Ordinal);

        var failures = new List<string>();
        int filesSearched = 0;

        foreach (string searchPath in searchPaths)
        {
            string fullSearchPath = Path.GetFullPath(Path.Combine(rootFull, searchPath));

            foreach (string file in EnumerateSearchableFiles(fullSearchPath, searchPath, excludeFull))
            {
                filesSearched++;
                SearchText(File.ReadAllText(file), Path.GetRelativePath(rootFull, file), identifiers, failures);
            }
        }

        foreach (string projectPath in releaseNotesProjects)
        {
            string fullProjectPath = Path.GetFullPath(Path.Combine(rootFull, projectPath));

            if (!File.Exists(fullProjectPath))
            {
                throw new VerificationException($"Release-notes project '{projectPath}' does not exist.");
            }

            var releaseNotes = XDocument.Load(fullProjectPath)
                .Descendants()
                .Where(static element => element.Name.LocalName == "PackageReleaseNotes")
                .ToArray();

            if (releaseNotes.Length == 0)
            {
                throw new VerificationException($"Release-notes project '{projectPath}' declares no <PackageReleaseNotes>.");
            }

            foreach (var element in releaseNotes)
            {
                SearchText(element.Value, $"{projectPath} <PackageReleaseNotes>", identifiers, failures);
            }
        }

        foreach (string assemblyPath in assemblies)
        {
            string fullAssemblyPath = Path.GetFullPath(Path.Combine(rootFull, assemblyPath));

            using var reader = PublicApiReader.OpenFile(fullAssemblyPath);

            foreach (var member in reader.EnumeratePublicMembers())
            {
                if (identifiers.Contains(member.Name, StringComparer.Ordinal))
                {
                    failures.Add($"{assemblyPath}: public surface still exposes {member}");
                }
            }
        }

        if (failures.Count > 0)
        {
            throw new VerificationException(
                $"Removed API references still present:{Environment.NewLine}  - " +
                string.Join($"{Environment.NewLine}  - ", failures));
        }

        if (filesSearched == 0)
        {
            throw new VerificationException("The scoped search matched no files at all; the search paths are wrong.");
        }

        Console.WriteLine(
            $"api-removals OK: none of {identifiers.Count.ToString(CultureInfo.InvariantCulture)} identifier(s) appear in " +
            $"{filesSearched.ToString(CultureInfo.InvariantCulture)} searched file(s), the release notes, or " +
            $"{assemblies.Count.ToString(CultureInfo.InvariantCulture)} assembly public surface(s).");
    }

    private static IEnumerable<string> EnumerateSearchableFiles(string fullSearchPath, string displayPath, HashSet<string> excludeFull)
    {
        if (File.Exists(fullSearchPath))
        {
            if (!excludeFull.Contains(fullSearchPath))
            {
                yield return fullSearchPath;
            }

            yield break;
        }

        if (!Directory.Exists(fullSearchPath))
        {
            throw new VerificationException($"Search path '{displayPath}' does not exist.");
        }

        foreach (string file in Directory.EnumerateFiles(fullSearchPath, "*", SearchOption.AllDirectories))
        {
            if (excludeFull.Contains(file))
            {
                continue;
            }

            // Build output is a copy of the sources being searched and is not checked in.
            string relative = Path.GetRelativePath(fullSearchPath, file);

            if (relative.Split(Path.DirectorySeparatorChar).Any(static segment =>
                    string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (SearchedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            {
                yield return file;
            }
        }
    }

    private static void SearchText(string content, string displayPath, IReadOnlyList<string> identifiers, List<string> failures)
    {
        string[] lines = content.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            foreach (string identifier in identifiers)
            {
                if (lines[i].Contains(identifier, StringComparison.Ordinal))
                {
                    failures.Add($"{displayPath}:{(i + 1).ToString(CultureInfo.InvariantCulture)}: '{identifier}'");
                }
            }
        }
    }
}
