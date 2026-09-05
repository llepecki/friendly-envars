#!/usr/bin/env bash
# Publish and run the trimmed linux-x64 smoke test. Requires Linux x64.

set -euo pipefail

REPO_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
PROJECT="$REPO_ROOT/tests/FriendlyEnvars.TrimSmoke/FriendlyEnvars.TrimSmoke.csproj"

if [[ $# -ne 0 ]]; then
    echo "usage: eng/trim-smoke.sh" >&2
    exit 2
fi

if [[ "$(uname -s)/$(uname -m)" != "Linux/x86_64" ]]; then
    echo "trim-smoke: this gate publishes and runs a self-contained linux-x64 binary; it requires Linux on x86_64, not $(uname -s)/$(uname -m)" >&2
    exit 1
fi

if [[ ! -f "$PROJECT" ]]; then
    echo "trim-smoke: project '$PROJECT' is missing" >&2
    exit 1
fi

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

# No --runtime flag: it would override the project's declared RuntimeIdentifiers and mismatch the
# committed lock file. The declared list already contains linux-x64; publish selects it below.
dotnet restore "$PROJECT" --locked-mode

dotnet publish "$PROJECT" \
    --configuration Release \
    --framework net8.0 \
    --runtime linux-x64 \
    --self-contained true \
    -p:PublishTrimmed=true \
    -p:TrimMode=link \
    -p:TreatWarningsAsErrors=true \
    --no-restore \
    --output "$WORK_DIR"

OUTPUT_FILE="$WORK_DIR/run-output.txt"

# Fixed, non-secret values. The executable itself asserts them, so a trimmed-away property or attribute
# shows up as a nonzero exit rather than a silent default.
STATUS=0
TRIM_SMOKE_NAME="trimmed" TRIM_SMOKE_COUNT="7" TRIM_SMOKE_ENDPOINT="trim-host:8080" TRIM_SMOKE_INHERITED="from-base" "$WORK_DIR/FriendlyEnvars.TrimSmoke" > "$OUTPUT_FILE" 2>&1 || STATUS=$?

if [[ $STATUS -ne 0 ]]; then
    echo "trim-smoke: the trimmed executable exited $STATUS" >&2
    cat "$OUTPUT_FILE" >&2
    exit 1
fi

if ! grep -qxF "Trim smoke completed successfully!" "$OUTPUT_FILE"; then
    echo "trim-smoke: the trimmed executable did not print the exact success line" >&2
    cat "$OUTPUT_FILE" >&2
    exit 1
fi

echo "trim-smoke: OK"
