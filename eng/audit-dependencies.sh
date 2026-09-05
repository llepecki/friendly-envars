#!/usr/bin/env bash
#
# Checks locked sources and scans every project for moderate-or-higher vulnerabilities.
#
# Usage: eng/audit-dependencies.sh

set -euo pipefail

REPO_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"

if [[ $# -ne 0 ]]; then
    echo "usage: eng/audit-dependencies.sh" >&2
    exit 2
fi

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

# Transient smoke-test projects are outside the repository and out of scope.
PROJECTS=()

while IFS= read -r project; do
    PROJECTS+=("$project")
done < <(cd "$REPO_ROOT" && git ls-files '*.csproj' | sort)

if [[ ${#PROJECTS[@]} -eq 0 ]]; then
    echo "audit-dependencies: found no checked-in projects to audit" >&2
    exit 1
fi

FAILED=0

# Ask NuGet for the effective sources so inherited configuration cannot stay hidden.
EXPECTED_SOURCE="https://api.nuget.org/v3/index.json"
SOURCES_FILE="$WORK_DIR/sources.txt"

if ! (cd "$REPO_ROOT" && dotnet nuget list source --format Short) > "$SOURCES_FILE" 2>&1; then
    echo "audit-dependencies: could not read the configured package sources" >&2
    cat "$SOURCES_FILE" >&2
    FAILED=1
else
    ENABLED=()

    # Legends combine enabled/disabled with optional machine-wide and official flags.
    while IFS= read -r line; do
        if [[ -z "${line//[[:space:]]/}" ]]; then
            continue
        fi

        if [[ ! "$line" =~ ^[[:space:]]*([A-Za-z]+)[[:space:]]+(.+[^[:space:]])[[:space:]]*$ ]]; then
            echo "audit-dependencies: could not parse a package source line: '$line'" >&2
            FAILED=1
            continue
        fi

        legend="${BASH_REMATCH[1]}"
        url="${BASH_REMATCH[2]}"

        if [[ ! "$legend" =~ ^[DE]M?O?$ ]]; then
            echo "audit-dependencies: unrecognised package source legend '$legend' on line: '$line'" >&2
            FAILED=1
            continue
        fi

        if [[ "$legend" == E* ]]; then
            ENABLED+=("$url")
        fi
    done < "$SOURCES_FILE"

    if [[ ${#ENABLED[@]} -ne 1 || "${ENABLED[0]}" != "$EXPECTED_SOURCE" ]]; then
        echo "audit-dependencies: restore must use '$EXPECTED_SOURCE' and nothing else; enabled sources are:" >&2
        printf '  %s\n' "${ENABLED[@]+"${ENABLED[@]}"}" >&2
        FAILED=1
    else
        echo "audit-dependencies: package sources OK (nuget.org only)"
    fi
fi

# Check the index so staged lock files count and untracked ones do not.
for project in "${PROJECTS[@]}"; do
    lock="$(dirname "$project")/packages.lock.json"

    if ! (cd "$REPO_ROOT" && git ls-files --error-unmatch "$lock" >/dev/null 2>&1); then
        echo "audit-dependencies: '$project' has no tracked '$lock'" >&2
        FAILED=1
    fi
done

# A locked restore must succeed without rewriting the graph.
if ! (cd "$REPO_ROOT" && dotnet restore FriendlyEnvars.slnx --locked-mode) > "$WORK_DIR/locked-restore.txt" 2>&1; then
    echo "audit-dependencies: locked restore failed" >&2
    cat "$WORK_DIR/locked-restore.txt" >&2
    FAILED=1
elif ! (cd "$REPO_ROOT" && git diff --quiet -- '*packages.lock.json'); then
    echo "audit-dependencies: the locked restore modified a lock file:" >&2
    (cd "$REPO_ROOT" && git diff --stat -- '*packages.lock.json') >&2
    FAILED=1
else
    echo "audit-dependencies: locked restore OK (solution, lock files unchanged)"
fi

for project in "${PROJECTS[@]}"; do
    output="$WORK_DIR/$(echo "$project" | tr '/' '_').txt"

    status=0
    (cd "$REPO_ROOT" && dotnet list "$project" package --vulnerable --include-transitive) > "$output" 2>&1 || status=$?

    if [[ $status -ne 0 ]]; then
        echo "audit-dependencies: '$project' could not be audited (exit $status)" >&2
        cat "$output" >&2
        FAILED=1
        continue
    fi

    # NU1900 means no vulnerability data was available.
    if grep -qE 'NU1900' "$output"; then
        echo "audit-dependencies: '$project' could not obtain vulnerability data, so nothing was audited:" >&2
        grep -E 'NU1900' "$output" | sort -u >&2
        FAILED=1
        continue
    fi

    # NU1902-NU1904 are moderate through critical; NU1901 is informational here.
    if grep -qE 'NU190[234]' "$output"; then
        echo "audit-dependencies: '$project' reported a NuGet audit diagnostic:" >&2
        grep -E 'NU190[234]' "$output" | sort -u >&2
        FAILED=1
        continue
    fi

    # `dotnet list` exits 0 when it finds advisories, so inspect its output.
    if grep -qiE '[[:space:]](Moderate|High|Critical)[[:space:]]' "$output"; then
        echo "audit-dependencies: '$project' has Moderate-or-higher advisories:" >&2
        grep -iE '[[:space:]](Moderate|High|Critical)[[:space:]]' "$output" >&2
        FAILED=1
        continue
    fi

    if grep -qiE '[[:space:]]Low[[:space:]]' "$output"; then
        echo "audit-dependencies: $project has Low-severity advisories (reported, not fatal):"
        grep -iE '[[:space:]]Low[[:space:]]' "$output"
    fi

    echo "audit-dependencies: $project OK"
done

if [[ $FAILED -ne 0 ]]; then
    exit 1
fi

echo "audit-dependencies: OK (${#PROJECTS[@]} project(s))"
