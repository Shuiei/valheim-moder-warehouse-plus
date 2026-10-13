# Translating Moder Warehouse

Moder Warehouse ships with maintained English and German text. Other languages can be
added as data files without rebuilding the DLL.

## What changes language

The selected language controls all text owned by Moder Warehouse: the 4×4 board,
sleep/wake display, help sign, INFO and request displays, delivery/visit messages,
capacity warnings and automatic chest labels. Player-authored signs are never
translated or rewritten. Marker commands such as `[WH:main:OUT]` are a stable protocol
and remain identical in every language.

English and German vanilla item names are built into search and automatic labels.
Community JSON files currently translate the warehouse interface; item searches in a
community language require that language's item-name table to be added to a future
release. English names, German names and internal prefab names continue to work in
every interface language.

## Adding a language (no DLL changes)

1. Start the server once with Moder Warehouse 2.0.0 or newer.
2. Copy `BepInEx/config/ModerWarehouse/localization/TranslationTemplate.json`.
3. Name the copy however you like, for example `Spanish.json` or `Serbian.json`.
4. Set `language` to a language tag, for example `es`, `es-MX`, `sr` or `sr-Latn`.
   Set `language_name` to the display name and `translator` to the contributor credit.
5. Translate the values inside `strings`. Never change the keys on the left. A complete
   translation is recommended; missing or empty entries are allowed during drafting
   and use English fallback until filled in.
6. Preserve every numbered format placeholder exactly (`{0}`, `{1}`, etc.). For
   `board.sleep_word` and `stats.sleep_word`, translate only the visible word; do not
   add trailing `Z` padding. The server adds and colors the decorative Z cells itself.
7. Save as UTF-8 and restart the server. The startup log reports the language and
   translation-gate result. No client-side file or matching Valheim client language is
   required; this is server-side text. YamlDotNet 16.3.1 is a server dependency used
   to read JSON; Thunderstore declares it in the manifest, and clients do not install it.
8. Select it globally with `Language = es`, or add `LANG=es` on a new line
   below a warehouse IN marker.

Example:

```text
[WH:main:IN]
LANG=es
```

The JSON structure is `{ "language": "es", "language_name": "Español",
"translator": "Name", "strings": { ... } }`; use the generated template for the
complete `strings` object rather than creating the file from scratch. The gate checks
the language tag, required display name, duplicate language codes/names, unknown keys,
missing/empty values and numbered format placeholders. Missing or empty strings safely
fall back to English and are listed in the startup log. Unknown keys are ignored and
reported (usually a misspelled key). Incorrect placeholders or invalid metadata reject
the file with a specific startup-log reason. Keep exactly one JSON file per language
code. When an update adds a key, older translations keep working via English fallback;
the server also appends new keys to an existing `TranslationTemplate.json` on startup
without replacing existing entries, so translators can copy the refreshed template.

The language file translates Moder Warehouse interface text, not Valheim item names.
English/German item names and internal prefab names remain searchable. A separate item
name table would be needed for translated search terms in a community language.

Before sharing a translation:

1. Validate the JSON syntax and save it as UTF-8.
2. Start a test server and confirm the startup log says `Translation gate: complete`.
3. Test board active/sleep mode, help, INFO, requests, target stock, delivery text and
   capacity warnings.
4. Test every placeholder-bearing message with realistic numbers.
5. Confirm long words still fit on the vanilla signs.
6. Include the language code, language name, translator credit and the Moder Warehouse
   version used for testing.

Translation contributions may be distributed as separate files first and incorporated
into Moder Warehouse after review. Contributors must have the right to license their
text for redistribution under the project license.
