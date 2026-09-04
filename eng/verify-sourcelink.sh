#!/usr/bin/env bash
#
# Verifies the Source Link contract of the symbol package: every compiled document maps to a URL that
# pins the exact commit being released, the mapped URLs really serve the compiled content, and the
# working tree the package was built from matches the PDB checksums.
#
# Run only from a clean checkout of the final committed release SHA, after that SHA has been pushed:
# the remote leg fetches raw.githubusercontent.com content for HEAD, which exists only once pushed.
#
# The specified Source Link CLI (`dotnet sourcelink test`) is not used: no Source Link tool exists at
# the pinned version on nuget.org, the only package source this repository permits. The repository
# verifier's `sourcelink` subcommand performs the same document-by-document checks (URL pinning plus
# checksum comparison of fetched content) with in-box metadata APIs. This deviation is recorded in the
# REV-L06 handoff.
#
# Usage: eng/verify-sourcelink.sh <snupkg>

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

if [[ $# -ne 1 ]]; then
    echo "usage: eng/verify-sourcelink.sh <snupkg>" >&2
    exit 2
fi

SNUPKG="$1"

if [[ ! -f "$SNUPKG" ]]; then
    echo "verify-sourcelink: symbol package '$SNUPKG' does not exist" >&2
    exit 1
fi

# The gate certifies one exact committed tree. A dirty tree would let the PDB describe sources that
# exist nowhere but this machine.
if [[ -n "$(cd "$REPO_ROOT" && git status --porcelain)" ]]; then
    echo "verify-sourcelink: the working tree is not clean; run this gate from a clean checkout" >&2
    exit 1
fi

COMMIT="$(cd "$REPO_ROOT" && git rev-parse HEAD)"

run_verifier sourcelink \
    --package "$SNUPKG" \
    --expect-repository-url "https://github.com/llepecki/friendly-envars" \
    --expect-commit "$COMMIT" \
    --verify-local-sources \
    --repo-root "$REPO_ROOT" \
    --fetch

echo "verify-sourcelink: OK"
