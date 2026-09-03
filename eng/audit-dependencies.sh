#!/usr/bin/env bash
#
# Audits every checked-in project for vulnerable packages, direct and transitive, across all of its
# target frameworks.
#
# Fails on NU1902/NU1903/NU1904 and on any reported Moderate, High or Critical advisory. Low advisories
# are reported but do not fail, matching the configured audit level.
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

# Only checked-in projects. Transient consumer projects created under the OS temporary directory by
# eng/smoke-consumer.sh are deliberately out of scope.
PROJECTS=()

while IFS= read -r project; do
    PROJECTS+=("$project")
done < <(cd "$REPO_ROOT" && git ls-files '*.csproj' | sort)

if [[ ${#PROJECTS[@]} -eq 0 ]]; then
    echo "audit-dependencies: found no checked-in projects to audit" >&2
    exit 1
fi

FAILED=0

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

    # The restore itself reports advisories as NU1902/NU1903/NU1904 when auditing is enabled.
    if grep -qE 'NU190[234]' "$output"; then
        echo "audit-dependencies: '$project' reported a NuGet audit diagnostic:" >&2
        grep -E 'NU190[234]' "$output" | sort -u >&2
        FAILED=1
        continue
    fi

    # dotnet list exits 0 even when it reports advisories, so inspecting its output is load-bearing
    # rather than belt-and-braces. It prints one row per vulnerable package with severity in a column.
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
