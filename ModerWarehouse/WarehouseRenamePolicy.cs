using System;

namespace ModerWarehouse;

public static class WarehouseRenamePolicy
{
	public static bool TryResolvePreviousId(string rememberedId, string currentId, out string previousId)
	{
		previousId = string.Empty;
		if (string.IsNullOrWhiteSpace(currentId))
		{
			return false;
		}
		string value = currentId.Trim();
		if (string.IsNullOrWhiteSpace(rememberedId))
		{
			return false;
		}
		string text = rememberedId.Trim();
		if (text.Equals(value, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		previousId = text;
		return true;
	}

	public static bool CanApplyRename(string previousId, string currentId, bool targetAlreadyExists)
	{
		if (!string.IsNullOrWhiteSpace(previousId) && !string.IsNullOrWhiteSpace(currentId) && !previousId.Trim().Equals(currentId.Trim(), StringComparison.OrdinalIgnoreCase))
		{
			return !targetAlreadyExists;
		}
		return false;
	}

	public static bool ShouldLogRenameConflict(string previousId, bool targetAlreadyExists)
	{
		return !string.IsNullOrWhiteSpace(previousId) && targetAlreadyExists;
	}
}
