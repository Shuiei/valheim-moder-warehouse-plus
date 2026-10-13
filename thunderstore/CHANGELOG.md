# Changelog

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
