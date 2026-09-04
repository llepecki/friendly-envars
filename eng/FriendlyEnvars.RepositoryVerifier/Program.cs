using FriendlyEnvars.RepositoryVerifier.Commands;
using System;
using System.Collections.Generic;

namespace FriendlyEnvars.RepositoryVerifier;

internal static class Program
{
    private static readonly string[] KnownSubcommands =
    [
        "package",
        "package-manifest"
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

                default:
                    throw new VerificationException(
                        $"Unknown subcommand '{subcommand}'. Expected one of: {string.Join(", ", KnownSubcommands)}.");
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
