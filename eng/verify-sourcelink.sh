#!/usr/bin/env bash
# Verify Source Link URLs and checksums. Run after pushing the clean release commit.
# Uses the repository verifier because the pinned Source Link CLI is unavailable on nuget.org.

set -euo pipefail

REPO_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
VERIFIER_PROJECT="$REPO_ROOT/eng/FriendlyEnvars.RepositoryVerifier/FriendlyEnvars.RepositoryVerifier.csproj"
VERIFIER_DLL="$REPO_ROOT/eng/FriendlyEnvars.RepositoryVerifier/bin/Release/net10.0/FriendlyEnvars.RepositoryVerifier.dll"

# Avoid `dotnet run`; path aliases can trigger an incremental clean before execution.
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
