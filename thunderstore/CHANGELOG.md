# Changelog

## 1.1.0 — 2026-11-02

Brings in Moder Warehouse 2.0.1 to 2.0.3 by Cris Haani. Existing warehouses, labels,
boards and statistics carry over; nothing needs to be rebuilt.

- New chest types: category chests (`ORE`, `BARS`, `FOOD`, `MEADS`, `AMMO`, `TROPHIES`,
  `WEAPONS`, `ARMOR`, `TOOLS`, `FISH`), a catch-all `MISC` chest, and refill chests
  (`[WH:<id>:FILL] Coal 50`) that keep a chest next to a smelter or kitchen stocked.
- Numbered labels such as `WOOD1`, `WOOD2`: lower numbers fill first. Item types spread
  over several chests are gathered back together, and an emptied chest whose sign is
  set to `...` is free for a new item.
- The stock board shows `NO ROOM` when items find no place; boards show `INACTIVE` with
  the reason instead of freezing.
- Statistics keep daily totals for a year (item details for 90 days), the period goes up
  to 365 days, the trends page forecasts when the chests will be full, and each world
  save writes a CSV per warehouse to `BepInEx/config/ModerWarehousePlus/`.
- A request without an OUT chest says so. `TARGET`/`SOLL` lines work in every language
  and are no longer taken as a withdrawal. A withdrawal never mixes two items that share
  a name (German Harz, Stein). Bars, ores and scrap no longer share German names.
- Chests a player has open stay counted in stock and statistics. Signed chests holding
  internal or cheated items are reported in the log and left alone.
- Faster cycles and fewer stalls when arriving at a base: inventories are read a few
  milliseconds per frame, and other warehouses' stock is read from a cache.
- Removed: the `AutoLabelDelay` setting and conversions of pre-2.0 worlds.

## 1.0.2 — 2026-10-20

- No more short freezes on big worlds: checking the warehouses (every 5 minutes) and
  counting other warehouses' stock for a request no longer go through every object in
  the world, which could hold the whole server for a quarter of a second.

## 1.0.1 — 2026-10-13

- With ServersideQoL ContainerSigns: the warehouse no longer removes or moves the
  signs ContainerSigns placed (it only writes the label on them). Removing one made
  ContainerSigns fail when that chest was later removed, and the game then skipped
  the other objects removed at the same moment. `[WH:CLEAN]` and superseded labels
  also leave ContainerSigns signs alone.

## 1.0.0 — 2026-10-13

First release of Moder Warehouse Plus, a fork of Moder Warehouse 2.0.0 by Cris Haani.

- Crafting requests on REQ signs: `craft <item> [lvl N] [xN]`, for recipes, upgrade
  levels and hammer, hoe and cultivator pieces; only what OUT lacks is taken.
- Wildcard requests: `*mead*`, `*frost resistance 5`, also after `craft`.
- With ServersideQoL ContainerSigns: inside a warehouse its signs show the warehouse's
  labels (the label is stored on the chest too, so deposits and restarts keep it);
  outside warehouses ContainerSigns is unchanged. ContainerSigns stays optional.
- REQ and INFO signs keep their warehouse when ContainerSigns recreates chest signs.
- An abandoned IN chest's in-use lock is released (`[Safety]` settings).
- Faster scans on large warehouses; each chest is saved once per cycle.
- Settings from `ch.cris.valheim.moderwarehouse.cfg` and the stock cache from
  `plugins/ModerWarehouse/` are copied on the first start. The stock cache and
  performance log are now in `BepInEx/config/ModerWarehousePlus/`, which mod managers
  leave alone on updates.
- Needs ServersideQoL 2.3.0 or newer.
