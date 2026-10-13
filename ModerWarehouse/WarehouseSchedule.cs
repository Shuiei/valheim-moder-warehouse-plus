using System;

namespace ModerWarehouse;

internal static class WarehouseSchedule
{
	internal static bool CanRun(DateTime now, DateTime next, DateTime completed, bool force, bool woke, bool dirty)
	{
		if (!(force || woke) && !(now >= next))
		{
			if (dirty)
			{
				return (now - completed).TotalSeconds >= 1.0;
			}
			return false;
		}
		return true;
	}
}
