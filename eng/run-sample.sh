#!/usr/bin/env bash
#
# Executes the sample as a contract rather than as documentation.
#
# For each target framework it asserts:
#   * default mode exits 0 and prints the exact success line
#   * --invalid-validation exits 2 and prints the exact fixed validation line
#   * neither stream discloses the sample's stand-in credentials, checked down to every
#     6-character window of them
#
# Usage: eng/run-sample.sh

set -euo pipefail

REPO_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
SAMPLE_PROJECT="$REPO_ROOT/sample/FriendlyEnvars.Sample/FriendlyEnvars.Sample.csproj"
SAMPLE_PROGRAM="$REPO_ROOT/sample/FriendlyEnvars.Sample/Program.cs"
FRAMEWORKS="net8.0 net10.0"

SUCCESS_LINE="Sample completed successfully!"
VALIDATION_LINE="Validation failed during StartAsync as expected."

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

if [[ $# -ne 0 ]]; then
    echo "usage: eng/run-sample.sh" >&2
    exit 2
fi

for required in "$SAMPLE_PROJECT" "$SAMPLE_PROGRAM"; do
    if [[ ! -f "$required" ]]; then
        echo "run-sample: '$required' is missing" >&2
        exit 1
    fi
done

# The sample only fills in variables that are unset. Clearing them first guarantees it uses its own
# stand-in credentials, so the secrecy assertion below cannot be made vacuous by an inherited value.
while IFS= read -r inherited_name; do
    unset "$inherited_name"
done < <(env | sed -n 's/^\(SAMPLE_[A-Za-z0-9_]*\)=.*/\1/p')

# The credentials are read out of the sample's own source rather than duplicated here, so the secrecy
# assertion can never quietly go stale by drifting away from what the sample actually uses.
read_constant() {
    sed -n "s/.*private const string $1 = \"\\([^\"]*\\)\";.*/\\1/p" "$SAMPLE_PROGRAM" | head -1
}

FAKE_PASSWORD="$(read_constant FakePassword)"
FAKE_API_KEY="$(read_constant FakeApiKey)"

if [[ ${#FAKE_PASSWORD} -lt 6 || ${#FAKE_API_KEY} -lt 6 ]]; then
    echo "run-sample: could not read both credential constants (at least 6 characters each) from $SAMPLE_PROGRAM" >&2
    exit 1
fi

# Every 6-character window of each credential. A longer disclosure necessarily contains one of these, so
# matching on 6-character windows also catches any longer fragment.
WINDOWS_FILE="$WORK_DIR/windows.txt"
: > "$WINDOWS_FILE"

emit_windows() {
    local secret="$1"
    local length=${#secret}
    local i

    for (( i = 0; i + 6 <= length; i++ )); do
        printf '%s\n' "${secret:i:6}"
    done
}

{
    emit_windows "$FAKE_PASSWORD"
    emit_windows "$FAKE_API_KEY"
} | sort -u > "$WINDOWS_FILE"

if [[ ! -s "$WINDOWS_FILE" ]]; then
    echo "run-sample: produced no credential windows to check" >&2
    exit 1
fi

assert_no_secret_disclosure() {
    local output_file="$1"
    local label="$2"
    local secret

    for secret in "$FAKE_PASSWORD" "$FAKE_API_KEY"; do
        if grep -qF -- "$secret" "$output_file"; then
            echo "run-sample: $label disclosed a full credential" >&2
            exit 1
        fi
    done

    if grep -qFf "$WINDOWS_FILE" "$output_file"; then
        echo "run-sample: $label disclosed a 6-character window of a credential:" >&2
        grep -oFf "$WINDOWS_FILE" "$output_file" | sort -u >&2
        exit 1
    fi
}

# Built unconditionally. The presence of a bin/Release directory says nothing about whether its contents
# were produced from the current source, and every run below uses --no-build, so skipping this would let
# the gate certify stale - or uncompilable - sample code. Incremental MSBuild makes the repeat cheap.
dotnet build "$SAMPLE_PROJECT" --configuration Release --verbosity quiet --nologo >&2

for framework in $FRAMEWORKS; do
    default_output="$WORK_DIR/$framework-default.txt"
    status=0
    dotnet run --project "$SAMPLE_PROJECT" --configuration Release --framework "$framework" --no-build \
        > "$default_output" 2>&1 || status=$?

    if [[ $status -ne 0 ]]; then
        echo "run-sample: default mode on $framework exited $status, expected 0" >&2
        cat "$default_output" >&2
        exit 1
    fi

    if ! grep -qFx -- "$SUCCESS_LINE" "$default_output"; then
        echo "run-sample: default mode on $framework did not print the exact success line" >&2
        cat "$default_output" >&2
        exit 1
    fi

    assert_no_secret_disclosure "$default_output" "default mode on $framework"

    invalid_output="$WORK_DIR/$framework-invalid.txt"
    status=0
    dotnet run --project "$SAMPLE_PROJECT" --configuration Release --framework "$framework" --no-build \
        -- --invalid-validation > "$invalid_output" 2>&1 || status=$?

    if [[ $status -ne 2 ]]; then
        echo "run-sample: --invalid-validation on $framework exited $status, expected 2" >&2
        cat "$invalid_output" >&2
        exit 1
    fi

    if ! grep -qFx -- "$VALIDATION_LINE" "$invalid_output"; then
        echo "run-sample: --invalid-validation on $framework did not print the exact validation line" >&2
        cat "$invalid_output" >&2
        exit 1
    fi

    # Reaching service resolution in this mode would mean invalid configuration was accepted. Requiring
    # the validation line to be the only output catches any resolution that SUCCEEDED, because the
    # reporter prints one line per bound setting. A resolution that itself throws
    # OptionsValidationException produces no output and is not externally distinguishable from the
    # compliant path; the source-level guarantee is that the reporter is only resolved after StartAsync
    # has returned, which happens solely on the default path.
    if grep -qFx -- "$SUCCESS_LINE" "$invalid_output"; then
        echo "run-sample: --invalid-validation on $framework reached the success path" >&2
        exit 1
    fi

    invalid_line_count="$(grep -c '' "$invalid_output" | tr -d '[:space:]')"

    if [[ "$invalid_line_count" != "1" ]]; then
        echo "run-sample: --invalid-validation on $framework produced $invalid_line_count lines, expected only the validation line" >&2
        cat "$invalid_output" >&2
        exit 1
    fi

    assert_no_secret_disclosure "$invalid_output" "--invalid-validation on $framework"

    echo "run-sample: $framework OK (exit 0 with the success line, exit 2 with the validation line)"
done

echo "run-sample: OK"
