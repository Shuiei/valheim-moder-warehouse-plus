using System;

namespace ModerWarehouse;

public static class WarehouseDuplicatePolicy
{
	public static int Compare(bool leftCanonical, int leftInfrastructure, string leftStableId, bool rightCanonical, int rightInfrastructure, string rightStableId)
	{
		int num = rightCanonical.CompareTo(leftCanonical);
		if (num != 0)
		{
			return num;
		}
		int num2 = rightInfrastructure.CompareTo(leftInfrastructure);
		if (num2 != 0)
		{
			return num2;
		}
		return StringComparer.Ordinal.Compare(leftStableId ?? string.Empty, rightStableId ?? string.Empty);
	}
}
