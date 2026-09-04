#!/usr/bin/env bash
# Compare a workflow package with nuget.org. Ignore only the repository signature.

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
