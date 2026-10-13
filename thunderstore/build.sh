#!/usr/bin/env bash
# Build the mod's package into <dist>: ModerWarehousePlus-<version>.zip with manifest.json, icon.png,
# README.md, CHANGELOG.md, LICENSE and TRANSLATING.md at the root and the DLL in plugins/ (mod
# managers flatten any other folder).
# Also ModerWarehousePlus-<version>-nexus.zip for Nexus Mods and manual installs, unzipped into the
# game or server folder: BepInEx/plugins/ModerWarehousePlus/ with the DLL, LICENSE and TRANSLATING.md.
# The version is the plugin's (Version in ModerWarehouse/WarehousePlugin.cs). tools/publish.py sends the
# first zip to GitHub, Thunderstore and Hexium, the second to Nexus Mods (and GitHub).
# Usage: thunderstore/build.sh <dist> [msbuild properties...]
#   e.g. thunderstore/build.sh dist -p:ValheimDir=/opt/valheim -p:ServersideQoLDir=/tmp/sqol
# (needs the .NET SDK, zip, and the DLLs at the paths in the .csproj or the ones given).
set -euo pipefail
dist=$(realpath -m "${1:?output folder}"); shift
here=$(cd "$(dirname "$0")" && pwd); repo=$(dirname "$here")
dotnet=${DOTNET:-dotnet}
version=$(sed -n 's/.*const string Version = "\([0-9.]*\)".*/\1/p' "$repo/ModerWarehouse/WarehousePlugin.cs")
[[ $version =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "plugin version not found" >&2; exit 1; }
grep -q "^## $version\$" "$here/CHANGELOG.md" || { echo "CHANGELOG.md has no '## $version' section" >&2; exit 1; }
work=$(mktemp -d); trap 'rm -rf "$work"' EXIT
"$dotnet" build "$repo/ModerWarehousePlus.csproj" -c Release -warnaserror -p:DebugType=none -o "$work/build" "$@" >/dev/null \
  || { "$dotnet" build "$repo/ModerWarehousePlus.csproj" -c Release -warnaserror -p:DebugType=none -o "$work/build" "$@" | grep -E 'error|warning' >&2; exit 1; }
rm -rf "$repo/bin" "$repo/obj"
mkdir -p "$work/pkg/plugins" "$dist"
cp "$work/build/ModerWarehousePlus.dll" "$work/pkg/plugins/"
cp "$here/icon.png" "$here/README.md" "$here/CHANGELOG.md" "$repo/LICENSE" "$repo/docs/TRANSLATING.md" "$work/pkg/"
sed "s/@VERSION@/$version/g" "$here/manifest.json" > "$work/pkg/manifest.json"
out="$dist/ModerWarehousePlus-$version.zip"
rm -f "$out"
(cd "$work/pkg" && zip -qrX "$out" .)
echo "$out"
nexus="$work/nexus/BepInEx/plugins/ModerWarehousePlus"
mkdir -p "$nexus"
cp "$work/build/ModerWarehousePlus.dll" "$repo/LICENSE" "$repo/docs/TRANSLATING.md" "$nexus/"
out="$dist/ModerWarehousePlus-$version-nexus.zip"
rm -f "$out"
(cd "$work/nexus" && zip -qrX "$out" .)
echo "$out"
