#!/usr/bin/env bash
# Validate .github/workflows/ci.yml against the locked supply-chain contract: the exact SHA-pinned
# action allowlist with release comments, top-level and per-job permissions, job conditions and
# dependency edges. Structured YAML inspection is delegated to the verifier's workflow subcommand.
#
# Usage: eng/validate-workflows.sh

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

if [[ $# -ne 0 ]]; then
    echo "usage: eng/validate-workflows.sh" >&2
    exit 2
fi

run_verifier workflow --workflow "$REPO_ROOT/.github/workflows/ci.yml"

echo "validate-workflows: OK"
