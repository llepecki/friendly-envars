using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.RepresentationModel;

namespace FriendlyEnvars.RepositoryVerifier.Commands;

/// <summary>
/// Validates .github/workflows/ci.yml against the locked supply-chain contract: the exact SHA-pinned
/// action allowlist with release comments, top-level and per-job permissions, job conditions,
/// dependency edges, and the verification-before-push ordering in the publish job.
/// </summary>
/// <remarks>
/// Structure is read through the YAML object model. The one exception is the release comment attached
/// to each `uses:` line: YAML comments do not exist in the object model, so those lines are checked
/// textually, scoped to lines that contain `uses:`.
/// </remarks>
internal static class WorkflowCommand
{
    /// <summary>The complete remote-action allowlist: exact pinned reference and release comment.</summary>
    private static readonly IReadOnlyDictionary<string, string> ActionAllowlist = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["actions/checkout@de0fac2e4500dabe0009e67214ff5f5447ce83dd"] = "v6.0.2",
        ["actions/setup-dotnet@c2fa09f4bde5ebb9d1777cf28262a3eb3db3ced7"] = "v5.2.0",
        ["actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a"] = "v7.0.1",
        ["actions/download-artifact@3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c"] = "v8.0.1",
        ["actions/attest@a1948c3f048ba23858d222213b7c278aabede763"] = "v4.1.1",
        ["dorny/test-reporter@7b7927aa7da8b82e81e755810cb51f39941a2cc7"] = "v2.2.0",
        ["NuGet/login@d22cc5f58ff5b88bf9bd452535b4335137e24544"] = "v1.1.0",
    };

    private static readonly string[] RequiredJobs = ["validate", "report-tests", "package", "publish"];

    private const string MasterPushCondition = "github.event_name == 'push' && github.ref == 'refs/heads/master'";

    public static void Run(CommandLine commandLine)
    {
        string workflowPath = commandLine.GetRequired("workflow");
        commandLine.EnsureAllConsumed();

        if (!File.Exists(workflowPath))
        {
            throw new VerificationException($"Workflow '{workflowPath}' does not exist.");
        }

        var failures = new List<string>();
        YamlMappingNode root;

        try
        {
            var stream = new YamlStream();
            using var reader = new StreamReader(workflowPath);
            stream.Load(reader);

            if (stream.Documents.Count == 0)
            {
                throw new VerificationException($"Workflow '{workflowPath}' contains no YAML document.");
            }

            root = (YamlMappingNode)stream.Documents[0].RootNode;
        }
        catch (Exception exception) when (exception is YamlDotNet.Core.YamlException or InvalidCastException)
        {
            throw new VerificationException($"Workflow '{workflowPath}' is not a YAML mapping: {exception.GetType().FullName}.");
        }

        VerifyTopLevel(root, failures);
        VerifyUsesLines(workflowPath, root, failures);

        var jobs = GetMapping(root, "jobs");

        if (jobs is null)
        {
            failures.Add("the workflow declares no jobs");
        }
        else
        {
            var jobNames = jobs.Children.Keys.Select(static key => key.ToString()).ToArray();

            foreach (string required in RequiredJobs)
            {
                if (!jobNames.Contains(required, StringComparer.Ordinal))
                {
                    failures.Add($"job '{required}' is missing");
                }
            }

            foreach (string extra in jobNames.Except(RequiredJobs, StringComparer.Ordinal))
            {
                failures.Add($"job '{extra}' is not part of the locked job set ({string.Join(", ", RequiredJobs)})");
            }

            VerifyJob(jobs, "validate", failures, VerifyValidate);
            VerifyJob(jobs, "report-tests", failures, VerifyReportTests);
            VerifyJob(jobs, "package", failures, VerifyPackage);
            VerifyJob(jobs, "publish", failures, VerifyPublish);
        }

        if (failures.Count > 0)
        {
            throw new VerificationException(
                $"Workflow validation failed for '{workflowPath}':{Environment.NewLine}  - " +
                string.Join($"{Environment.NewLine}  - ", failures));
        }

        Console.WriteLine($"workflow OK: '{workflowPath}' matches the locked job, permission and action contract.");
    }

    private static void VerifyTopLevel(YamlMappingNode root, List<string> failures)
    {
        // permissions: {} - present and empty, so every job grants its own minimum.
        if (!TryGet(root, "permissions", out var permissions))
        {
            failures.Add("top-level 'permissions: {}' is missing");
        }
        else if (permissions is not YamlMappingNode { Children.Count: 0 })
        {
            failures.Add("top-level permissions must be exactly the empty mapping '{}'");
        }

        var on = GetMapping(root, "on");

        if (on is null)
        {
            failures.Add("the workflow has no 'on' triggers");
        }
        else
        {
            var triggers = on.Children.Keys.Select(static key => key.ToString()).Order(StringComparer.Ordinal).ToArray();

            if (!triggers.SequenceEqual(["pull_request", "push"], StringComparer.Ordinal))
            {
                failures.Add($"triggers must be exactly push and pull_request; found: {string.Join(", ", triggers)}");
            }
        }
    }

    /// <summary>
    /// Every `uses:` reference must be on the allowlist and carry the exact release comment. The
    /// allowlist itself is enforced structurally through the YAML object model, which resolves quoted
    /// and unquoted keys alike; the textual pass exists only for the release comment, which the object
    /// model cannot see.
    /// </summary>
    private static void VerifyUsesLines(string workflowPath, YamlMappingNode root, List<string> failures)
    {
        var jobs = GetMapping(root, "jobs");

        if (jobs is not null)
        {
            foreach (var jobEntry in jobs.Children)
            {
                if (jobEntry.Value is not YamlMappingNode job)
                {
                    continue;
                }

                foreach (var step in Steps(job))
                {
                    string? uses = ScalarChild(step, "uses");

                    if (uses is not null && !ActionAllowlist.ContainsKey(uses))
                    {
                        failures.Add($"job '{jobEntry.Key}' uses '{uses}', which is not on the allowlist");
                    }
                }
            }
        }

        foreach (string rawLine in File.ReadAllLines(workflowPath))
        {
            string line = rawLine.Trim();

            if (line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            int usesIndex = line.IndexOf("uses:", StringComparison.Ordinal);

            if (usesIndex < 0)
            {
                continue;
            }

            string value = line[(usesIndex + "uses:".Length)..].Trim();
            int commentIndex = value.IndexOf('#', StringComparison.Ordinal);
            string reference = (commentIndex < 0 ? value : value[..commentIndex]).Trim();
            string comment = commentIndex < 0 ? string.Empty : value[(commentIndex + 1)..].Trim();

            reference = reference.StartsWith("- ", StringComparison.Ordinal) ? reference[2..].Trim() : reference;

            if (!ActionAllowlist.TryGetValue(reference, out string? expectedComment))
            {
                failures.Add($"action '{reference}' is not on the allowlist");
                continue;
            }

            if (!string.Equals(comment, expectedComment, StringComparison.Ordinal))
            {
                failures.Add($"action '{reference}' must carry the release comment '# {expectedComment}', found '#{(comment.Length == 0 ? " <none>" : " " + comment)}'");
            }
        }
    }

    private static void VerifyJob(YamlMappingNode jobs, string name, List<string> failures, Action<YamlMappingNode, List<string>> verify)
    {
        if (TryGet(jobs, name, out var node) && node is YamlMappingNode job)
        {
            verify(job, failures);
        }
    }

    private static void VerifyValidate(YamlMappingNode job, List<string> failures)
    {
        VerifyPermissions(job, "validate", failures, ("contents", "read"));

        if (TryGet(job, "environment", out _))
        {
            failures.Add("validate must not use a deployment environment");
        }

        VerifyCheckout(job, "validate", failures, requireFetchDepthZero: true);
        VerifyValidateMatrix(job, failures);

        var runs = RunCommands(job).ToArray();

        foreach (string required in new[]
        {
            "eng/secret-scan.sh",
            "eng/validate-workflows.sh",
            "dotnet restore FriendlyEnvars.slnx --locked-mode",
            "dotnet format FriendlyEnvars.slnx --verify-no-changes --no-restore",
            "--warnaserror",
            "--framework ${{ matrix.framework }}",
            "--filter \"Category=Portability\"",
            "eng/run-sample.sh",
            "eng/audit-dependencies.sh",
            "dotnet pack",
            "eng/verify-package.sh",
            "eng/verify-docs.sh",
            "eng/verify-api-removals.sh",
            "eng/smoke-consumer.sh",
            "eng/trim-smoke.sh",
        })
        {
            if (!runs.Any(run => run.Contains(required, StringComparison.Ordinal)))
            {
                failures.Add($"validate has no step running '{required}'");
            }
        }

        if (SecretReferences(job).Any())
        {
            failures.Add("validate must not reference any secret");
        }
    }

    /// <summary>
    /// The full test frameworks and the portability operating systems both live in validate's matrix,
    /// so the axes are the contract: net8.0 and net10.0 across ubuntu, windows and macos.
    /// </summary>
    private static void VerifyValidateMatrix(YamlMappingNode job, List<string> failures)
    {
        var strategy = GetMapping(job, "strategy");
        var matrix = strategy is null ? null : GetMapping(strategy, "matrix");

        if (matrix is null)
        {
            failures.Add("validate has no os/framework matrix");
            return;
        }

        VerifyAxis(matrix, "os", ["macos-latest", "ubuntu-latest", "windows-latest"], failures);
        VerifyAxis(matrix, "framework", ["net10.0", "net8.0"], failures);
    }

    private static void VerifyAxis(YamlMappingNode matrix, string axis, string[] expectedSorted, List<string> failures)
    {
        if (!TryGet(matrix, axis, out var node) || node is not YamlSequenceNode sequence)
        {
            failures.Add($"validate matrix has no '{axis}' axis");
            return;
        }

        var values = sequence.Children.Select(static child => child.ToString()).Order(StringComparer.Ordinal).ToArray();

        if (!values.SequenceEqual(expectedSorted, StringComparer.Ordinal))
        {
            failures.Add($"validate matrix '{axis}' must be exactly [{string.Join(", ", expectedSorted)}]; found [{string.Join(", ", values)}]");
        }
    }

    private static void VerifyReportTests(YamlMappingNode job, List<string> failures)
    {
        VerifyPermissions(job, "report-tests", failures, ("contents", "read"), ("checks", "write"));

        if (TryGet(job, "needs", out _))
        {
            failures.Add("report-tests must not depend on any job");
        }

        VerifyCheckout(job, "report-tests", failures, requireFetchDepthZero: false);

        foreach (var step in Steps(job))
        {
            string uses = ScalarChild(step, "uses") ?? string.Empty;

            if (uses.StartsWith("actions/upload-artifact", StringComparison.Ordinal) ||
                uses.StartsWith("actions/download-artifact", StringComparison.Ordinal))
            {
                failures.Add("report-tests must not upload or download artifacts");
            }
        }
    }

    private static void VerifyPackage(YamlMappingNode job, List<string> failures)
    {
        VerifyPermissions(job, "package", failures,
            ("contents", "read"), ("id-token", "write"), ("attestations", "write"), ("artifact-metadata", "write"));
        VerifyCondition(job, "package", failures);
        VerifyNeeds(job, "package", "validate", failures);
        VerifyCheckout(job, "package", failures, requireFetchDepthZero: false, requireShaRef: true);
        VerifyRemoteActionsRestrictedTo(job, "package", failures, "actions/", "NuGet/login@");

        var attestSteps = Steps(job)
            .Where(static step => (ScalarChild(step, "uses") ?? string.Empty).StartsWith("actions/attest@", StringComparison.Ordinal))
            .ToArray();

        if (attestSteps.Length != 4)
        {
            failures.Add($"package must invoke actions/attest exactly 4 times, found {attestSteps.Length}");
        }
        else
        {
            int provenanceCount = 0;
            int sbomCount = 0;

            foreach (var step in attestSteps)
            {
                var with = GetMapping(step, "with");
                string? subject = with is null ? null : ScalarChild(with, "subject-path");
                string? sbom = with is null ? null : ScalarChild(with, "sbom-path");

                if (subject is null || subject.Contains('\n', StringComparison.Ordinal) || subject.Contains('*', StringComparison.Ordinal))
                {
                    failures.Add("every attest step must name exactly one concrete subject-path");
                }
                else if (sbom is null)
                {
                    provenanceCount++;
                }
                else
                {
                    sbomCount++;
                }
            }

            if (provenanceCount != 2 || sbomCount != 2)
            {
                failures.Add($"package must produce 2 provenance and 2 SBOM attestations; found {provenanceCount} and {sbomCount}");
            }
        }
    }

    private static void VerifyPublish(YamlMappingNode job, List<string> failures)
    {
        VerifyPermissions(job, "publish", failures, ("contents", "read"), ("attestations", "read"), ("id-token", "write"));
        VerifyCondition(job, "publish", failures);
        VerifyNeeds(job, "publish", "package", failures);
        VerifyCheckout(job, "publish", failures, requireFetchDepthZero: false, requireShaRef: true);
        VerifyRemoteActionsRestrictedTo(job, "publish", failures, "actions/", "NuGet/login@");

        if (!string.Equals(ScalarChild(job, "environment"), "nuget-production", StringComparison.Ordinal))
        {
            failures.Add("publish must use the 'nuget-production' environment");
        }

        var steps = Steps(job).ToArray();
        int verifyIndex = -1;
        int loginIndex = -1;
        int pushIndex = -1;

        for (int i = 0; i < steps.Length; i++)
        {
            string run = ScalarChild(steps[i], "run") ?? string.Empty;
            string uses = ScalarChild(steps[i], "uses") ?? string.Empty;

            if (run.Contains("gh attestation verify", StringComparison.Ordinal) &&
                run.Contains("SHA256SUMS", StringComparison.Ordinal))
            {
                verifyIndex = i;
            }

            if (uses.StartsWith("NuGet/login@", StringComparison.Ordinal))
            {
                loginIndex = i;
            }

            if (run.Contains("dotnet nuget push", StringComparison.Ordinal))
            {
                pushIndex = i;

                if (!run.Contains("FriendlyEnvars.2.0.0-alpha.nupkg", StringComparison.Ordinal))
                {
                    failures.Add("publish must push exactly FriendlyEnvars.2.0.0-alpha.nupkg");
                }
            }
        }

        if (verifyIndex < 0)
        {
            failures.Add("publish has no step verifying SHA256SUMS and attestations");
        }

        if (loginIndex < 0)
        {
            failures.Add("publish has no NuGet/login step");
        }

        if (pushIndex < 0)
        {
            failures.Add("publish has no push step");
        }

        if (verifyIndex >= 0 && loginIndex >= 0 && pushIndex >= 0 && !(verifyIndex < loginIndex && loginIndex < pushIndex))
        {
            failures.Add("publish must verify checksums and attestations immediately before login, and login before push");
        }

        // The long-lived API key is retired; the only NUGET_API_KEY is the login step's output.
        foreach (string secret in SecretReferences(job))
        {
            if (secret.Contains("NUGET_API_KEY", StringComparison.Ordinal))
            {
                failures.Add("publish must not reference the retired NUGET_API_KEY secret");
            }
        }
    }

    private static void VerifyPermissions(YamlMappingNode job, string jobName, List<string> failures, params (string Scope, string Level)[] expected)
    {
        var permissions = GetMapping(job, "permissions");

        if (permissions is null)
        {
            failures.Add($"{jobName} declares no permissions");
            return;
        }

        var actual = permissions.Children
            .ToDictionary(static pair => pair.Key.ToString(), static pair => pair.Value.ToString(), StringComparer.Ordinal);

        foreach (var (scope, level) in expected)
        {
            if (!actual.TryGetValue(scope, out string? actualLevel))
            {
                failures.Add($"{jobName} is missing permission '{scope}: {level}'");
            }
            else if (!string.Equals(actualLevel, level, StringComparison.Ordinal))
            {
                failures.Add($"{jobName} permission '{scope}' is '{actualLevel}', expected '{level}'");
            }
        }

        foreach (string extra in actual.Keys.Except(expected.Select(static pair => pair.Scope), StringComparer.Ordinal))
        {
            failures.Add($"{jobName} grants an extra permission '{extra}'");
        }
    }

    private static void VerifyCondition(YamlMappingNode job, string jobName, List<string> failures)
    {
        string? condition = ScalarChild(job, "if");

        // Exact equality: an appended clause such as "|| github.event_name == 'pull_request'" would
        // make the job PR-reachable while still containing the contract condition.
        if (!string.Equals(condition, MasterPushCondition, StringComparison.Ordinal))
        {
            failures.Add($"{jobName} condition must be exactly '{MasterPushCondition}'; found '{condition ?? "<absent>"}'");
        }
    }

    private static void VerifyNeeds(YamlMappingNode job, string jobName, string expected, List<string> failures)
    {
        if (!TryGet(job, "needs", out var needs))
        {
            failures.Add($"{jobName} must depend on '{expected}'");
            return;
        }

        var dependencies = needs switch
        {
            YamlScalarNode scalar => [scalar.ToString()],
            YamlSequenceNode sequence => sequence.Children.Select(static child => child.ToString()).ToArray(),
            _ => Array.Empty<string>(),
        };

        if (dependencies.Length != 1 || !string.Equals(dependencies[0], expected, StringComparison.Ordinal))
        {
            failures.Add($"{jobName} must depend on exactly '{expected}'; found: {string.Join(", ", dependencies)}");
        }
    }

    private static void VerifyCheckout(YamlMappingNode job, string jobName, List<string> failures, bool requireFetchDepthZero, bool requireShaRef = false)
    {
        var checkouts = Steps(job)
            .Where(static step => (ScalarChild(step, "uses") ?? string.Empty).StartsWith("actions/checkout@", StringComparison.Ordinal))
            .ToArray();

        if (checkouts.Length == 0)
        {
            failures.Add($"{jobName} has no checkout step");
            return;
        }

        // Every checkout, not just the first: a later checkout with persistence enabled would write the
        // runner token into .git/config for all subsequent steps.
        foreach (var checkout in checkouts)
        {
            var checkoutWith = GetMapping(checkout, "with");

            if (checkoutWith is null || !string.Equals(ScalarChild(checkoutWith, "persist-credentials"), "false", StringComparison.Ordinal))
            {
                failures.Add($"{jobName}: every checkout must set persist-credentials: false");
            }
        }

        var with = GetMapping(checkouts[0], "with");

        if (requireFetchDepthZero && (with is null || !string.Equals(ScalarChild(with, "fetch-depth"), "0", StringComparison.Ordinal)))
        {
            failures.Add($"{jobName} checkout must set fetch-depth: 0");
        }

        if (requireShaRef && (with is null || !string.Equals(ScalarChild(with, "ref"), "${{ github.sha }}", StringComparison.Ordinal)))
        {
            failures.Add($"{jobName} checkout must pin ref: ${{{{ github.sha }}}}");
        }
    }

    private static void VerifyRemoteActionsRestrictedTo(YamlMappingNode job, string jobName, List<string> failures, params string[] allowedPrefixes)
    {
        foreach (var step in Steps(job))
        {
            string? uses = ScalarChild(step, "uses");

            if (uses is not null && !allowedPrefixes.Any(prefix => uses.StartsWith(prefix, StringComparison.Ordinal)))
            {
                failures.Add($"{jobName} uses '{uses}', which is outside its allowed action set");
            }
        }
    }

    private static IEnumerable<YamlMappingNode> Steps(YamlMappingNode job)
    {
        if (TryGet(job, "steps", out var steps) && steps is YamlSequenceNode sequence)
        {
            foreach (var child in sequence.Children)
            {
                if (child is YamlMappingNode step)
                {
                    yield return step;
                }
            }
        }
    }

    private static IEnumerable<string> RunCommands(YamlMappingNode job)
    {
        foreach (var step in Steps(job))
        {
            string? run = ScalarChild(step, "run");

            if (run is not null)
            {
                yield return run;
            }
        }
    }

    private static IEnumerable<string> SecretReferences(YamlMappingNode job)
    {
        var pending = new Stack<YamlNode>();
        pending.Push(job);

        while (pending.Count > 0)
        {
            switch (pending.Pop())
            {
                case YamlScalarNode scalar when scalar.Value?.Contains("secrets.", StringComparison.Ordinal) == true:
                    yield return scalar.Value;
                    break;

                case YamlMappingNode mapping:
                    foreach (var pair in mapping.Children)
                    {
                        pending.Push(pair.Value);
                    }

                    break;

                case YamlSequenceNode sequence:
                    foreach (var child in sequence.Children)
                    {
                        pending.Push(child);
                    }

                    break;

                default:
                    break;
            }
        }
    }

    private static bool TryGet(YamlMappingNode mapping, string key, out YamlNode node)
    {
        return mapping.Children.TryGetValue(new YamlScalarNode(key), out node!);
    }

    private static YamlMappingNode? GetMapping(YamlMappingNode mapping, string key)
    {
        return TryGet(mapping, key, out var node) ? node as YamlMappingNode : null;
    }

    private static string? ScalarChild(YamlMappingNode mapping, string key)
    {
        return TryGet(mapping, key, out var node) && node is YamlScalarNode scalar ? scalar.Value : null;
    }
}
