#!/usr/bin/env bash
# Installs a published build for the current user (default) or system wide.
# Usage: packaging/linux/install.sh [--prefix /usr/local] [--from artifacts/linux-x64]
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PREFIX="$HOME/.local"
FROM="$ROOT/artifacts/linux-x64"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --prefix) PREFIX="$2"; shift 2 ;;
    --from) FROM="$2"; shift 2 ;;
    *) echo "Unknown option $1" >&2; exit 1 ;;
  esac
done

APP_ID="net.glennwatson.HyperPDF"
LIB_DIR="$PREFIX/lib/hyperpdf"
install -d "$LIB_DIR" "$PREFIX/bin" "$PREFIX/share/applications" "$PREFIX/share/metainfo" "$PREFIX/share/icons/hicolor"
install -m 755 "$FROM/hyperpdf" "$LIB_DIR/hyperpdf"
for lib in "$FROM"/*.so; do install -m 644 "$lib" "$LIB_DIR/"; done
ln -sf "$LIB_DIR/hyperpdf" "$PREFIX/bin/hyperpdf"
sed "s|^Exec=hyperpdf|Exec=$PREFIX/bin/hyperpdf|; s|^TryExec=hyperpdf|TryExec=$PREFIX/bin/hyperpdf|" \
  "$ROOT/packaging/linux/$APP_ID.desktop" > "$PREFIX/share/applications/$APP_ID.desktop"
install -m 644 "$ROOT/packaging/linux/$APP_ID.metainfo.xml" "$PREFIX/share/metainfo/"
cp -R "$ROOT/packaging/linux/icons/hicolor/." "$PREFIX/share/icons/hicolor/"

command -v update-desktop-database >/dev/null && update-desktop-database "$PREFIX/share/applications" || true
command -v kbuildsycoca6 >/dev/null && kbuildsycoca6 --noincremental >/dev/null 2>&1 || true
echo "Installed to $PREFIX. To make it the default PDF viewer:"
echo "  xdg-mime default $APP_ID.desktop application/pdf"
