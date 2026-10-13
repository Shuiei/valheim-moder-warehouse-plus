#!/usr/bin/env bash
# The package in a real Valheim dedicated server: what the unit tests cannot reach, since it needs the
# game. Installs into <server> (a dedicated server, Steam app 896660) BepInExPack, ServersideQoL,
# ServersideQoL ContainerSigns and YamlDotNet (the versions manifest.json depends on, from
# Thunderstore), the package's plugin as a mod manager would, and a server switching over from the
# original Moder Warehouse (its settings and stock cache). Then two runs of the server, read from
# BepInEx's log:
#   1. Plus loads beside ServersideQoL and ContainerSigns, copies the original's settings and stock
#      cache to its own files, and starts its warehouse processing, with no error of its own
#   2. with the original Moder Warehouse's DLL installed too, BepInEx refuses to load Plus
# Usage: tools/server-smoke.sh <server> <package.zip>   (needs curl and unzip; changes <server>)
# The logs of both runs are left in <server>/smoke-logs/ (CI keeps them as artifacts).
set -euo pipefail
server=$(realpath "${1:?dedicated server folder}")
zip=$(realpath "${2:?package zip}")
repo=$(cd "$(dirname "$0")/.." && pwd)
[ -x "$server/valheim_server.x86_64" ] || { echo "$server is not a Valheim dedicated server" >&2; exit 1; }
work=$(mktemp -d); trap 'rm -rf "$work"' EXIT
logs="$server/smoke-logs"
rm -rf "$logs"; mkdir -p "$logs"
version=$(unzip -p "$zip" manifest.json | sed -n 's/.*"version_number": *"\([^"]*\)".*/\1/p')
name="Moder Warehouse Plus $version"

# ---- Install.
download() {   # $1 Team-Name, $2 version -> $work/<Team-Name>.zip
  curl -fsSL -o "$work/$1.zip" "https://thunderstore.io/package/download/${1%%-*}/${1#*-}/$2/"
}
dependency() {   # the version manifest.json depends on, for Team-Name $1
  grep -o "\"$1-[0-9.]*\"" "$repo/thunderstore/manifest.json" | tr -d '"' | sed 's/.*-//'
}
sqol=$(dependency ArgusMagnus-ServersideQoL)
download denikson-BepInExPack_Valheim "$(dependency denikson-BepInExPack_Valheim)"
unzip -qo "$work/denikson-BepInExPack_Valheim.zip" 'BepInExPack_Valheim/*' -d "$work/pack"
cp -a "$work/pack/BepInExPack_Valheim/." "$server/"
plugins="$server/BepInEx/plugins" config="$server/BepInEx/config"
rm -rf "$plugins" "$config/ModerWarehousePlus" "$config"/*.cfg.bak
mkdir -p "$plugins" "$config"
download ArgusMagnus-ServersideQoL "$sqol"
# ContainerSigns is released with ServersideQoL, under the same version.
download ArgusMagnus-ServersideQoL_ContainerSigns "$sqol"
download ValheimModding-YamlDotNet "$(dependency ValheimModding-YamlDotNet)"
# As mod managers install a package: plugins/ and patchers/ to BepInEx's, DLLs at its root to plugins/.
rm -rf "$server/BepInEx/patchers"; mkdir -p "$server/BepInEx/patchers"
install_package() {   # $1 zip
  local files
  files=$(unzip -Z1 "$1")
  if grep -q '^plugins/.' <<< "$files"; then unzip -qjo "$1" 'plugins/*' -d "$plugins"; fi
  if grep -q '^patchers/.' <<< "$files"; then unzip -qjo "$1" 'patchers/*' -d "$server/BepInEx/patchers"; fi
  local root
  root=$(grep -E '^[^/]+\.dll$' <<< "$files" || true)
  if [ -n "$root" ]; then unzip -qjo "$1" $root -d "$plugins"; fi
}
for package in ArgusMagnus-ServersideQoL ArgusMagnus-ServersideQoL_ContainerSigns ValheimModding-YamlDotNet; do
  install_package "$work/$package.zip"
done
mkdir -p "$plugins/Tie-ModerWarehousePlus"
unzip -qjo "$zip" 'plugins/*' -d "$plugins/Tie-ModerWarehousePlus"

# A server that ran the original mod: its settings, and its stock cache in its plugin folder.
rm -f "$config/Tie.ModerWarehousePlus.cfg"
printf '[Warehouse]\n\n## Storage radius around the IN chest.\nRadius = 22\n' > "$config/ch.cris.valheim.moderwarehouse.cfg"
mkdir -p "$plugins/ModerWarehouse"
printf '%s\t%s\t%s\n' "$(printf smoke | base64)" "$(printf '$item_wood' | base64)" 42 > "$plugins/ModerWarehouse/ModerWarehouse-stock-cache.tsv"

# ---- Run.
# Starts the server as BepInExPack's start_server_bepinex.sh does and stops it once the log has a line
# matching $2 (or after $3 seconds); its BepInEx log is kept as $logs/$1.log.
run() {
  local run=$1 until=$2 limit=$3 log="$server/BepInEx/LogOutput.log" pid elapsed=0
  rm -f "$log"
  (
    cd "$server"
    export DOORSTOP_ENABLED=1 DOORSTOP_TARGET_ASSEMBLY=./BepInEx/core/BepInEx.Preloader.dll
    export LD_LIBRARY_PATH="./doorstop_libs:./linux64:${LD_LIBRARY_PATH:-}" LD_PRELOAD="libdoorstop_x64.so:${LD_PRELOAD:-}"
    export SteamAppId=892970
    exec ./valheim_server.x86_64 -nographics -batchmode -name "Moder Warehouse Plus smoke test" -port 2456 \
      -world Smoke -password smoke-test-1 -public 0 -savedir "$work/saves"
  ) > "$logs/$run.console.log" 2>&1 &
  pid=$!
  until grep -qE "$until" "$log" 2>/dev/null; do
    if ! kill -0 "$pid" 2>/dev/null; then echo "The server stopped before: $until" >&2; break; fi
    if [ "$elapsed" -ge "$limit" ]; then echo "No '$until' after ${limit}s." >&2; break; fi
    sleep 2; elapsed=$((elapsed + 2))
  done
  kill -INT "$pid" 2>/dev/null || true
  for _ in $(seq 30); do kill -0 "$pid" 2>/dev/null || break; sleep 1; done
  kill -KILL "$pid" 2>/dev/null || true
  wait "$pid" 2>/dev/null || true
  cp "$log" "$logs/$run.log" 2>/dev/null || touch "$logs/$run.log"
  echo "Run '$run': stopped after ${elapsed}s; its log is $logs/$run.log"
}

failed=0
expect() {   # $1 run, $2 what, then a grep -E pattern that must (or with !, must not) be in the run's log
  local run=$1 what=$2 wanted=yes found=no
  shift 2
  if [ "$1" = '!' ]; then wanted=no; shift; fi
  if grep -qaE "$1" "$logs/$run.log"; then found=yes; fi
  if [ "$found" = "$wanted" ]; then
    echo "  ok: $what"
  else
    echo "  FAILED: $what"; failed=1
    if [ "$wanted" = no ]; then grep -aE -m 20 "$1" "$logs/$run.log" | sed 's/^/    /'; fi
  fi
}
check() {   # $1 what, then a test(1) expression
  local what=$1; shift
  if test "$@"; then echo "  ok: $what"; else echo "  FAILED: $what"; failed=1; fi
}
escaped=$(printf '%s' "$name" | sed 's/[.[\]]/\\&/g')

run plus "$escaped: localization" 600
echo "Run 'plus': Plus beside ServersideQoL and ContainerSigns, switching over from the original"
expect plus "ServersideQoL $sqol loaded" "Loading \[ServersideQoL $sqol\]"
expect plus "ContainerSigns loaded" "Loading \[ServersideQoL\.ContainerSigns"
expect plus "Plus loaded" "Loading \[$escaped\]"
expect plus "the original's settings copied" "Settings copied from ch\.cris\.valheim\.moderwarehouse\.cfg to Tie\.ModerWarehousePlus\.cfg\."
expect plus "the original's stock cache copied" "Stock cache copied from .*ModerWarehouse-stock-cache\.tsv"
expect plus "last-known stock read" "Loaded last-known stock for 1 warehouse"
expect plus "warehouse processing started" "$escaped: localization"
expect plus "no error or warning from Plus" ! "^\[(Error|Fatal|Warning) *: *$escaped\]"
expect plus "no exception in Plus" ! "at ModerWarehouse\."
check "Tie.ModerWarehousePlus.cfg has the original's radius" -n "$(grep -E '^Radius = 22$' "$config/Tie.ModerWarehousePlus.cfg" 2>/dev/null)"
check "the stock cache is in config/ModerWarehousePlus/" -f "$config/ModerWarehousePlus/ModerWarehouse-stock-cache.tsv"
check "the performance log is in config/ModerWarehousePlus/" -f "$config/ModerWarehousePlus/ModerWarehouse-performance.log"
check "the original's files are left in place" -f "$config/ch.cris.valheim.moderwarehouse.cfg" -a -f "$plugins/ModerWarehouse/ModerWarehouse-stock-cache.tsv"

download CrisHaani-ModerWarehouse 2.0.0
unzip -qjo "$work/CrisHaani-ModerWarehouse.zip" 'BepInEx/plugins/ModerWarehouse/ModerWarehouse.dll' -d "$plugins/ModerWarehouse"
run original 'Chainloader startup complete' 300
echo "Run 'original': the original Moder Warehouse installed too"
expect original "Plus refuses to load beside the original" "Could not load \[$escaped\] .*because it is incompatible with: ch\.cris\.valheim\.moderwarehouse"
expect original "Plus did not start" ! "Loading \[$escaped\]"

[ "$failed" = 0 ] && echo "Smoke test passed." || { echo "Smoke test FAILED (logs in $logs)." >&2; exit 1; }
