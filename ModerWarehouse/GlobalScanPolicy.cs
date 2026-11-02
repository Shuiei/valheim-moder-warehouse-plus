using System;

namespace ModerWarehouse;

internal static class GlobalScanPolicy
{
	internal const double TopologySafetyMinutes = 30.0;

	internal const double WorldStockCooldownSeconds = 10.0;

	internal static bool ShouldValidateTopology(bool unknown, bool signatureChanged, DateTime now, DateTime lastValidation)
	{
		if (!(unknown || signatureChanged))
		{
			return (now - lastValidation).TotalMinutes >= 30.0;
		}
		return true;
	}

	internal static bool ShouldRefreshWorldStock(DateTime now, DateTime lastRefresh)
	{
		return (now - lastRefresh).TotalSeconds >= 10.0;
	}
}
