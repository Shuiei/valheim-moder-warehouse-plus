using System;

namespace ModerWarehouse;

public static class WarehouseContainerPolicy
{
	public static bool IsPrivatePrefab(string prefabName)
	{
		string text = (prefabName ?? string.Empty).ToLowerInvariant();
		if (!text.Contains("chest_private"))
		{
			return text.Contains("personal");
		}
		return true;
	}

	public static bool CanManage(bool endpoint, bool ignored, bool isPrivate, bool inUse, bool hasStorageSign)
	{
		return !endpoint && !ignored && !isPrivate && !inUse && hasStorageSign;
	}

	public static bool IsExplicitLabelReset(string plainText)
	{
		return string.Equals((plainText ?? string.Empty).Trim(), "...", StringComparison.Ordinal);
	}

	public static bool TryParseNumberedLabel(string plainText, out string itemLabel, out int priority)
	{
		string text = (plainText ?? string.Empty).Trim();
		int num = text.Length;
		while (num > 0 && char.IsDigit(text[num - 1]))
		{
			num--;
		}
		itemLabel = text.Substring(0, num).TrimEnd(Array.Empty<char>());
		priority = 0;
		if (num == text.Length || itemLabel.Length == 0 || !int.TryParse(text.Substring(num), out priority))
		{
			return false;
		}
		if (priority >= 1)
		{
			return priority <= 99;
		}
		return false;
	}

	public static int EffectiveFillPriority(int storedPriority)
	{
		if (storedPriority < 1 || storedPriority > 99)
		{
			return 1;
		}
		return storedPriority;
	}

	public static int TransferAmount(int sourceStack, int wanted, int maxStack, int matchingStackSpace, int emptySlots)
	{
		int num = Math.Max(1, maxStack);
		int val = Math.Max(0, matchingStackSpace) + Math.Max(0, emptySlots) * num;
		return Math.Min(Math.Min(Math.Max(0, sourceStack), Math.Max(0, wanted)), val);
	}
}
