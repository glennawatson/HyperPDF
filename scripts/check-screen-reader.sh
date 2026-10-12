#!/usr/bin/env bash
# Starts the app on a private display, D-Bus session and accessibility bus, then checks a screen reader can see the
# window and a name for every control. Needs the .NET SDK, dbus, systemd and at-spi2-core.
# Uses a private Xvfb display, or a private headless Weston compositor with --wayland.
# Usage: scripts/check-screen-reader.sh [--wayland] <path to hyperpdf> [minimum controls]
set -euo pipefail

if [[ -z "${DBUS_SESSION_BUS_ADDRESS:-}" || "${CHECK_SCREEN_READER_INNER:-}" != 1 ]]; then
  exec env CHECK_SCREEN_READER_INNER=1 dbus-run-session -- "$0" "$@"
fi

backend=x11
if [[ "${1:-}" == --wayland ]]; then
  backend=wayland
  shift
fi
app=$(realpath "$1")
minimum=${2:-25}
here=$(cd "$(dirname "$0")" && pwd)
check="$here/AccessibilityCheck.cs"
work=$(mktemp -d)
pids=()
cleanup() {
  for pid in "${pids[@]}"; do kill "$pid" 2>/dev/null || true; done
  rm -rf "$work"
}
trap cleanup EXIT

# The display and runtime sockets must never belong to the user's desktop.
unset DISPLAY WAYLAND_DISPLAY WAYLAND_SOCKET
export XDG_RUNTIME_DIR="$work/runtime"
mkdir -m 700 "$XDG_RUNTIME_DIR"
if [[ "$backend" == wayland ]]; then
  export WAYLAND_DISPLAY=wayland-test LIBGL_ALWAYS_SOFTWARE=1
  weston --backend=headless --renderer=pixman --socket="$WAYLAND_DISPLAY" --idle-time=0 >"$work/display.log" 2>&1 &
  pids+=($!)
  for _ in $(seq 50); do
    [[ -S "$XDG_RUNTIME_DIR/$WAYLAND_DISPLAY" ]] && break
    sleep 0.1
  done
  [[ -S "$XDG_RUNTIME_DIR/$WAYLAND_DISPLAY" ]] || { cat "$work/display.log" >&2; exit 1; }
else
  Xvfb -displayfd 3 -screen 0 1400x900x24 3>"$work/display-number" >"$work/display.log" 2>&1 &
  pids+=($!)
  for _ in $(seq 50); do
    [[ -s "$work/display-number" ]] && break
    sleep 0.1
  done
  [[ -s "$work/display-number" ]] || { cat "$work/display.log" >&2; exit 1; }
  export DISPLAY=":$(cat "$work/display-number")"
fi

# Fresh settings and session, so the check sees the default window.
export XDG_CONFIG_HOME="$work/config" XDG_DATA_HOME="$work/data" XDG_STATE_HOME="$work/state"
mkdir -p "$XDG_CONFIG_HOME" "$XDG_DATA_HOME" "$XDG_STATE_HOME"

# Writing the check PDF also builds the walker, before the timed accessibility check starts.
dotnet run --file "$check" -- create-check-pdf "$work/check.pdf"

launcher=$(command -v at-spi-bus-launcher || echo /usr/libexec/at-spi-bus-launcher)
"$launcher" --launch-immediately &
pids+=($!)
for _ in $(seq 50); do
  gdbus call --session --dest org.a11y.Bus --object-path /org/a11y/bus --method org.a11y.Bus.GetAddress >/dev/null 2>&1 && break
  sleep 0.1
done
gdbus call --session --dest org.a11y.Bus --object-path /org/a11y/bus \
  --method org.freedesktop.DBus.Properties.Set org.a11y.Status IsEnabled "<true>" >/dev/null
address=$(gdbus call --session --dest org.a11y.Bus --object-path /org/a11y/bus --method org.a11y.Bus.GetAddress \
  | sed "s/^('\(.*\)',)$/\1/")

"$app" --new-instance "$work/check.pdf" >"$work/app.log" 2>&1 &
pids+=($!)

# Give the window time to appear, then let the walk decide.
for _ in $(seq 30); do
  sleep 1
  name=$(busctl --address="$address" list --no-pager 2>/dev/null | awk '$3 == "hyperpdf" { print $1; exit }')
  if [[ -n "$name" ]] && dotnet run --file "$check" -- atspi-walk "$address" "$name" "$minimum" >"$work/walk.txt" 2>&1; then
    cat "$work/walk.txt"
    exit 0
  fi
done

echo "A screen reader cannot see every control:" >&2
cat "$work/walk.txt" >&2 || true
cat "$work/app.log" >&2
exit 1
