#!/usr/bin/env bash
# Generate and validate the SPDX 2.2 SBOM for the packed artifacts, then place it as sbom.spdx.json.
# Only invokes the pinned SBOM tool from the local manifest; all inspection lives in eng/verify-sbom.sh.
#
# Usage: eng/generate-sbom.sh <artifacts-directory>

set -euo pipefail

REPO_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"

if [[ $# -ne 1 ]]; then
    echo "usage: eng/generate-sbom.sh <artifacts-directory>" >&2
    exit 2
fi

ARTIFACTS="$(cd -- "$1" && pwd -P)"

if [[ ! -d "$ARTIFACTS" ]]; then
    echo "generate-sbom: artifacts directory '$ARTIFACTS' does not exist" >&2
    exit 1
fi

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

cd "$REPO_ROOT"

dotnet tool run sbom-tool generate \
    -b "$ARTIFACTS" \
    -bc . \
    -pn FriendlyEnvars \
    -pv 2.0.0 \
    -ps "Lukasz Lepecki" \
    -nsb https://github.com/llepecki/friendly-envars/sbom \
    -m "$WORK_DIR"

dotnet tool run sbom-tool validate \
    -b "$ARTIFACTS" \
    -m "$WORK_DIR/_manifest" \
    -mi SPDX:2.2 \
    -o "$WORK_DIR/validation.json"

cp "$WORK_DIR/_manifest/spdx_2.2/manifest.spdx.json" "$ARTIFACTS/sbom.spdx.json"

echo "generate-sbom: OK ($ARTIFACTS/sbom.spdx.json)"
