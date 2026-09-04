#!/usr/bin/env bash
# The single local entry point for the complete repository gate. Runs every subordinate gate in a
# fixed order. Run from a clean checkout of the release candidate commit on the gate host
# (Ubuntu 24.04 x64 with the .NET 8 and .NET 10 SDKs installed).
#
# Usage: eng/verify-repository.sh

set -euo pipefail

REPO_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
cd "$REPO_ROOT"

if [[ $# -ne 0 ]]; then
    echo "usage: eng/verify-repository.sh" >&2
    exit 2
fi

# Every eng script must be committed executable before anything else runs.
NON_EXECUTABLE="$(git ls-files --stage 'eng/*.sh' | awk '$1 != "100755" {print $4}')"

if [[ -n "$NON_EXECUTABLE" ]]; then
    echo "verify-repository: these scripts are not committed with mode 100755:" >&2
    printf '  %s\n' $NON_EXECUTABLE >&2
    exit 1
fi

ACTUAL_SDK="$(dotnet --version)"

if [[ "$ACTUAL_SDK" != 10.0.* ]]; then
    echo "verify-repository: dotnet --version is '$ACTUAL_SDK', expected a 10.0 SDK" >&2
    exit 1
fi

dotnet tool restore --configfile NuGet.config
dotnet restore FriendlyEnvars.slnx --locked-mode
dotnet format FriendlyEnvars.slnx --verify-no-changes --no-restore
dotnet build FriendlyEnvars.slnx --configuration Release --no-restore --warnaserror
dotnet test tests/FriendlyEnvars.Tests/FriendlyEnvars.Tests.csproj --configuration Release --framework net8.0 --no-build --no-restore
dotnet test tests/FriendlyEnvars.Tests/FriendlyEnvars.Tests.csproj --configuration Release --framework net10.0 --no-build --no-restore
eng/run-sample.sh
dotnet pack src/FriendlyEnvars/FriendlyEnvars.csproj --configuration Release --no-build --no-restore --output artifacts/release
eng/verify-package.sh artifacts/release/FriendlyEnvars.2.0.0-alpha.nupkg artifacts/release/FriendlyEnvars.2.0.0-alpha.snupkg
eng/verify-docs.sh artifacts/release/FriendlyEnvars.2.0.0-alpha.nupkg
eng/generate-sbom.sh artifacts/release
eng/verify-sbom.sh artifacts/release/sbom.spdx.json
eng/smoke-consumer.sh artifacts/release/FriendlyEnvars.2.0.0-alpha.nupkg
eng/trim-smoke.sh
eng/audit-dependencies.sh
eng/validate-workflows.sh
eng/verify-api-removals.sh
eng/secret-scan.sh
eng/verify-sourcelink.sh artifacts/release/FriendlyEnvars.2.0.0-alpha.snupkg
eng/verify-reproducible-package.sh
git diff --exit-code

echo "verify-repository: OK"
