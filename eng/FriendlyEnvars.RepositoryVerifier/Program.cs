using FriendlyEnvars.RepositoryVerifier.Commands;
using System;
using System.Collections.Generic;

namespace FriendlyEnvars.RepositoryVerifier;

/// <summary>
/// Subcommand dispatcher for the repository gates. Every <c>eng/*.sh</c> wrapper that has to inspect a
/// binary, XML, JSON or YAML artifact delegates here so the assertions run against a parsed object model
/// instead of ad hoc text matching.
/// </summary>
internal static class Program
{
    /// <summary>
    /// The complete set of subcommands the specification requires this verifier to expose. Names are
    /// listed even when not yet implemented so that an unimplemented gate fails loudly rather than
    /// looking like a typo.
    /// </summary>
    private static readonly string[] KnownSubcommands =
    [
        "attestation",
        "benchmark",
        "package",
        "docs",
        "sbom",
        "workflow",
        "api-removals",
        "package-manifest",
        "published-package",
        "sourcelink",
        "reproducible-package"
    ];

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                throw new VerificationException($"No subcommand supplied. Expected one of: {string.Join(", ", KnownSubcommands)}.");
            }

            string subcommand = args[0];
            var rest = new List<string>(args.Length - 1);

            for (int i = 1; i < args.Length; i++)
            {
                rest.Add(args[i]);
            }

            var commandLine = CommandLine.Parse(rest);

            switch (subcommand)
            {
                case "package":
                    PackageCommand.Run(commandLine);
                    break;

                case "package-manifest":
                    PackageManifestCommand.Run(commandLine);
                    break;

                case "docs":
                    DocsCommand.Run(commandLine);
                    break;

                case "api-removals":
                    ApiRemovalsCommand.Run(commandLine);
                    break;

                case "sourcelink":
                    SourceLinkCommand.Run(commandLine);
                    break;

                case "reproducible-package":
                    PackageCompareCommand.Run(commandLine, exemptRepositorySignature: false);
                    break;

                case "published-package":
                    PackageCompareCommand.Run(commandLine, exemptRepositorySignature: true);
                    break;

                default:
                    throw new VerificationException(
                        Array.IndexOf(KnownSubcommands, subcommand) >= 0
                            ? $"Subcommand '{subcommand}' is declared but not implemented in this revision."
                            : $"Unknown subcommand '{subcommand}'. Expected one of: {string.Join(", ", KnownSubcommands)}.");
            }

            return 0;
        }
        catch (VerificationException ex)
        {
            Console.Error.WriteLine($"FAIL: {ex.Message}");
            return 1;
        }
    }
}
