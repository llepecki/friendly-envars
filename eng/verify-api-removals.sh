#!/usr/bin/env bash
# Verify that the options-blocking API removed in 2.0 is absent from source and binaries.

set -euo pipefail

REPO_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
VERIFIER_PROJECT="$REPO_ROOT/eng/FriendlyEnvars.RepositoryVerifier/FriendlyEnvars.RepositoryVerifier.csproj"
VERIFIER_DLL="$REPO_ROOT/eng/FriendlyEnvars.RepositoryVerifier/bin/Release/net10.0/FriendlyEnvars.RepositoryVerifier.dll"
LIBRARY_ASSEMBLY="src/FriendlyEnvars/bin/Release/net8.0/FriendlyEnvars.dll"

# Avoid `dotnet run`; path aliases can trigger an incremental clean before execution.
run_verifier() {
    if [[ ! -f "$VERIFIER_DLL" ]]; then
        dotnet build "$VERIFIER_PROJECT" --configuration Release --verbosity quiet --nologo >&2
    fi

    dotnet "$VERIFIER_DLL" "$@"
}

if [[ $# -ne 0 ]]; then
    echo "usage: eng/verify-api-removals.sh" >&2
    exit 2
fi

if [[ ! -f "$REPO_ROOT/$LIBRARY_ASSEMBLY" ]]; then
    echo "verify-api-removals: '$LIBRARY_ASSEMBLY' is missing; build the solution in Release first" >&2
    exit 1
fi

SEARCH_ARGS=(--root "$REPO_ROOT")

for identifier in BlockOptionsSnapshot BlockOptionsMonitor IsOptionsSnapshotAllowed IsOptionsMonitorAllowed; do
    SEARCH_ARGS+=(--identifier "$identifier")
done

for path in src tests sample benchmarks README.md; do
    if [[ -e "$REPO_ROOT/$path" ]]; then
        SEARCH_ARGS+=(--search-path "$path")
    fi
done

# The API-compatibility baseline legitimately names the removed members as intentional breaks.
if [[ -f "$REPO_ROOT/src/FriendlyEnvars/CompatibilitySuppressions.xml" ]]; then
    SEARCH_ARGS+=(--exclude "src/FriendlyEnvars/CompatibilitySuppressions.xml")
fi

run_verifier api-removals \
    "${SEARCH_ARGS[@]}" \
    --release-notes-project "src/FriendlyEnvars/FriendlyEnvars.csproj" \
    --assembly "$LIBRARY_ASSEMBLY"

echo "verify-api-removals: OK"
