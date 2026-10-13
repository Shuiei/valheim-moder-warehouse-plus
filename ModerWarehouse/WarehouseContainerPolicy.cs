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

	public static int TransferAmount(int sourceStack, int wanted, int maxStack, int matchingStackSpace, int emptySlots)
	{
		int num = Math.Max(1, maxStack);
		int val = Math.Max(0, matchingStackSpace) + Math.Max(0, emptySlots) * num;
		return Math.Min(Math.Min(Math.Max(0, sourceStack), Math.Max(0, wanted)), val);
	}
}
