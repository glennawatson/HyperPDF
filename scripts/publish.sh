#!/usr/bin/env bash
# Publishes the Native AOT build for a runtime identifier into artifacts/<rid>; debug symbols go to artifacts/symbols.
# Native AOT compiles for the operating system it runs on, so run this on Linux, Windows (Git Bash) or macOS as needed.
# Usage: scripts/publish.sh <linux-x64|linux-arm64|win-x64|win-arm64|osx-arm64|osx-x64> [extra dotnet publish arguments]
set -euo pipefail

RID="${1:?usage: scripts/publish.sh <rid> [dotnet publish arguments]}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$ROOT/artifacts/$RID"
SYMBOLS="$ROOT/artifacts/symbols/$RID"

rm -rf "$OUT"
dotnet publish "$ROOT/src/HyperPdf.App/HyperPdf.App.csproj" -c Release -f net11.0 -r "$RID" -o "$OUT" "${@:2}"

# Debug symbols are kept next to the artifacts, not shipped.
mkdir -p "$SYMBOLS"
find "$OUT" -maxdepth 1 \( -name '*.dbg' -o -name '*.pdb' \) -exec mv {} "$SYMBOLS/" \;
find "$OUT" -maxdepth 1 -name '*.dSYM' -exec mv {} "$SYMBOLS/" \;
echo "Published $RID to $OUT"
ls -la "$OUT"
