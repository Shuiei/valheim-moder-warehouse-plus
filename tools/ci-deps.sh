#!/usr/bin/env bash
# Fetch the mod DLLs the plugin and its tests build against into <dir>, from Thunderstore (the
# versions the manifest depends on): <dir>/core/ (BepInEx's core: BepInEx.dll, Mono.Cecil, MonoMod...),
# <dir>/plugins/ServersideQoL.dll and YamlDotNet.dll, and ServersideQoL's preloader patcher
# <dir>/patchers/ServersideQoL.Patchers.dll (the tests run it: tests/GamePatcher). The game's own DLLs
# come from the dedicated server (CircleCI's game-dlls job). Usage: tools/ci-deps.sh <dir>
# (needs curl and unzip)
set -euo pipefail
dir=$(realpath -m "${1:?output folder}")
repo=$(cd "$(dirname "$0")/.." && pwd)
mkdir -p "$dir/core" "$dir/plugins" "$dir/patchers"
work=$(mktemp -d); trap 'rm -rf "$work"' EXIT
dep() {   # $1 Team-Name from the manifest, $2 destination folder, then the paths in its zip
  local id=$(grep -o "\"$1-[0-9.]*\"" "$repo/thunderstore/manifest.json" | tr -d '"')
  [ -n "$id" ] || { echo "$1 is not a dependency in manifest.json" >&2; exit 1; }
  local version=${id##*-} name=${id%-*}
  local destination=$2
  shift 2
  [ -f "$work/$name.zip" ] || curl -fsSL -o "$work/$name.zip" "https://thunderstore.io/package/download/${name%%-*}/${name#*-}/$version/"
  unzip -joq "$work/$name.zip" "$@" -d "$destination"
  echo "$id: $*"
}
dep denikson-BepInExPack_Valheim "$dir/core" 'BepInExPack_Valheim/BepInEx/core/*'
dep ArgusMagnus-ServersideQoL "$dir/plugins" plugins/ServersideQoL.dll
dep ArgusMagnus-ServersideQoL "$dir/patchers" patchers/ServersideQoL.Patchers.dll
dep ValheimModding-YamlDotNet "$dir/plugins" plugins/YamlDotNet.dll
