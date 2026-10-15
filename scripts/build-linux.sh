#!/usr/bin/env bash
# Restores, builds and tests the Linux app and the shared core (Release).
# Works on Linux, macOS and Windows (Git Bash); it never builds the Windows-only WPF app,
# so it needs only the .NET 8 SDK.
set -euo pipefail

# MonWin promises no telemetry, build time included. The projects already exclude Avalonia's
# build-time telemetry package; this is a second safeguard.
export AVALONIA_TELEMETRY_OPTOUT=1
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "== Build =="
dotnet build "$root/src/MonWin.Linux/MonWin.Linux.csproj" -c Release

echo "== Test =="
dotnet test "$root/tests/MonWin.Core.Tests/MonWin.Core.Tests.csproj" -c Release
dotnet test "$root/tests/MonWin.Linux.Tests/MonWin.Linux.Tests.csproj" -c Release
