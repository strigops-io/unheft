#!/usr/bin/env bash
# link-local — build, pack, and install unheft from the local source tree
# so you can run and test the tool as if it were installed from NuGet.
#
# Usage: scripts/link-local.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
OUT_DIR="$REPO_ROOT/artifacts/nupkg"

echo "Packing unheft from local source..."
dotnet pack "$REPO_ROOT/src/Unheft" -o "$OUT_DIR" --nologo

echo "Uninstalling existing global tool (if any)..."
dotnet tool uninstall -g unheft 2>/dev/null || true

echo "Installing unheft from local build..."
dotnet tool install -g unheft --add-source "$OUT_DIR"

echo ""
echo "Done. Run 'unheft --help' to verify."
