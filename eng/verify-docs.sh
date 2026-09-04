#!/usr/bin/env bash
#
# Checks that the package documents every public assembly member.
#
# Usage: eng/verify-docs.sh <nupkg>

set -euo pipefail

REPO_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
VERIFIER_PROJECT="$REPO_ROOT/eng/FriendlyEnvars.RepositoryVerifier/FriendlyEnvars.RepositoryVerifier.csproj"
VERIFIER_DLL="$REPO_ROOT/eng/FriendlyEnvars.RepositoryVerifier/bin/Release/net10.0/FriendlyEnvars.RepositoryVerifier.dll"

# Run the built DLL to avoid path-sensitive incremental cleanup from `dotnet run`.
run_verifier() {
    if [[ ! -f "$VERIFIER_DLL" ]]; then
        dotnet build "$VERIFIER_PROJECT" --configuration Release --verbosity quiet --nologo >&2
    fi

    dotnet "$VERIFIER_DLL" "$@"
}

if [[ $# -ne 1 ]]; then
    echo "usage: eng/verify-docs.sh <nupkg>" >&2
    exit 2
fi

NUPKG="$1"

if [[ ! -f "$NUPKG" ]]; then
    echo "verify-docs: package '$NUPKG' does not exist" >&2
    exit 1
fi

run_verifier docs \
    --package "$NUPKG" \
    --assembly-path "/lib/net8.0/FriendlyEnvars.dll" \
    --documentation-path "/lib/net8.0/FriendlyEnvars.xml"

echo "verify-docs: OK"
