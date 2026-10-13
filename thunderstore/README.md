# Moder Warehouse Plus

A server-only warehouse for Valheim: the server sorts an input chest into your storage,
fills withdrawal requests, tracks target stock and shows it all on signs. Players join
with an unchanged game.

**Moder Warehouse Plus is a fork of [Moder Warehouse](https://thunderstore.io/c/valheim/p/CrisHaani/ModerWarehouse/)
2.0.0 by Cris Haani**, released under the MIT license. Everything Moder Warehouse does
is here; Plus adds crafting requests, wildcard requests, ContainerSigns support and a
few fixes. It replaces Moder Warehouse: install one or the other, not both.

## What Plus adds

- **Crafting requests.** On a REQ sign, `craft Iron sword` puts the materials for one
  into OUT. `craft Iron sword lvl 3` asks for an upgrade level, `craft Iron nails x5`
  for several. Only what OUT does not already hold is taken. Hammer, hoe and
  cultivator pieces count too (`craft Stone wall x10`). German: `herstellen … stufe 3`.
- **Wildcard requests.** `*mead*` or `*frost resistance 5` takes every matching item
  (up to 10 different items per request). `craft` accepts `*` as well.
- **ContainerSigns support.** See [ContainerSigns](#containersigns) below.
- **REQ and INFO signs keep their warehouse** when ContainerSigns replaces chest signs
  at a server start.
- **A stuck IN chest is freed.** When the player who opened the IN chest has left or
  disconnected, its in-use lock is released (settings in `[Safety]`).
- **Faster on big warehouses:** IN/OUT markers are found from signs only, world scans
  are quicker, and each chest is saved once per cycle instead of once per move.

## Requirements

Install on the dedicated server only:

1. [BepInExPack for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
2. [ServersideQoL](https://thunderstore.io/c/valheim/p/ArgusMagnus/ServersideQoL/) 2.3.0 or newer
3. [YamlDotNet](https://thunderstore.io/c/valheim/p/ValheimModding/YamlDotNet/) 16.3.1

Mod managers install these for you. Players joining the server need nothing.

## Installation

1. Stop the server and back up the world.
2. Install with a mod manager, or copy `ModerWarehousePlus.dll` to
   `BepInEx/plugins/ModerWarehousePlus/`.
3. Start the server and look for `Loading [Moder Warehouse Plus …]` in the BepInEx log.

### Switching from Moder Warehouse

Remove `ModerWarehouse.dll` first: Plus refuses to load next to it. Your warehouses,
labels, boards, statistics and REQ links are kept, since Plus reads the same world data.
On its first start Plus copies `ch.cris.valheim.moderwarehouse.cfg` to
`Tie.ModerWarehousePlus.cfg`, so your settings come along, and the last-known stock of
your warehouses from `plugins/ModerWarehouse/` to `config/ModerWarehousePlus/`. The old
files are left in place in case you go back; once you are sure you won't, the
`plugins/ModerWarehouse/` folder can be deleted.

Plus keeps its stock cache and performance log in `BepInEx/config/ModerWarehousePlus/`.

## ContainerSigns

[ServersideQoL ContainerSigns](https://thunderstore.io/c/valheim/p/ArgusMagnus/ServersideQoL_ContainerSigns/)
is optional. Without it, labels work as described below (automatic labels and `...` signs).

With it installed:

- **Outside a warehouse** nothing changes: ContainerSigns puts its signs and contents
  lists on chests as usual.
- **Inside a warehouse** the warehouse takes over ContainerSigns' signs. A chest whose
  ContainerSigns sign shows a contents list counts as enrolled, like a `...` sign, and
  that sign becomes the chest's label. The label is also stored on the chest itself,
  so ContainerSigns keeps showing it on all of that chest's signs, after deposits and
  across restarts.
- A chest that leaves a warehouse keeps showing its last label. Write ContainerSigns'
  placeholder (`•` by default) on the sign to get the contents list back.
- ContainerSigns 2.3 connects a player's sign to the nearest chest when its text is
  `SignConnectText` (`•` by default). Inside a warehouse such a sign is treated like a
  `...` sign and gets the chest's label. If you do not use that feature, set
  `SignConnectText =` (empty) in `ArgusMagnus.ServersideQoL.ContainerSigns.cfg`.

## Quick start

Choose a warehouse id such as `main`. Place ordinary signs no more than three metres
from their intended chests:

| Sign text | Purpose |
|---|---|
| `[WH:main:IN]` | input chest, warehouse centre and optional language selector |
| `[WH:main:OUT]` | output chest |
| `[WH:main:REQ]` | request and target-stock sign |
| `[WH:main:INFO]` | live container-capacity sign |
| `[WH:main:BOARD]` | bottom-left anchor of the automatic 4×4 board |
| `[WH:main:STATS]` | bottom-left anchor of the automatic 4×4 statistics board |
| `[WH:IGNORE]` | exclude the nearest container |
| `[WH:DELETE]` | remove a generated warehouse sign |
| `[WH:CLEAN]` | clean overlapping generated signs at one position |

Only suitable containers within the input radius that have an associated storage sign
are managed. A normal item label or an explicit blank `...` sign opts a chest in. A
chest without a sign is completely ignored: it is not scanned, changed, or included in
stock displays. `[WH:IGNORE]` explicitly excludes a chest. Private, open, in-use, cart
and unsuitable auto-destroy containers are not used. Items stay safely in the input
chest when no destination has capacity.

With Plus, a chest is also opted in by:

- a ContainerSigns sign showing its contents list (it becomes the chest's label, see
  [ContainerSigns](#containersigns));
- a sign with only `•` or other punctuation, like `...`;
- a label written as a contents line, such as `Wood 521`: it is read as `Wood` and
  cleaned to that.

Once initialised, a REQ sign takes:

| Write on the REQ sign | What goes into OUT |
|---|---|
| `Wood 100` | 100 wood |
| `Stone 10, Wood 20, Resin 5` | each of them |
| `SOLL Wood 1000, Stone 500` | nothing: saves target stock, shown as a shopping list |
| `craft Iron sword` (Plus) | the materials for one |
| `craft Iron sword lvl 3` (Plus) | the materials to upgrade it to level 3 |
| `craft Stone wall x10` (Plus) | the materials for ten (hammer, hoe and cultivator pieces too) |
| `herstellen Eisenschwert stufe 2` (Plus) | the same in German |
| `*mead* 2` (Plus) | 2 of every item whose name contains "mead" (up to 10 items) |
| `craft *frost*` (Plus) | the materials for the one recipe matching, or a list of the candidates |

`craft` counts what OUT already holds and only takes the rest. If an IN chest stays
marked in use after its player left or walked away, Plus frees it after 30 seconds
(90 seconds at most while they stand next to it; see `[Safety]` in the settings).

## Internal and cheat-object safety filter

Moder Warehouse sorts only genuine player-handled items. Internal attack prefabs,
boss abilities, developer weapons and cheated items are ignored in the player-facing
IN chest and remain there for the player to remove. The mod never deletes or rewrites
rejected IN contents. Valid items in the same IN chest continue to be sorted.

If the scanner finds a forbidden object in managed storage, it removes only that
object; genuine items in the same chest remain untouched. An automatically labelled
chest that becomes empty is released for normal use again.

If a genuine item is manually placed in the wrong labelled storage chest, housekeeping
moves it to a matching labelled, homogeneous or free chest. It is never deleted. Every
correction records item, amount, source and target in the server log. Open or currently
used chests remain untouched until a later scan.

`[WH:IGNORE]` is the explicit quarantine mechanism. Excluded chests are neither
scanned nor cleaned.

Valheim stores the persistent `m_cheated` marker on each individual item. When it is
set, even an otherwise ordinary item such as wood or stone is denied sorting. Every
scan checks this marker in addition to blocking internal and dedicated cheat prefabs.

## Languages

English and German are maintained built-in languages. Set the server-wide default in
`ch.cris.valheim.moderwarehouse.cfg`:

```ini
[Localization]
Language = English
```

Use `English`, `German`, `en` or `de`. A warehouse can override the server default by
adding a second line to its IN sign:

```text
[WH:main:IN]
LANG=DE
```

Changing `LANG=` updates the board, help, capacity, request, delivery and automatic
chest-label text on the next scans. Physical signs are shared world objects, so one
warehouse shows one language to every player. Manually written signs are deliberately
never translated or rewritten.

Additional interface languages can be installed as JSON data files. See
[`TRANSLATING.md`](https://github.com/Shuiei/valheim-moder-warehouse-plus/blob/main/docs/TRANSLATING.md) (also in the package). Missing entries fall back to English.
English and German item names are accepted for searches regardless of display language.

## Input and automatic labels

Put unsorted items into the IN chest. Existing matching stacks are filled before empty
slots are used.

To enroll a destination, attach either a normal item label or a blank vanilla sign
whose complete text is `...`. An unsigned chest remains private to the player and is
invisible to the warehouse. Once an enrolled chest contains only one item type, Moder
Warehouse recognises it as a destination for matching items. If `AutoCreateLabels` is
enabled, it gets a persistent white label after `AutoLabelDelay` (90 seconds by
default); an existing `...` sign is reused.

For exact sign placement, attach and orient an ordinary vanilla sign yourself and set
its complete text to `...`. Moder Warehouse reuses that sign instead of creating one.
Ordinary storage labels and `...` adoption signs must be within `LabelDistance` of the
chest (one metre by default). The separate three-metre `MarkerDistance` applies to
functional signs such as IN, OUT and REQ and is deliberately not used for dense racks.
Automatically created labels are placed on the decorated oval front of vanilla chests;
older server-generated back-side labels are migrated automatically. Player-built signs
are never moved.
The first automatic assignment records the underlying Valheim item token. From then on,
the label follows `LANG=`/`Language`, even while the chest is empty. Do not use `...` on
a sign that should remain manually controlled.

Existing non-empty manual labels still reserve and route their nearest chest, but their
text is never translated. Older automatic signs are migrated only when the mod can
identify them safely; if an old label does not switch language, clear its text to `...`
once and let Moder Warehouse adopt it.

## Requests

Initialize a request sign once with `[WH:main:REQ]`. Its warehouse association is
stored persistently, after which the sign accepts requests such as:

```text
Wood 100
Stone 10, Wood 20, Resin 5
```

German names, English names and internal prefab names are supported. Each comma-
separated request receives its own result. Available items are delivered even if
another requested item is missing. Results remain visible for at most 8 seconds.
The REQ sign then returns to unmet target stock or to the neutral REQ prompt when all
targets are met. Keep the OUT chest closed while the server performs a request; a
saved target-stock list never overwrites a fresh command.

Also on a REQ sign (Plus):

```text
craft Iron sword
craft Iron sword lvl 3
craft Iron nails x5
*mead*
*frost resistance 5
```

`craft` puts the materials of a recipe, a building piece or an upgrade level into OUT,
counting what OUT already holds. A `*` matches any text; one wildcard request takes up
to 10 different items.

## Target stock

Store a persistent shopping list on the initialized REQ sign:

```text
SOLL Wood 1000, Stone 500, Resin 100
```

Red means missing or critically low, orange means below target and green means the
target has been met. Normal withdrawal requests remain available at any time.

## INFO sign

`[WH:main:INFO]` shows free enrolled chests with a blank `...` sign, empty
reserved/labeled chests and occupied chests versus total managed capacity. The
warehouse association persists after the first successful initialization.

## Interactive 4×4 stock board

Place `[WH:main:BOARD]` as the **bottom-left** cell and leave space to its right and
above. The server creates fifteen additional vanilla signs. The sign's written face
defines the board front; the board grows right and upward.

The top row shows warehouse, filter, page and container occupancy. Twelve alphabetical
item totals are shown below it. Pages rotate every three seconds by default. With
multiple valid warehouses, an additional cached network overview page is available.

- Edit **BESTAND** with a prefix such as `G` to filter and freeze the list.
- Edit **SEITE** with a page number to hold that page.
- Use both controls together when required.
- Enter `AUTO` or clear the control to resume rotation immediately.
- Automatic rotation resumes after 60 seconds without another filter change.

The board wakes while a player is within five metres. Otherwise it displays a large
sleep message and stops rotating. On waking it briefly shows stock changes since the
last visit. After input sorting it shows an `EINGELAGERT` delivery confirmation.

Container occupancy is green below 80 percent, orange from 80 percent, and red with
an expansion warning from 95 percent.

## 4×4 statistics board

Place one vanilla sign as the **bottom-left** anchor and write:

```text
[WH:main:STATS]
```

The server creates fifteen signs to the right and above. Four local pages rotate automatically:
today, a direct 7/30/90-day comparison, trends/forecast, and dated records. Metrics
include deposits, withdrawals, balance, daily average, top items, current total,
item types, current and average chest usage, peak/low stock, strongest trends, the item
expected to run out first, and the input/output ratio.

Movement history is stored as compact daily aggregates on the anchor sign. Windows
roll every day instead of resetting at the start of a month. Data older than 90 days is
automatically removed, so storage cannot grow forever. Player identities are not
recorded. Forecasts are simple flow projections, not guarantees. The statistics board
uses the same wake distance and sleep behaviour as the stock board.

Two cells are interactive: edit `PERIOD` to any whole number from `1` to `90`, and
edit `PAGE` to `1`, `2`, `3` or `4`. The server restores its rendered text after
reading the command. A separate control sign is created below the board and accepts
combined commands such as `27 TREND`, `30 3`, `TODAY OVERVIEW` or `AUTO`. German
commands such as `27 TREND`, `HEUTE UEBERSICHT` and `VERGLEICH` are accepted too.
Manual selection returns to normal page rotation after 60 seconds without another
input. Forecasts remain marked as insufficient until a useful data basis exists.

## Multi-warehouse network

Reduce an individual warehouse radius directly on its IN sign:

```text
[WH:main:IN] R=60
```

The configured server radius remains the upper limit. IN chests require at least
210 metres separation by default. The automatic help sign reports radius and zone
validity. Conflicting zones and duplicate ids are locked until corrected.

REQ always searches and withdraws locally first. Only after a complete local miss
does it read other valid warehouses and report their id and available amount. This
world lookup is informational only: it never claims a remote chest and never moves,
reserves or teleports an item.

Warehouse IDs are globally unique (case-insensitive). A new IN sign is rejected with
a localized reason if its ID is already in use, its input chest is missing, or it is
less than the configured minimum distance (210 m by default) from another valid IN.
Existing valid warehouses are protected from a conflicting new marker. Renaming an
IN ID migrates its linked REQ, INFO, help, BOARD and STATS signs, generated board
cells, and associated runtime/statistics state when the links are unambiguous.

REQ searches the local warehouse first. On a local miss, its read-only world lookup
can inspect persistent remote inventory data without a player standing at that
warehouse. Results distinguish `LIVE`, timestamped last-known (`AS OF`), and
`UNKNOWN` data; an incomplete or failed read is never presented as live. Remote
results are informational only: they do not reserve, claim, or transfer items.
Fuzzy suggestions are limited to plausible item-name typos, show the matched item in
green on the large stock board, and page through longer lists before returning to
normal stock display. Leaving the board area resets the search display.

From the second valid warehouse onward, stock and statistics boards automatically
gain a cached network overview page. It is informational, not a cross-warehouse
transfer system. Warehouse inventory and movement statistics remain separate.

## Expanding and running multiple warehouses

Build more suitable chests with a storage sign within the configured radius; unsigned
chests are not enrolled. Capacity updates on the next scan. Use another id for an
independent site, for example `[WH:harbour:IN]` and `[WH:harbour:OUT]`.

## Configuration defaults

| Setting | Default | Meaning |
|---|---:|---|
| `Enabled` | `true` | enable warehouse processing |
| `Language` | `English` | default interface and automatic-label language |
| `Radius` | `100` | storage radius around the IN chest |
| `MarkerDistance` | `3` | maximum sign-to-chest distance |
| `LabelDistance` | `1` | maximum storage-label or `...` sign distance |
| `ScanInterval` | `3` | seconds between scans |
| `MaxMovesPerCycle` | `250` | safety limit for stack movements |
| `AutoCreateLabels` | `true` | create automatic chest labels |
| `AutoLabelDelay` | `90` | seconds before automatic labeling |
| `ResultDisplaySeconds` | `8` | maximum request-result display time |
| `BoardPageSeconds` | `3` | stock-board page duration |
| `BoardWakeDistance` | `5` | stock-board wake distance |
| `DeliveryNoticeSeconds` | `6` | delivery-message duration |
| `VisitSummarySeconds` | `12` | visit-difference duration |
| `CapacityWarningPercent` | `80` | orange capacity threshold |
| `StaleInputLockSeconds` | `30` | seconds before an abandoned IN chest's in-use lock is released (`0` disables) |
| `StaleInputLockPlayerDistance` | `4` | the opener keeps the lock while this close (m) |
| `StaleInputLockMaxSeconds` | `90` | the lock is released after this long regardless |

| `CapacityCriticalPercent` | `95` | red capacity threshold |

Test configuration changes on a world copy. Very short scan intervals and very large

## Troubleshooting

- **Plugin does not load:** verify BepInEx and ServersideQoL 2.3+, remove `ModerWarehouse.dll`
  and duplicate DLLs,
  and check the log for `Loading [Moder Warehouse Plus …]`.
- **IN or OUT is not detected:** verify exact spelling, matching warehouse id and a
  sign-to-chest distance below three metres.
- **Items remain in IN:** check capacity, matching stacks, private/open containers and
  nearby `[WH:IGNORE]` signs.
- **Request fails:** use `Item amount`, separate entries with commas, and keep enough
  free space in OUT.
- **Automatic label stays in the old language:** only mod-managed labels are rewritten.
  Manual signs stay untouched. Replace an old automatic label's text with `...` once
  to let the mod adopt it safely.
- **Board grows incorrectly:** the anchor is the bottom-left cell; clear the space to
  the right and above, remove it with `[WH:DELETE]` or `[WH:CLEAN]`, then rebuild once.

## License and credits

MIT. Moder Warehouse © 2026 Cris Haani; the Plus changes © 2026 Tie. See `LICENSE`.
Moder Warehouse Plus is not made or endorsed by Cris Haani, Iron Gate Studio or Coffee
Stain Publishing. Back up your world before installing or updating.

Source and issues: https://github.com/Shuiei/valheim-moder-warehouse-plus

## Support

If this mod saved you some trouble:

[![Buy me a coffee](https://cdn.buymeacoffee.com/buttons/v2/default-yellow.png)](https://buymeacoffee.com/tieboo)
