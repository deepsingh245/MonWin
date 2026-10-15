#!/usr/bin/env bash
# Publishes the Linux app self-contained (the .NET runtime is bundled, so users need no .NET).
# Output: publish-linux/<rid>/
#
#   scripts/publish-linux.sh              # linux-x64 (the supported, tested target)
#   scripts/publish-linux.sh linux-arm64  # builds, but is NOT tested and NOT released
#
# Cross-publishing from Windows or macOS works too (no Linux machine required to build).
set -euo pipefail

# MonWin promises no telemetry, build time included. The projects already exclude Avalonia's
# build-time telemetry package; this is a second safeguard.
export AVALONIA_TELEMETRY_OPTOUT=1
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
rid="${1:-linux-x64}"

case "$rid" in
    linux-x64) ;;
    linux-arm64) echo "warning: $rid is built but not tested; do not publish it as a release asset." >&2 ;;
    *) echo "error: unsupported runtime '$rid' (use linux-x64 or linux-arm64)" >&2; exit 2 ;;
esac

out="$root/publish-linux/$rid"
rm -rf "$out"

echo "Publishing MonWin for $rid to $out ..."
dotnet publish "$root/src/MonWin.Linux/MonWin.Linux.csproj" \
    -c Release \
    -r "$rid" \
    --self-contained true \
    -p:DebugType=None \
    -p:DebugSymbols=false \
    -o "$out"

echo "Published to $out"
