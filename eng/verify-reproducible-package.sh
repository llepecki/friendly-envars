#!/usr/bin/env bash
# Compare packages built from two clean copies of the same commit.

set -euo pipefail

REPO_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
VERIFIER_PROJECT="$REPO_ROOT/eng/FriendlyEnvars.RepositoryVerifier/FriendlyEnvars.RepositoryVerifier.csproj"
VERIFIER_DLL="$REPO_ROOT/eng/FriendlyEnvars.RepositoryVerifier/bin/Release/net10.0/FriendlyEnvars.RepositoryVerifier.dll"

# Avoid `dotnet run`; path aliases can trigger an incremental clean before execution.
run_verifier() {
    if [[ ! -f "$VERIFIER_DLL" ]]; then
        dotnet build "$VERIFIER_PROJECT" --configuration Release --verbosity quiet --nologo >&2
    fi

    dotnet "$VERIFIER_DLL" "$@"
}

if [[ $# -ne 0 ]]; then
    echo "usage: eng/verify-reproducible-package.sh" >&2
    exit 2
fi

if [[ -n "$(cd "$REPO_ROOT" && git status --porcelain)" ]]; then
    echo "verify-reproducible-package: the working tree is not clean; run this gate from a clean checkout" >&2
    exit 1
fi

COMMIT="$(cd "$REPO_ROOT" && git rev-parse HEAD)"

WORK_DIR="$(mktemp -d)"
WORK_DIR="$(cd -- "$WORK_DIR" && pwd -P)"
trap 'rm -rf "$WORK_DIR"' EXIT

# Each copy is a real clone detached at the exact commit, not a file copy: Source Link reads the git
# metadata at build time, and the embedded commit must be identical in both builds.
for copy in one two; do
    git clone --quiet "$REPO_ROOT" "$WORK_DIR/$copy"
    git -C "$WORK_DIR/$copy" -c advice.detachedHead=false checkout --quiet --detach "$COMMIT"

    (
        cd "$WORK_DIR/$copy"
        dotnet restore FriendlyEnvars.slnx --locked-mode
        dotnet build src/FriendlyEnvars/FriendlyEnvars.csproj --configuration Release --no-restore --warnaserror
        dotnet pack src/FriendlyEnvars/FriendlyEnvars.csproj --configuration Release --no-build --no-restore --output packages
    ) > "$WORK_DIR/$copy.log" 2>&1 || {
        echo "verify-reproducible-package: build in copy '$copy' failed" >&2
        cat "$WORK_DIR/$copy.log" >&2
        exit 1
    }
done

for extension in nupkg snupkg; do
    run_verifier reproducible-package \
        --left "$WORK_DIR/one/packages/FriendlyEnvars.2.0.0-alpha.$extension" \
        --right "$WORK_DIR/two/packages/FriendlyEnvars.2.0.0-alpha.$extension"
done

echo "verify-reproducible-package: OK (commit $COMMIT)"
