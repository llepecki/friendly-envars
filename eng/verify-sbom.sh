#!/usr/bin/env bash
# Verify the generated SBOM: valid SPDX 2.2 JSON, package identity and version, both NuGet artifact
# file names, and a relationship from the FriendlyEnvars package to each declared dependency.
# Structured inspection is delegated to the verifier's sbom subcommand.
#
# Usage: eng/verify-sbom.sh <sbom.spdx.json>

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
    echo "usage: eng/verify-sbom.sh <sbom.spdx.json>" >&2
    exit 2
fi

run_verifier sbom \
    --sbom "$1" \
    --expect-name FriendlyEnvars \
    --expect-version 2.0.0-alpha \
    --expect-file FriendlyEnvars.2.0.0-alpha.nupkg \
    --expect-file FriendlyEnvars.2.0.0-alpha.snupkg \
    --expect-dependency "Microsoft.Extensions.Options=8.0.2"

echo "verify-sbom: OK"
