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
icon, license, translation guide, DLL). The version is `Version` in
`ModerWarehouse/WarehousePlugin.cs`, and `thunderstore/CHANGELOG.md` needs a section for it. The
icon and the Nexus Mods page's banner are drawn by `dotnet run --file thunderstore/make_images.cs`.

The tools are .NET file-based apps, run with `dotnet run --file` (the repository's root holds the
plugin's project, which `dotnet run` alone would run instead).

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

CircleCI runs the unit tests, builds the package and runs the smoke test on every push, from the
game's DLLs and server (Steam's free dedicated server) and the mod DLLs on Thunderstore; on main it
keeps the packages as artifacts and sends the tests' coverage to Codecov (`codecov.yml`: the check
fails under 70%; the token is the project's `CODECOV_TOKEN`). It makes the release once you approve
it (`.circleci/config.yml`, `.circleci/continue.yml`):

1. While working, the version's `thunderstore/CHANGELOG.md` heading says `— unreleased`
   (`## 1.0.1 — unreleased`), with `Version` raised in `ModerWarehouse/WarehousePlugin.cs`.
2. To release, date the heading (`## 1.0.1 — 2026-10-14`) and push it to main (or merge a pull
   request). On main, a dated heading for the plugin's version with no `v<version>` tag yet makes the
   pipeline a release one: the usual jobs, then **hold-release** waits.
3. In CircleCI, open that pipeline's workflow (`release`) and approve **hold-release**. Then:
   - **github-release** tags the commit `v<version>` and makes the GitHub release with the zip the
     pipeline built and tested. Its notes are `docs/releases/v<version>.md` when there is one, else
     the changelog's sections since the last release;
   - **publish-thunderstore**, **publish-hexium** and **publish-nexus** publish side by side
     (`tools/release/publish.cs`, with the sites' settings in `tools/release/sites.json`):
     `ModerWarehousePlus-<version>.zip` on Thunderstore and Hexium (team `Tie`), and on Nexus Mods
     (mod 4413) repacked to unzip into the game's folder (`BepInEx/plugins/ModerWarehousePlus/`), as
     a new version of the page's file, with the notes as the mod's changelog.

A version already on a site fails its job: no site takes a version twice. A failed job can be rerun
from CircleCI (Rerun from failed); the others are done. Still by hand: **unhide** the new version on
Hexium (its Versions page on hexium.gg; uploads arrive hidden), and the Nexus page's description and
pictures (`thunderstore/nexus.bbcode`, `thunderstore/nexus-banner.png`: no API changes them).

The release jobs' secrets are in CircleCI's **mod-token** context, restricted to the maintainer and
shared by the mods' projects: `GITHUB_TOKEN` (a GitHub token with Contents read and write on this
repository), `THUNDERSTORE_TOKEN` (a service account of team Tie), `HEXIUM_TOKEN` (a team API token
of Tie) and `NEXUS_API_KEY` (a personal API key). The project needs CircleCI's dynamic configuration
(Project Settings › Advanced › Enable dynamic config using setup workflows).

To check the publishing without publishing:
`dotnet run --file tools/release/publish.cs -- <thunderstore|hexium|nexus> <folder> --dry-run`, with
the zip in the folder and the site's token in the environment. It checks the token, that the version
is not on the site yet, the categories and the files.

By hand, without CI: `thunderstore/build.sh <folder>`, then tag `v<version>` and
`gh release create` with the zip, then `publish.cs` for each site.

## License

MIT: [LICENSE](LICENSE). Moder Warehouse © 2026 Cris Haani; the Plus changes © 2026 Tie.
