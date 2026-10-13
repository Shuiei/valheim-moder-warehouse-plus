#!/usr/bin/env bash
# Fetch the mod DLLs the plugin builds against into <dir>, from Thunderstore (the versions the
# manifest depends on): <dir>/core/BepInEx.dll, <dir>/plugins/ServersideQoL.dll and
# <dir>/plugins/YamlDotNet.dll. The game's own DLLs come from the dedicated server (CircleCI's
# game-dlls job). Usage: tools/ci-deps.sh <dir>   (needs curl and unzip)
set -euo pipefail
dir=$(realpath -m "${1:?output folder}")
repo=$(cd "$(dirname "$0")/.." && pwd)
mkdir -p "$dir/core" "$dir/plugins"
work=$(mktemp -d); trap 'rm -rf "$work"' EXIT
dep() {   # $1 Team-Name-Version from the manifest, $2 path in its zip, $3 destination folder
  local id=$(grep -o "\"$1-[0-9.]*\"" "$repo/thunderstore/manifest.json" | tr -d '"')
  [ -n "$id" ] || { echo "$1 is not a dependency in manifest.json" >&2; exit 1; }
  local version=${id##*-} name=${id%-*}
  curl -fsSL -o "$work/$name.zip" "https://thunderstore.io/package/download/${name%%-*}/${name#*-}/$version/"
  unzip -joq "$work/$name.zip" "$2" -d "$3"
  echo "$id: $(basename "$2")"
}
dep denikson-BepInExPack_Valheim BepInExPack_Valheim/BepInEx/core/BepInEx.dll "$dir/core"
dep ArgusMagnus-ServersideQoL plugins/ServersideQoL.dll "$dir/plugins"
dep ValheimModding-YamlDotNet plugins/YamlDotNet.dll "$dir/plugins"
