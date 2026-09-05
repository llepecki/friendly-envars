#!/usr/bin/env bash
#
# Runs a full-history scan with a temporary, checksum-verified Gitleaks binary.
#
# Usage: eng/secret-scan.sh

set -euo pipefail

REPO_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"

if [[ $# -ne 0 ]]; then
    echo "usage: eng/secret-scan.sh" >&2
    exit 2
fi

readonly GITLEAKS_VERSION="8.30.1"

# Allow one HTTPS redirect from GitHub to its release-assets host.
readonly EXPECTED_REDIRECT_HOST="release-assets.githubusercontent.com"

# Unlisted platforms fail closed.
case "$(uname -s)/$(uname -m)" in
    Linux/x86_64)
        PLATFORM="linux_x64"
        EXPECTED_SHA256="551f6fc83ea457d62a0d98237cbad105af8d557003051f41f3e7ca7b3f2470eb"
        ;;
    Darwin/x86_64)
        PLATFORM="darwin_x64"
        EXPECTED_SHA256="dfe101a4db2255fc85120ac7f3d25e4342c3c20cf749f2c20a18081af1952709"
        ;;
    Darwin/arm64)
        PLATFORM="darwin_arm64"
        EXPECTED_SHA256="b40ab0ae55c505963e365f271a8d3846efbc170aa17f2607f13df610a9aeb6a5"
        ;;
    *)
        echo "secret-scan: unsupported platform '$(uname -s)/$(uname -m)'; refusing to guess a binary" >&2
        exit 1
        ;;
esac

readonly ARCHIVE_NAME="gitleaks_${GITLEAKS_VERSION}_${PLATFORM}.tar.gz"
readonly URL="https://github.com/gitleaks/gitleaks/releases/download/v${GITLEAKS_VERSION}/${ARCHIVE_NAME}"

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

ARCHIVE="$WORK_DIR/$ARCHIVE_NAME"

# Capture the redirect count and effective URL for validation.
CURL_METADATA="$(curl --silent --show-error --fail \
    --location --max-redirs 1 \
    --proto '=https' --proto-redir '=https' \
    --output "$ARCHIVE" \
    --write-out '%{num_redirects} %{url_effective}' \
    "$URL")" || {
    echo "secret-scan: download failed for '$URL'" >&2
    exit 1
}

REDIRECTS="${CURL_METADATA%% *}"
EFFECTIVE_URL="${CURL_METADATA#* }"

if [[ "$REDIRECTS" != "1" ]]; then
    echo "secret-scan: expected exactly one redirect, took $REDIRECTS; refusing '$EFFECTIVE_URL'" >&2
    exit 1
fi

if [[ "$EFFECTIVE_URL" != https://* ]]; then
    echo "secret-scan: effective URL '$EFFECTIVE_URL' is not HTTPS" >&2
    exit 1
fi

EFFECTIVE_HOST="${EFFECTIVE_URL#https://}"
EFFECTIVE_HOST="${EFFECTIVE_HOST%%/*}"

if [[ "$EFFECTIVE_HOST" != "$EXPECTED_REDIRECT_HOST" ]]; then
    echo "secret-scan: download was served by '$EFFECTIVE_HOST', expected '$EXPECTED_REDIRECT_HOST'" >&2
    exit 1
fi

# Verify before extraction.
if command -v sha256sum >/dev/null 2>&1; then
    ACTUAL_SHA256="$(sha256sum "$ARCHIVE" | awk '{print $1}')"
else
    ACTUAL_SHA256="$(shasum -a 256 "$ARCHIVE" | awk '{print $1}')"
fi

if [[ "$ACTUAL_SHA256" != "$EXPECTED_SHA256" ]]; then
    echo "secret-scan: checksum mismatch for '$ARCHIVE_NAME'" >&2
    echo "  expected: $EXPECTED_SHA256" >&2
    echo "  actual:   $ACTUAL_SHA256" >&2
    exit 1
fi

# Extract only the binary.
tar -xzf "$ARCHIVE" -C "$WORK_DIR" gitleaks
GITLEAKS="$WORK_DIR/gitleaks"

if [[ ! -x "$GITLEAKS" ]]; then
    echo "secret-scan: archive did not contain an executable 'gitleaks' member" >&2
    exit 1
fi

REPORTED_VERSION="$("$GITLEAKS" version)"

if [[ "$REPORTED_VERSION" != "$GITLEAKS_VERSION" ]]; then
    echo "secret-scan: binary reports version '$REPORTED_VERSION', expected '$GITLEAKS_VERSION'" >&2
    exit 1
fi

# Scan every ref and redact findings.
cd "$REPO_ROOT"

"$GITLEAKS" git --redact --no-banner --exit-code 1 --log-opts="--all" .

echo "secret-scan: OK (gitleaks $GITLEAKS_VERSION, $PLATFORM, full history)"
