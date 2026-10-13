using System;

namespace ModerWarehouse;

internal static class RequestDisplayPolicy
{
	internal const float MinimumResultSeconds = 2f;

	internal const float MaximumResultSeconds = 8f;

	internal static float ClampResultSeconds(float configuredSeconds)
	{
		if (float.IsNaN(configuredSeconds) || float.IsInfinity(configuredSeconds))
		{
			return 8f;
		}
		return Math.Max(2f, Math.Min(8f, configuredSeconds));
	}
}
