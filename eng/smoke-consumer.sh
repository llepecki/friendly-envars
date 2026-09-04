#!/usr/bin/env bash
#
# Proves the README Quick Start is executable exactly as documented, from the candidate package, by a
# project that has never seen this repository.
#
# The programs and the package-add commands are extracted from README.md rather than written here, so
# the gate fails when the documentation drifts away from what works. Nothing is copied into the smoke
# projects that a reader could not copy from the Quick Start.
#
# Restore is isolated on purpose. The package sources come from a NuGet.config written into the
# temporary directory and passed explicitly, and packages land in a temporary global-packages folder, so
# neither a user-level source nor an already-cached FriendlyEnvars can make this pass.
#
# Usage: eng/smoke-consumer.sh <nupkg>

set -euo pipefail

REPO_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
README="$REPO_ROOT/README.md"

if [[ $# -ne 1 ]]; then
    echo "usage: eng/smoke-consumer.sh <nupkg>" >&2
    exit 2
fi

NUPKG="$1"

if [[ ! -f "$NUPKG" ]]; then
    echo "smoke-consumer: package '$NUPKG' does not exist" >&2
    exit 1
fi

if [[ ! -f "$README" ]]; then
    echo "smoke-consumer: '$README' does not exist" >&2
    exit 1
fi

NUPKG_BASENAME="$(basename -- "$NUPKG")"

if [[ ! "$NUPKG_BASENAME" =~ ^FriendlyEnvars\.([0-9]+\.[0-9]+\.[0-9]+[A-Za-z0-9.+-]*)\.nupkg$ ]]; then
    echo "smoke-consumer: '$NUPKG_BASENAME' is not a FriendlyEnvars.<version>.nupkg file name" >&2
    exit 1
fi

PACKAGE_VERSION="${BASH_REMATCH[1]}"

WORK_DIR="$(mktemp -d)"
WORK_DIR="$(cd -- "$WORK_DIR" && pwd -P)"
trap 'rm -rf "$WORK_DIR"' EXIT

PACKAGE_DIR="$WORK_DIR/packages"
GLOBAL_PACKAGES="$WORK_DIR/global-packages"
CONFIG_FILE="$WORK_DIR/NuGet.config"

mkdir -p "$PACKAGE_DIR" "$GLOBAL_PACKAGES"
cp -- "$NUPKG" "$PACKAGE_DIR/"

# The candidate package is reachable only by its exact id, and only from the local folder; everything
# the framework supplies is reachable only from nuget.org. A package that is neither is unresolvable,
# which is what keeps this a test of the documented dependency set.
cat > "$CONFIG_FILE" <<XML
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="candidate" value="$PACKAGE_DIR" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
  <disabledPackageSources>
    <clear />
  </disabledPackageSources>
  <packageSourceMapping>
    <packageSource key="candidate">
      <package pattern="FriendlyEnvars" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="Microsoft.*" />
      <package pattern="System.*" />
      <package pattern="runtime.*" />
      <package pattern="NETStandard.Library" />
    </packageSource>
  </packageSourceMapping>
</configuration>
XML

# Prints the fenced block that immediately follows a marker line. Fails loudly when the marker is
# missing or the block is empty, so a renamed section cannot silently reduce this gate to nothing.
extract_block() {
    local marker="$1"
    local output

    output="$(awk -v marker="$marker" '
        $0 == marker { armed = 1; next }
        armed && /^```/ { if (fence) { exit } ; fence = 1; next }
        fence { print }
    ' "$README")"

    if [[ -z "${output//[[:space:]]/}" ]]; then
        echo "smoke-consumer: README has no non-empty block after '$marker'" >&2
        return 1
    fi

    printf '%s\n' "$output"
}

FAILURES=0

fail() {
    echo "smoke-consumer: $*" >&2
    FAILURES=1
}

# Asserts the documented command block adds exactly the three documented packages at the documented
# versions, and none of the packages the Quick Start says Hosting already supplies.
check_documented_packages() {
    local tfm="$1" hosting_version="$2" annotations_version="$3" block="$4"
    local adds

    adds="$(grep -c '^dotnet add package ' "$block" || true)"

    if [[ "$adds" -ne 3 ]]; then
        fail "$tfm Quick Start declares $adds package additions; exactly 3 are documented"
    fi

    local expected
    for expected in \
        "dotnet add package FriendlyEnvars --version $PACKAGE_VERSION" \
        "dotnet add package Microsoft.Extensions.Hosting --version $hosting_version" \
        "dotnet add package Microsoft.Extensions.Options.DataAnnotations --version $annotations_version"
    do
        if ! grep -qxF "$expected" "$block"; then
            fail "$tfm Quick Start does not contain '$expected'"
        fi
    done

    # Hosting supplies these; adding them directly is what the Quick Start tells the reader not to do.
    local forbidden
    for forbidden in "Microsoft.Extensions.DependencyInjection" "Microsoft.Extensions.Options"; do
        if grep -qE "^dotnet add package $forbidden( |\$)" "$block"; then
            fail "$tfm Quick Start adds '$forbidden' directly"
        fi
    done
}

run_target() {
    local tfm="$1" hosting_version="$2" annotations_version="$3"
    local block="$WORK_DIR/$tfm-packages.sh"

    extract_block "<!-- smoke-consumer: packages $tfm -->" > "$block"
    check_documented_packages "$tfm" "$hosting_version" "$annotations_version" "$block"

    # Each scenario gets its own project, created by running the documented commands again, rather than
    # a copy of one project. A copied project carries an obj/project.assets.json full of absolute paths
    # belonging to the directory it was restored in, which is a difference this gate should not have to
    # reason about.
    run_scenario "$tfm" "$block" "valid"
    run_scenario "$tfm" "$block" "invalid"
}

run_scenario() {
    local tfm="$1" block="$2" scenario="$3"
    local root="$WORK_DIR/$tfm-$scenario"

    mkdir -p "$root"

    # The documented commands run unchanged apart from --no-restore, which is what forces the restore
    # below to be the only one, and therefore the only place package sources are decided.
    sed 's/^dotnet add package .*/& --no-restore/' "$block" > "$root/create.sh"

    if ! (cd "$root" && bash -euo pipefail create.sh) > "$WORK_DIR/$tfm-$scenario-create.log" 2>&1; then
        fail "$tfm/$scenario project creation failed"
        cat "$WORK_DIR/$tfm-$scenario-create.log" >&2
        return
    fi

    local project="$root/quickstart"

    if [[ ! -d "$project" ]]; then
        fail "$tfm/$scenario Quick Start did not create a 'quickstart' project directory"
        return
    fi

    # A ProjectReference would mean this is testing the working tree rather than the package.
    if grep -rq "ProjectReference" "$project" --include='*.csproj'; then
        fail "$tfm/$scenario smoke project references a repository source project"
        return
    fi

    if ! (cd "$project" && dotnet restore --configfile "$CONFIG_FILE" --packages "$GLOBAL_PACKAGES") \
        > "$WORK_DIR/$tfm-$scenario-restore.log" 2>&1; then
        fail "$tfm/$scenario isolated restore failed"
        cat "$WORK_DIR/$tfm-$scenario-restore.log" >&2
        return
    fi

    # Only documented code compiles: whatever the template generated is removed first.
    rm -f "$project"/*.cs
    extract_block "<!-- smoke-consumer: program $scenario -->" > "$project/Program.cs"

    if ! (cd "$project" && dotnet build --no-restore --configuration Release --nologo) \
        > "$WORK_DIR/$tfm-$scenario-build.log" 2>&1; then
        fail "$tfm/$scenario program did not build"
        cat "$WORK_DIR/$tfm-$scenario-build.log" >&2
        return
    fi

    local status=0
    local output="$WORK_DIR/$tfm-$scenario-run.log"

    if [[ "$scenario" == "valid" ]]; then
        # The documented block is a shell script of export statements, so it is sourced verbatim rather
        # than reinterpreted. It goes through a real file because bash 3.2, which is what macOS ships,
        # reads nothing at all from a sourced process substitution - the variables are never set, with
        # or without `set -a` - which silently produced an unset environment and a program that failed
        # validation for the wrong reason.
        #
        # The subshell keeps the documented environment out of the invalid scenario and out of anything
        # that runs after this script.
        local env_file="$WORK_DIR/$tfm-environment.sh"
        extract_block "<!-- smoke-consumer: environment valid -->" > "$env_file"

        (
            # shellcheck source=/dev/null
            . "$env_file"
            cd "$project" && dotnet run --no-build --no-restore --configuration Release
        ) > "$output" 2>&1 || status=$?

        if [[ $status -ne 0 ]]; then
            fail "$tfm valid program exited $status"
            cat "$output" >&2
            return
        fi

        # The documented output is asserted from the README rather than from literals kept here, so
        # the block a reader is shown cannot drift away from what the program actually prints.
        local expected_file="$WORK_DIR/$tfm-expected-output.txt"
        extract_block "<!-- smoke-consumer: output valid -->" > "$expected_file"

        local expected
        while IFS= read -r expected; do
            if [[ -z "${expected//[[:space:]]/}" ]]; then
                continue
            fi

            if ! grep -qxF "$expected" "$output"; then
                fail "$tfm valid program did not print the documented line '$expected'"
                cat "$output" >&2
                return
            fi
        done < "$expected_file"

        echo "smoke-consumer: $tfm valid Quick Start ran and printed the documented values"
    else
        # No environment is applied: the documented invalid program sets its own out-of-range value.
        (cd "$project" && dotnet run --no-build --no-restore --configuration Release) > "$output" 2>&1 || status=$?

        if [[ $status -eq 0 ]]; then
            fail "$tfm invalid program exited 0; it must fail during host start"
            cat "$output" >&2
            return
        fi

        if ! grep -q "OptionsValidationException" "$output"; then
            fail "$tfm invalid program failed without an OptionsValidationException"
            cat "$output" >&2
            return
        fi

        # The README presents this block as an excerpt of the output, so each documented line must be
        # present in the real output but is not required to be the whole of it.
        local excerpt_file="$WORK_DIR/$tfm-invalid-excerpt.txt"
        extract_block "<!-- smoke-consumer: output invalid -->" > "$excerpt_file"

        local documented
        while IFS= read -r documented; do
            if [[ -z "${documented//[[:space:]]/}" ]]; then
                continue
            fi

            if ! grep -qF "$documented" "$output"; then
                fail "$tfm invalid program output does not contain the documented line '$documented'"
                cat "$output" >&2
                return
            fi
        done < "$excerpt_file"

        echo "smoke-consumer: $tfm invalid Quick Start failed host start with OptionsValidationException"
    fi
}

run_target "net8.0" "8.0.1" "8.0.0"
run_target "net10.0" "10.0.11" "10.0.11"

if [[ $FAILURES -ne 0 ]]; then
    exit 1
fi

echo "smoke-consumer: OK"
