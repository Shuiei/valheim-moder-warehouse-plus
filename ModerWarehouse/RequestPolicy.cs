using System;
using System.Collections.Generic;

namespace ModerWarehouse;

internal static class RequestPolicy
{
	internal static bool TryStripKeyword(string text, IEnumerable<string> keywords, out string rest)
	{
		rest = string.Empty;
		string text2 = (text ?? string.Empty).TrimStart(Array.Empty<char>());
		if (text2.Length == 0 || keywords == null)
		{
			return false;
		}
		foreach (string keyword in keywords)
		{
			string text3 = (keyword ?? string.Empty).Trim();
			if (text3.Length != 0 && text2.Length > text3.Length && text2.StartsWith(text3, StringComparison.OrdinalIgnoreCase) && char.IsWhiteSpace(text2[text3.Length]))
			{
				rest = text2.Substring(text3.Length).Trim();
				return true;
			}
		}
		return false;
	}

	internal static int MatchRank(bool localizedName, bool prefabOrToken)
	{
		if (!localizedName)
		{
			if (!prefabOrToken)
			{
				return 2;
			}
			return 1;
		}
		return 0;
	}

	internal static string ChooseToken(IEnumerable<(string Token, int Rank, int Amount)> candidates)
	{
		string text = null;
		int num = int.MaxValue;
		int num2 = -1;
		foreach (var (text2, num3, num4) in candidates)
		{
			if (!string.IsNullOrEmpty(text2) && (num3 < num || (num3 == num && num4 > num2) || (num3 == num && num4 == num2 && string.CompareOrdinal(text2, text) < 0)))
			{
				text = text2;
				num = num3;
				num2 = num4;
			}
		}
		return text;
	}
}
