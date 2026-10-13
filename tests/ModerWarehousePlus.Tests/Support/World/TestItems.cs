using HarmonyLib;

namespace ModerWarehouse.Tests;

/// <summary>
/// The items the test world knows, as the game's item database (ObjectDB) would give them: each
/// with its token, prefab name, English and German name and stack size. Their names are written to
/// the warehouse's name tables, as it does from the game's items when it starts (LoadGermanNames).
/// </summary>
public sealed class TestItems
{
    private static readonly Type Warehouse = typeof(WarehouseProcessor);

    public TestItems()
    {
        Wood = Define("Wood", "$item_wood", "Wood", "Holz", 50);
        FineWood = Define("FineWood", "$item_finewood", "Fine wood", "Feines Holz", 50);
        Stone = Define("Stone", "$item_stone", "Stone", "Stein", 50);
        Resin = Define("Resin", "$item_resin", "Resin", "Harz", 50);
        Amber = Define("Amber", "$item_amber", "Amber", "Bernstein", 20);
        BronzeNails = Define("BronzeNails", "$item_bronzenails", "Bronze nails", "Bronzenägel", 100);
        MeadFrost = Define("MeadFrostResist", "$item_mead_frostres", "Frost resistance mead", "Frostschutzmet", 10);
        MeadHealth = Define("MeadHealthMinor", "$item_mead_hp_minor", "Minor healing mead", "Kleiner Heilmet", 10);
        Sword = Define("SwordIron", "$item_sword_iron", "Iron sword", "Eisenschwert", 1, ItemDrop.ItemData.ItemType.OneHandedWeapon);
        var names = Field<Dictionary<string, HashSet<string>>>("AliasOwners");
        names.Clear();
        foreach (var item in All)
        {
            foreach (string alias in ItemAliases(Make(item, 1)))
            {
                if (!names.TryGetValue(alias, out var owners))
                {
                    names[alias] = owners = [];
                }
                owners.Add(item.m_name);
            }
        }
        AccessTools.Field(Warehouse, "_germanNamesLoaded").SetValue(null, true);
    }

    public ItemDrop.ItemData.SharedData Wood { get; }

    public ItemDrop.ItemData.SharedData FineWood { get; }

    public ItemDrop.ItemData.SharedData Stone { get; }

    public ItemDrop.ItemData.SharedData Resin { get; }

    public ItemDrop.ItemData.SharedData Amber { get; }

    public ItemDrop.ItemData.SharedData BronzeNails { get; }

    public ItemDrop.ItemData.SharedData MeadFrost { get; }

    public ItemDrop.ItemData.SharedData MeadHealth { get; }

    public ItemDrop.ItemData.SharedData Sword { get; }

    public IEnumerable<ItemDrop.ItemData.SharedData> All => [Wood, FineWood, Stone, Resin, Amber, BronzeNails, MeadFrost, MeadHealth, Sword];

    /// <summary>A stack of an item, as found in a chest.</summary>
    public static ItemDrop.ItemData Make(ItemDrop.ItemData.SharedData item, int stack, bool cheated = false) =>
        new() { m_shared = item, m_stack = stack, m_quality = 1, m_durability = item.m_maxDurability, m_cheated = cheated };

    private static ItemDrop.ItemData.SharedData Define(string prefab, string token, string english, string german, int maxStack,
        ItemDrop.ItemData.ItemType type = ItemDrop.ItemData.ItemType.Material)
    {
        var shared = new ItemDrop.ItemData.SharedData { m_name = token, m_maxStackSize = maxStack, m_itemType = type, m_maxQuality = maxStack == 1 ? 4 : 1 };
        Field<Dictionary<string, string>>("EnglishNames")[token] = WarehouseProcessor.Normalize(english);
        Field<Dictionary<string, string>>("EnglishDisplayNames")[token] = english;
        Field<Dictionary<string, string>>("GermanNames")[token] = WarehouseProcessor.Normalize(german);
        Field<Dictionary<string, string>>("GermanDisplayNames")[token] = german;
        Field<Dictionary<string, string>>("PrefabNames")[token] = prefab;
        return shared;
    }

    private static IEnumerable<string> ItemAliases(ItemDrop.ItemData item) =>
        (IEnumerable<string>)AccessTools.Method(Warehouse, "ItemAliases").Invoke(null, [item])!;

    private static T Field<T>(string name) => (T)AccessTools.Field(Warehouse, name).GetValue(null)!;
}
