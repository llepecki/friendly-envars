#!/usr/bin/env bash
# Compare the 1.1.0 baseline with this tree. The certified host is an idle Ubuntu x64 runner.

set -euo pipefail

REPO_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
VERIFIER_PROJECT="$REPO_ROOT/eng/FriendlyEnvars.RepositoryVerifier/FriendlyEnvars.RepositoryVerifier.csproj"
VERIFIER_DLL="$REPO_ROOT/eng/FriendlyEnvars.RepositoryVerifier/bin/Release/net10.0/FriendlyEnvars.RepositoryVerifier.dll"
BASELINE_PROJECT="$REPO_ROOT/benchmarks/FriendlyEnvars.Benchmarks/FriendlyEnvars.Benchmarks.Baseline/FriendlyEnvars.Benchmarks.Baseline.csproj"
CANDIDATE_PROJECT="$REPO_ROOT/benchmarks/FriendlyEnvars.Benchmarks/FriendlyEnvars.Benchmarks.Candidate/FriendlyEnvars.Benchmarks.Candidate.csproj"
BASELINE_DLL="$REPO_ROOT/benchmarks/FriendlyEnvars.Benchmarks/FriendlyEnvars.Benchmarks.Baseline/bin/Release/net8.0/FriendlyEnvars.Benchmarks.Baseline.dll"
CANDIDATE_DLL="$REPO_ROOT/benchmarks/FriendlyEnvars.Benchmarks/FriendlyEnvars.Benchmarks.Candidate/bin/Release/net8.0/FriendlyEnvars.Benchmarks.Candidate.dll"

# Avoid `dotnet run`; path aliases can trigger an incremental clean before execution.
run_verifier() {
    if [[ ! -f "$VERIFIER_DLL" ]]; then
        dotnet build "$VERIFIER_PROJECT" --configuration Release --verbosity quiet --nologo >&2
    fi

    dotnet "$VERIFIER_DLL" "$@"
}

if [[ $# -ne 0 ]]; then
    echo "usage: eng/compare-benchmarks.sh" >&2
    exit 2
fi

for project in "$BASELINE_PROJECT" "$CANDIDATE_PROJECT"; do
    if [[ ! -f "$project" ]]; then
        echo "compare-benchmarks: project '$project' is missing" >&2
        exit 1
    fi
done

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

echo "compare-benchmarks: host $(uname -s)/$(uname -m)"

for project in "$BASELINE_PROJECT" "$CANDIDATE_PROJECT"; do
    dotnet restore "$project" --locked-mode
    dotnet build "$project" --configuration Release --no-restore
done

# Sequential: concurrent measurement processes would contend for the same cores.
mkdir -p "$WORK_DIR/baseline" "$WORK_DIR/candidate"
dotnet "$BASELINE_DLL" "$WORK_DIR/baseline"
dotnet "$CANDIDATE_DLL" "$WORK_DIR/candidate"

find_report() {
    local reports
    reports="$(find "$1" -name '*-report-full.json' -type f)"

    if [[ "$(printf '%s\n' "$reports" | grep -c .)" -ne 1 ]]; then
        echo "compare-benchmarks: expected exactly one full JSON report under '$1', found:" >&2
        printf '%s\n' "$reports" >&2
        return 1
    fi

    printf '%s\n' "$reports"
}

BASELINE_REPORT="$(find_report "$WORK_DIR/baseline")"
CANDIDATE_REPORT="$(find_report "$WORK_DIR/candidate")"

run_verifier benchmark --baseline "$BASELINE_REPORT" --candidate "$CANDIDATE_REPORT"

echo "compare-benchmarks: OK"
