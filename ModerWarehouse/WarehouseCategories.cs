using System;
using System.Collections.Generic;

namespace ModerWarehouse;

internal static class WarehouseCategories
{
	internal enum Category
	{
		None,
		Ore,
		Bars,
		Food,
		Meads,
		Ammo,
		Trophies,
		Weapons,
		Armor,
		Tools,
		Fish,
		Misc
	}

	private static readonly Dictionary<string, Category> Keywords = new Dictionary<string, Category>(StringComparer.Ordinal)
	{
		["erz"] = Category.Ore,
		["erze"] = Category.Ore,
		["ore"] = Category.Ore,
		["ores"] = Category.Ore,
		["barren"] = Category.Bars,
		["bars"] = Category.Bars,
		["ingots"] = Category.Bars,
		["essen"] = Category.Food,
		["nahrung"] = Category.Food,
		["food"] = Category.Food,
		["met"] = Category.Meads,
		["mead"] = Category.Meads,
		["meads"] = Category.Meads,
		["tränke"] = Category.Meads,
		["traenke"] = Category.Meads,
		["potions"] = Category.Meads,
		["munition"] = Category.Ammo,
		["ammo"] = Category.Ammo,
		["trophäen"] = Category.Trophies,
		["trophaeen"] = Category.Trophies,
		["trophies"] = Category.Trophies,
		["trophy"] = Category.Trophies,
		["waffen"] = Category.Weapons,
		["weapons"] = Category.Weapons,
		["rüstung"] = Category.Armor,
		["ruestung"] = Category.Armor,
		["armor"] = Category.Armor,
		["armour"] = Category.Armor,
		["werkzeug"] = Category.Tools,
		["werkzeuge"] = Category.Tools,
		["tools"] = Category.Tools,
		["fisch"] = Category.Fish,
		["fische"] = Category.Fish,
		["fish"] = Category.Fish,
		["sonstiges"] = Category.Misc,
		["diverses"] = Category.Misc,
		["misc"] = Category.Misc,
		["overflow"] = Category.Misc
	};

	private static readonly HashSet<string> BarTokens = new HashSet<string>(StringComparer.Ordinal) { "copper", "tin", "bronze", "iron", "silver", "blackmetal", "flametal" };

	internal static bool TryParse(string normalizedLabel, out Category category)
	{
		return Keywords.TryGetValue(normalizedLabel ?? string.Empty, out category);
	}

	internal static bool Contains(Category category, string token, string itemType, float food, float foodStamina)
	{
		string text = itemType ?? string.Empty;
		string text2 = BaseName(token);
		switch (category)
		{
		case Category.Ore:
			if (text == "Material")
			{
				if (!text2.EndsWith("ore", StringComparison.Ordinal) || text2.EndsWith("core", StringComparison.Ordinal))
				{
					return text2.EndsWith("scrap", StringComparison.Ordinal);
				}
				return true;
			}
			return false;
		case Category.Bars:
			if (text == "Material")
			{
				return BarTokens.Contains(text2);
			}
			return false;
		case Category.Food:
			if (text == "Consumable")
			{
				if (!(food > 0f))
				{
					return foodStamina > 0f;
				}
				return true;
			}
			return false;
		case Category.Meads:
			if (text == "Consumable" && food <= 0f)
			{
				return foodStamina <= 0f;
			}
			return false;
		case Category.Ammo:
			if (!(text == "Ammo"))
			{
				return text == "AmmoNonEquipable";
			}
			return true;
		case Category.Trophies:
			return text == "Trophy";
		case Category.Weapons:
			switch (text)
			{
			default:
				return text == "Attach_Atgeir";
			case "OneHandedWeapon":
			case "TwoHandedWeapon":
			case "TwoHandedWeaponLeft":
			case "Bow":
				return true;
			}
		case Category.Armor:
			switch (text)
			{
			default:
				return text == "Trinket";
			case "Helmet":
			case "Chest":
			case "Legs":
			case "Hands":
			case "Shoulder":
			case "Utility":
			case "Shield":
				return true;
			}
		case Category.Tools:
			if (!(text == "Tool"))
			{
				return text == "Torch";
			}
			return true;
		case Category.Fish:
			return text == "Fish";
		default:
			return false;
		}
	}

	private static string BaseName(string token)
	{
		string text = (token ?? string.Empty).ToLowerInvariant();
		if (text.StartsWith("$item_", StringComparison.Ordinal))
		{
			text = text.Substring(6);
		}
		if (text.EndsWith("_old", StringComparison.Ordinal))
		{
			text = text.Substring(0, text.Length - 4);
		}
		return text;
	}
}
