using System;

namespace ModerWarehouse;

public static class WarehouseDuplicatePolicy
{
	public static int Compare(bool leftCanonical, string leftStableId, bool rightCanonical, string rightStableId)
	{
		int num = rightCanonical.CompareTo(leftCanonical);
		if (num != 0)
		{
			return num;
		}
		return StringComparer.Ordinal.Compare(leftStableId ?? string.Empty, rightStableId ?? string.Empty);
	}
}
