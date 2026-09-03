#!/usr/bin/env bash
#
# Verifies that the packaged XML documentation documents the packaged assembly.
#
# Both artifacts are read out of the .nupkg, so this cannot pass against a stale build tree. The
# structured comparison - enumerating the assembly's externally visible members, deriving each one's
# documentation ID, and requiring an exact matching entry - is delegated to the repository verifier.
#
# Usage: eng/verify-docs.sh <nupkg>

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
