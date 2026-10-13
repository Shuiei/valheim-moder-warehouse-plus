# Moder Warehouse Plus

Server-only BepInEx plugin for Valheim: an automatic warehouse driven by signs. It sorts an IN
chest into labelled storage, fills REQ requests (including `craft <item>` and `*` wildcards),
tracks target stock and draws stock and statistics boards. Players need no mod.

A fork of [Moder Warehouse](https://thunderstore.io/c/valheim/p/CrisHaani/ModerWarehouse/) 2.0.0
by Cris Haani (MIT). What it adds, and how to use it: [the mod page](thunderstore/README.md).
Install with a mod manager: [Tie-ModerWarehousePlus on Thunderstore](https://thunderstore.io/c/valheim/p/Tie/ModerWarehousePlus/).

## Building

```sh
dotnet build -c Release
```

It builds against a Valheim dedicated server's DLLs, BepInEx's and ServersideQoL 2.3's. The
defaults in `Directory.Build.props` (shared with the tests) are one machine's paths; give your own with
`-p:ValheimDir=<server folder>`, or each folder with `-p:ManagedDir=`, `-p:BepInExCoreDir=`,
`-p:PluginsDir=` (YamlDotNet) and `-p:ServersideQoLDir=`.

`thunderstore/build.sh <dist> [-p:...]` builds the package zip (manifest, mod page, changelog,
icon from `thunderstore/make_icon.py`, license, translation guide, DLL). The version is
`Version` in `ModerWarehouse/WarehousePlugin.cs`, and `thunderstore/CHANGELOG.md` needs a
section for it.

## Testing

```sh
dotnet test tests/ModerWarehousePlus.Tests -c Release
tools/server-smoke.sh <dedicated server folder> dist/ModerWarehousePlus-<version>.zip
```

- **Unit tests** (`tests/ModerWarehousePlus.Tests`, xUnit on .NET 10, one test class per plugin file):
  requests, sign texts, ContainerSigns label choice, the first-start copies, translations,
  statistics history, board texts and the small policies. `Package/` checks the package's files
  against the plugin.
- **Warehouse tests** (`World/`): the real `WarehouseProcessor` run over a stand-in world
  (`Support/World/TestWorld`) of chests with real Valheim inventories, signs, items, recipes,
  players and a clock: sorting, requests, crafting, labels and the janitor, ContainerSigns, IN
  signs and their checks, renames, boards, statistics, several warehouses, stale IN locks. The
  game's code is used as ServersideQoL patches it in the game (`tests/GamePatcher` runs its
  preloader patcher); `Support/World/GameStubs` replaces, with Harmony, only the code that would
  reach Unity's engine or the live world (each patch says why). The warehouse reads the time
  through `WarehouseClock`, which the tests move on.
- **Server smoke test** (`tools/server-smoke.sh`): the package in a real dedicated server with
  BepInEx, ServersideQoL and ContainerSigns. It must load, copy the original mod's settings and
  stock cache, start with no error of its own, and refuse to load beside the original Moder
  Warehouse. It changes the server folder it is given: use a server kept for it (SteamCMD:
  `+login anonymous +app_update 896660`).
- **Not covered:** what only the game has: real players' clients, the game's item database and
  prefabs (stubbed), and the world's zones loading around players. Try a release on a test server
  before tagging it.

## Releasing

CircleCI (`.circleci/config.yml`) runs the unit tests, builds the package and runs the smoke test
on every push, from the game's DLLs and server (Steam's free dedicated server) and the mod DLLs on
Thunderstore; on main it also sends the tests' coverage to Codecov (`codecov.yml`: the check fails under 70%). To release:

1. Raise `Version` in `WarehousePlugin.cs` and add its section to `thunderstore/CHANGELOG.md`.
2. Push a tag `v<version>`, e.g. `git tag v1.0.1 && git push origin v1.0.1`.

The tag's pipeline publishes the zip (`tools/publish.py`) to a GitHub release, Thunderstore,
Hexium and Nexus Mods, with tokens from the CircleCI context `mod-token`:

| Variable | What |
|---|---|
| `GITHUB_TOKEN` | GitHub token that can create releases here (fine-grained: Contents read & write) |
| `THUNDERSTORE_TOKEN` | Thunderstore service account token of the team Tie |
| `HEXIUM_TOKEN` | Hexium team API token of Tie |
| `NEXUS_API_KEY` | Nexus Mods personal API key |

`NEXUS_MOD_ID`, the mod's number on Nexus Mods, is set in this project's CircleCI settings: a
Nexus mod page can only be made on the website. So is `CODECOV_TOKEN`, the repository's upload
token on Codecov. A site without its variable is skipped. Each
version goes to each site once; running a failed release again skips the sites that have it.

## License

MIT: [LICENSE](LICENSE). Moder Warehouse © 2026 Cris Haani; the Plus changes © 2026 Tie.
