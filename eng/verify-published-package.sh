#!/usr/bin/env bash
#
# Compares the package this repository's workflow produced against the copy nuget.org actually serves,
# after publication. The complete extracted path and hash manifest must match except for
# .signature.p7s: nuget.org repository signing adds that entry and may repack the container, and is
# allowed to change nothing else. Any other missing, extra or different file fails.
#
# Usage: eng/verify-published-package.sh <workflow-nupkg> <nuget-org-nupkg>

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

if [[ $# -ne 2 ]]; then
    echo "usage: eng/verify-published-package.sh <workflow-nupkg> <nuget-org-nupkg>" >&2
    exit 2
fi

for package in "$1" "$2"; do
    if [[ ! -f "$package" ]]; then
        echo "verify-published-package: package '$package' does not exist" >&2
        exit 1
    fi
done

run_verifier published-package --left "$1" --right "$2"

echo "verify-published-package: OK"
