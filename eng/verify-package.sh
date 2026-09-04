#!/usr/bin/env bash
#
# Verifies the produced NuGet package against the repository's release contract.
#
# All structured inspection is delegated to the repository verifier's `package` and `package-manifest`
# subcommands; this wrapper only resolves paths and states the repository's expectations.
#
# Usage: eng/verify-package.sh <nupkg> [<snupkg>]

set -euo pipefail

REPO_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
VERIFIER_PROJECT="$REPO_ROOT/eng/FriendlyEnvars.RepositoryVerifier/FriendlyEnvars.RepositoryVerifier.csproj"
VERIFIER_DLL="$REPO_ROOT/eng/FriendlyEnvars.RepositoryVerifier/bin/Release/net10.0/FriendlyEnvars.RepositoryVerifier.dll"

# Launches the verifier assembly directly through the muxer. `dotnet run` is deliberately avoided: it
# re-evaluates the project, and when the wrapper's resolved repository path differs from the one the
# solution build used (for example /private/var vs /var on macOS) MSBuild's incremental clean deletes
# the runtime configuration out from under the run.
run_verifier() {
    if [[ ! -f "$VERIFIER_DLL" ]]; then
        dotnet build "$VERIFIER_PROJECT" --configuration Release --verbosity quiet --nologo >&2
    fi

    dotnet "$VERIFIER_DLL" "$@"
}

if [[ $# -lt 1 || $# -gt 2 ]]; then
    echo "usage: eng/verify-package.sh <nupkg> [<snupkg>]" >&2
    exit 2
fi

NUPKG="$1"
SNUPKG="${2-}"

if [[ ! -f "$NUPKG" ]]; then
    echo "verify-package: package '$NUPKG' does not exist" >&2
    exit 1
fi

if [[ ! -f "$VERIFIER_PROJECT" ]]; then
    echo "verify-package: repository verifier project '$VERIFIER_PROJECT' is missing" >&2
    exit 1
fi

# The caller names the exact artifact it expects, so the package identity is derived from that file name
# and then asserted against the metadata actually packed inside the archive. A mismatch between the two
# is precisely the defect this catches.
NUPKG_BASENAME="$(basename -- "$NUPKG")"

if [[ ! "$NUPKG_BASENAME" =~ ^([A-Za-z0-9._-]+)\.([0-9]+\.[0-9]+\.[0-9]+[A-Za-z0-9.+-]*)\.nupkg$ ]]; then
    echo "verify-package: '$NUPKG_BASENAME' is not a <id>.<version>.nupkg file name" >&2
    exit 1
fi

PACKAGE_ID="${BASH_REMATCH[1]}"
PACKAGE_VERSION="${BASH_REMATCH[2]}"

if [[ -n "$SNUPKG" ]]; then
    if [[ ! -f "$SNUPKG" ]]; then
        echo "verify-package: symbol package '$SNUPKG' does not exist" >&2
        exit 1
    fi

    EXPECTED_SNUPKG="${PACKAGE_ID}.${PACKAGE_VERSION}.snupkg"

    if [[ "$(basename -- "$SNUPKG")" != "$EXPECTED_SNUPKG" ]]; then
        echo "verify-package: symbol package must be named '$EXPECTED_SNUPKG'" >&2
        exit 1
    fi
fi

run_verifier package \
    --package "$NUPKG" \
    --expect-id "$PACKAGE_ID" \
    --expect-version "$PACKAGE_VERSION" \
    --expect-icon "icon-v2.png" \
    --expect-readme "README.md" \
    --project "$REPO_ROOT/src/FriendlyEnvars/FriendlyEnvars.csproj" \
    --expect-validation-baseline "1.1.0" \
    --suppressions-file "$REPO_ROOT/src/FriendlyEnvars/CompatibilitySuppressions.xml" \
    --expect-dependency "Microsoft.Extensions.Options" \
    --expect-metadata "copyright=Copyright (c) 2026 Lukasz Lepecki" \
    --expect-repository-url "https://github.com/llepecki/friendly-envars" \
    --expect-property "Deterministic=true" \
    --expect-property "ContinuousIntegrationBuild=true" \
    --expect-property "PublishRepositoryUrl=true" \
    --expect-property "EmbedUntrackedSources=true" \
    --expect-property "IncludeSymbols=true" \
    --expect-property "SymbolPackageFormat=snupkg"

run_verifier package-manifest \
    --package "$NUPKG" \
    --require "/icon-v2.png" \
    --require "/README.md" \
    --require "/lib/net8.0/FriendlyEnvars.dll" \
    --require "/lib/net8.0/FriendlyEnvars.xml"

if [[ -n "$SNUPKG" ]]; then
    # The symbol package must actually carry the symbols its name promises.
    run_verifier package-manifest \
        --package "$SNUPKG" \
        --require "/lib/net8.0/FriendlyEnvars.pdb"
fi

echo "verify-package: OK"
