#!/usr/bin/env bash
#
# Audits every checked-in project for vulnerable packages, direct and transitive, across all of its
# target frameworks, and verifies that dependency restore is constrained the way REV-M05 requires.
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

# nuget.org is the only source any checked-in project may restore from. The source list is read back
# from NuGet itself rather than from the committed NuGet.config, because that is what makes <clear />
# observable: a source inherited from a user- or machine-level configuration appears here even though it
# appears nowhere in this repository.
EXPECTED_SOURCE="https://api.nuget.org/v3/index.json"
SOURCES_FILE="$WORK_DIR/sources.txt"

if ! (cd "$REPO_ROOT" && dotnet nuget list source --format Short) > "$SOURCES_FILE" 2>&1; then
    echo "audit-dependencies: could not read the configured package sources" >&2
    cat "$SOURCES_FILE" >&2
    FAILED=1
else
    ENABLED=()

    # Short format is one source per line: a legend field then the URL. The legend is "E" (enabled) or
    # "D" (disabled), optionally followed by "M" for a machine-wide source and "O" for an official one,
    # concatenated with no separator - so an enabled machine-wide source prints "EM <url>". Matching
    # only a bare "E" would make exactly the machine-wide sources this check exists to catch invisible.
    # An unrecognised legend or an unparseable line fails rather than being skipped.
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

# Every checked-in project must carry a tracked lock file. An untracked one would let a locked restore
# pass locally and resolve something different in CI. This tests the index rather than HEAD, which is
# what allows the check to run on a staged tree before the commit exists.
for project in "${PROJECTS[@]}"; do
    lock="$(dirname "$project")/packages.lock.json"

    if ! (cd "$REPO_ROOT" && git ls-files --error-unmatch "$lock" >/dev/null 2>&1); then
        echo "audit-dependencies: '$project' has no tracked '$lock'" >&2
        FAILED=1
    fi
done

# The locked restore is the mechanism the lock files exist for, so the gate exercises it rather than
# assuming it. It must also leave the lock files untouched; a restore that rewrites one is a restore
# that resolved something the committed graph does not describe.
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

    # NU1900 means the vulnerability data could not be retrieved. It is fatal here because the listing
    # below reports nothing when the data is missing, which is indistinguishable from a clean result;
    # without this check an offline or source-blocked run would certify the repository as audited when
    # nothing was audited.
    if grep -qE 'NU1900' "$output"; then
        echo "audit-dependencies: '$project' could not obtain vulnerability data, so nothing was audited:" >&2
        grep -E 'NU1900' "$output" | sort -u >&2
        FAILED=1
        continue
    fi

    # The restore itself reports advisories as NU1902/NU1903/NU1904 when auditing is enabled.
    # NU1901 is excluded: it is the low-severity advisory, which is reported without failing.
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
