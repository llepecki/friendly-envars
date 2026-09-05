#!/usr/bin/env bash
#
# Builds and runs the README Quick Start against the candidate package.
# Isolated sources and caches prevent repository or user state from hiding missing dependencies.
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

# Resolve FriendlyEnvars locally and framework packages only from nuget.org.
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

# Extract the non-empty fenced block after a marker.
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

# Require the documented dependency set and versions.
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

    # Hosting already supplies these packages.
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

    # Use a fresh project so build artifacts cannot leak between scenarios.
    run_scenario "$tfm" "$block" "valid"
    run_scenario "$tfm" "$block" "invalid"
}

run_scenario() {
    local tfm="$1" block="$2" scenario="$3"
    local root="$WORK_DIR/$tfm-$scenario"

    mkdir -p "$root"

    # Delay restore so the explicit NuGet configuration is authoritative.
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

    # Compile only the documented program.
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
        # A real file works on macOS Bash 3.2; the subshell contains the variables.
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

        # Compare against the README, not duplicated literals.
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

        # The README block is an excerpt, not the full stack trace.
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
