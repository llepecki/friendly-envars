#!/usr/bin/env bash
#
# Full-history secret scan with a pinned, checksum-verified Gitleaks binary.
#
# The binary is downloaded fresh into a temporary directory on every run and never installed. Every
# part of the supply chain is pinned and fails closed: the version, the initial URL, the one permitted
# redirect target, and a SHA-256 per platform, verified before anything is extracted or executed.
#
# Usage: eng/secret-scan.sh

set -euo pipefail

REPO_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"

if [[ $# -ne 0 ]]; then
    echo "usage: eng/secret-scan.sh" >&2
    exit 2
fi

readonly GITLEAKS_VERSION="8.30.1"

# GitHub release downloads answer with one redirect to the release-assets host. Both hops are pinned:
# the initial URL exactly, and the effective host exactly. A direct response, a second redirect, any
# other host, or any non-HTTPS hop is rejected.
readonly EXPECTED_REDIRECT_HOST="release-assets.githubusercontent.com"

# Per-platform archive checksums for v8.30.1. Any platform not listed here fails closed.
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

# --proto '=https' pins the first hop to HTTPS and --proto-redir '=https' pins every redirect hop, so
# no downgrade is possible. --max-redirs 1 makes curl itself fail on a second redirect. The write-out
# reports how many redirects were actually taken and where the download really came from, which is
# asserted below rather than assumed.
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

# The checksum is verified before anything is extracted or executed.
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

# Only the binary is extracted; the archive's other members are never written to disk.
tar -xzf "$ARCHIVE" -C "$WORK_DIR" gitleaks
GITLEAKS="$WORK_DIR/gitleaks"

if [[ ! -x "$GITLEAKS" ]]; then
    echo "secret-scan: archive did not contain an executable 'gitleaks' member" >&2
    exit 1
fi

# The binary must be the pinned version, not merely a correctly-hashed archive of something else.
REPORTED_VERSION="$("$GITLEAKS" version)"

if [[ "$REPORTED_VERSION" != "$GITLEAKS_VERSION" ]]; then
    echo "secret-scan: binary reports version '$REPORTED_VERSION', expected '$GITLEAKS_VERSION'" >&2
    exit 1
fi

# Full-history scan across all refs, redacted so a finding's own output cannot leak the secret it
# found. Gitleaks discovers the repository's .gitleaks.toml itself, which extends the default ruleset
# with the reviewed fixture allowlist. A dirty tree is not a prerequisite failure: the scan covers
# commits, and the CI checkout is always clean.
cd "$REPO_ROOT"

"$GITLEAKS" git --redact --no-banner --exit-code 1 --log-opts="--all" .

echo "secret-scan: OK (gitleaks $GITLEAKS_VERSION, $PLATFORM, full history)"
