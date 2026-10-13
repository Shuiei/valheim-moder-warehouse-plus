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
defaults in `ModerWarehousePlus.csproj` are one machine's paths; give your own with
`-p:ValheimDir=<server folder>`, or each folder with `-p:ManagedDir=`, `-p:BepInExCoreDir=`,
`-p:PluginsDir=` (YamlDotNet) and `-p:ServersideQoLDir=`.

`thunderstore/build.sh <dist> [-p:...]` builds the package zip (manifest, mod page, changelog,
icon from `thunderstore/make_icon.py`, license, translation guide, DLL). The version is
`Version` in `ModerWarehouse/WarehousePlugin.cs`, and `thunderstore/CHANGELOG.md` needs a
section for it.

## Releasing

CircleCI (`.circleci/config.yml`) builds the package on every push, from the game's DLLs
(downloaded from Steam's free dedicated server) and the mod DLLs on Thunderstore. To release:

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
Nexus mod page can only be made on the website. A site without its variable is skipped. Each
version goes to each site once; running a failed release again skips the sites that have it.

## License

MIT: [LICENSE](LICENSE). Moder Warehouse © 2026 Cris Haani; the Plus changes © 2026 Tie.
