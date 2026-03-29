#!/usr/bin/env bash
set -euo pipefail

# Prefer native Linux Godot .NET build if ever installed later
if command -v godot4 >/dev/null 2>&1; then
  exec "$(command -v godot4)" "$@"
fi

if command -v godot >/dev/null 2>&1; then
  exec "$(command -v godot)" "$@"
fi

WIN_GODOT_MONO_CONSOLE="/mnt/c/Users/wmbro/Downloads/Godot_v4.5.1-stable_mono_win64/Godot_v4.5.1-stable_mono_win64/Godot_v4.5.1-stable_mono_win64_console.exe"
WIN_GODOT_MONO_GUI="/mnt/c/Users/wmbro/Downloads/Godot_v4.5.1-stable_mono_win64/Godot_v4.5.1-stable_mono_win64/Godot_v4.5.1-stable_mono_win64.exe"

if [ -x "$WIN_GODOT_MONO_CONSOLE" ]; then
  echo "[godot_local] using Windows mono console build: $WIN_GODOT_MONO_CONSOLE" >&2
  exec "$WIN_GODOT_MONO_CONSOLE" "$@"
fi

if [ -x "$WIN_GODOT_MONO_GUI" ]; then
  echo "[godot_local] using Windows mono GUI build: $WIN_GODOT_MONO_GUI" >&2
  exec "$WIN_GODOT_MONO_GUI" "$@"
fi

echo "[godot_local] No usable C#-capable Godot executable found." >&2
echo "[godot_local] This project requires the Godot .NET/mono build, not the standard non-.NET build." >&2
exit 127