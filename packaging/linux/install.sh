#!/bin/sh
# MonWin per-user installer for Linux. No root, no sudo, no system-wide changes.
#
#   ./install.sh              install to ~/.local (binary, menu entry, icon)
#   ./install.sh --uninstall  remove what this script installed
#   ./install.sh --prefix DIR install under DIR instead of ~/.local (used for testing)
#
# Your settings (~/.config/monwin) and the optional autostart entry
# (~/.config/autostart/monwin.desktop) are never touched by install/uninstall; turn autostart
# on/off inside MonWin's Settings.
set -eu

PREFIX="${XDG_DATA_HOME:-$HOME/.local/share}"
BIN_DIR="$HOME/.local/bin"
ACTION=install

while [ $# -gt 0 ]; do
    case "$1" in
        --uninstall) ACTION=uninstall ;;
        --prefix)
            [ $# -ge 2 ] || { echo "error: --prefix needs a directory" >&2; exit 2; }
            PREFIX="$2/share"
            BIN_DIR="$2/bin"
            shift
            ;;
        -h|--help) sed -n '2,11p' "$0"; exit 0 ;;
        *) echo "error: unknown option: $1" >&2; exit 2 ;;
    esac
    shift
done

APP_DIR="$PREFIX/monwin"
DESKTOP_FILE="$PREFIX/applications/monwin.desktop"
ICON_FILE="$PREFIX/icons/hicolor/128x128/apps/monwin.png"
LINK="$BIN_DIR/monwin"

if [ "$ACTION" = uninstall ]; then
    rm -f "$LINK" "$DESKTOP_FILE" "$ICON_FILE"
    rm -rf "$APP_DIR"
    echo "MonWin removed. Your settings in ~/.config/monwin were left in place."
    exit 0
fi

SRC="$(cd "$(dirname "$0")" && pwd)"
[ -f "$SRC/monwin" ] || { echo "error: run this script from the extracted MonWin folder (monwin binary not found)" >&2; exit 1; }

# Replace any previous install in one step so an upgrade never mixes old and new files.
rm -rf "$APP_DIR"
mkdir -p "$APP_DIR" "$BIN_DIR" "$PREFIX/applications" "$(dirname "$ICON_FILE")"
cp -R "$SRC"/. "$APP_DIR"/
chmod +x "$APP_DIR/monwin"

ln -sf "$APP_DIR/monwin" "$LINK"
cp "$SRC/monwin.png" "$ICON_FILE"
sed "s|@EXEC@|$APP_DIR/monwin|" "$SRC/monwin.desktop" > "$DESKTOP_FILE"

echo "MonWin installed:"
echo "  program   $APP_DIR/monwin"
echo "  command   $LINK   (make sure $BIN_DIR is on your PATH)"
echo "  menu      $DESKTOP_FILE"
echo "Start it from your applications menu, or run: monwin"
