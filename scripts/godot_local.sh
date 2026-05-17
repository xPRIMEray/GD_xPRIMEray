#!/usr/bin/env bash
set -euo pipefail

# GPU runtime default: D3D12 Gallium backend for WSL hardware acceleration.
# Override by setting GALLIUM_DRIVER in the calling environment before running this script.
export GALLIUM_DRIVER="${GALLIUM_DRIVER:-d3d12}"

resolve_project_path() {
  local base_dir project_arg resolved
  base_dir="$(pwd -P)"
  project_arg="."

  local index=1
  while [ "$index" -le "$#" ]; do
    if [ "${!index}" = "--path" ]; then
      index=$((index + 1))
      if [ "$index" -le "$#" ]; then
        project_arg="${!index}"
      fi
      break
    fi
    index=$((index + 1))
  done

  if command -v realpath >/dev/null 2>&1; then
    resolved="$(realpath -m "$base_dir/$project_arg")"
  else
    resolved="$base_dir/$project_arg"
  fi

  printf '%s\n' "$resolved"
}

find_linux_godot() {
  local candidate

  if [ -n "${GODOT_LINUX_DOTNET_BIN:-}" ] && [ -x "${GODOT_LINUX_DOTNET_BIN}" ]; then
    printf '%s\n' "${GODOT_LINUX_DOTNET_BIN}"
    return 0
  fi

  for candidate in \
    godot4-mono \
    godot-mono \
    godot4-dotnet \
    godot-dotnet \
    godot4 \
    godot
  do
    if command -v "$candidate" >/dev/null 2>&1; then
      printf '%s\n' "$(command -v "$candidate")"
      return 0
    fi
  done

  for candidate in \
    "$HOME/.local/bin/godot4-mono" \
    "$HOME/.local/bin/godot-mono" \
    "$HOME/.local/bin/godot4-dotnet" \
    "$HOME/.local/bin/godot-dotnet" \
    "$HOME/Downloads/Godot_v4.5.1-stable_mono_linux_x86_64/Godot_v4.5.1-stable_mono_linux.x86_64" \
    "$HOME/Downloads/Godot_v4.5.1-stable_mono_linux_x86_64/Godot_v4.5.1-stable_mono_linux.x86_64.console" \
    "$HOME/Downloads/Godot_v4.5.1-stable_linux.x86_64" \
    "/opt/godot/Godot_v4.5.1-stable_mono_linux.x86_64" \
    "/opt/godot/Godot_v4.5.1-stable_mono_linux.x86_64.console" \
    "/usr/local/bin/godot4-mono" \
    "/usr/local/bin/godot-mono"
  do
    if [ -x "$candidate" ]; then
      printf '%s\n' "$candidate"
      return 0
    fi
  done

  return 1
}

PROJECT_PATH="$(resolve_project_path "$@")"
LINUX_GODOT_BIN=""
if LINUX_GODOT_BIN="$(find_linux_godot)"; then
  echo "[godot_local] using native Linux Godot build: $LINUX_GODOT_BIN" >&2
  exec "$LINUX_GODOT_BIN" "$@"
fi

WIN_GODOT_MONO_CONSOLE="/mnt/c/Users/wmbro/Downloads/Godot_v4.5.1-stable_mono_win64/Godot_v4.5.1-stable_mono_win64/Godot_v4.5.1-stable_mono_win64_console.exe"
WIN_GODOT_MONO_GUI="/mnt/c/Users/wmbro/Downloads/Godot_v4.5.1-stable_mono_win64/Godot_v4.5.1-stable_mono_win64/Godot_v4.5.1-stable_mono_win64.exe"

if [[ "$PROJECT_PATH" == /home/* ]]; then
  echo "[godot_local] Refusing Windows Godot fallback for WSL project path: $PROJECT_PATH" >&2
  echo "[godot_local] Missing runtime: no native Linux Godot .NET/mono binary was found." >&2
  echo "[godot_local] Searched PATH plus common locations under /usr/local, /opt, \$HOME/.local/bin, and \$HOME/Downloads." >&2
  echo "[godot_local] Install a Linux Godot .NET build or set GODOT_LINUX_DOTNET_BIN to its executable path." >&2
  exit 127
fi

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
