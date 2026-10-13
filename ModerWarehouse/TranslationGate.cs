using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ModerWarehouse;

internal static class TranslationGate
{
	internal sealed class Report
	{
		internal readonly List<string> Missing = new List<string>();

		internal readonly List<string> Unknown = new List<string>();

		internal readonly List<string> InvalidPlaceholders = new List<string>();

		internal bool IsValid => InvalidPlaceholders.Count == 0;
	}

	private static readonly Regex LanguageCode = new Regex("^[a-z]{2,3}(?:-[a-z0-9]{2,8})*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex Placeholder = new Regex("\\{([0-9]+)(?:,[^{}]+)?(?::[^{}]+)?\\}", RegexOptions.CultureInvariant);

	internal static bool IsValidLanguageCode(string value)
	{
		string input = (value ?? string.Empty).Trim().Replace('_', '-');
		return LanguageCode.IsMatch(input);
	}

	internal static Report Validate(IDictionary<string, string> reference, IDictionary<string, string> supplied)
	{
		Report report = new Report();
		if (reference == null || supplied == null)
		{
			return report;
		}
		foreach (KeyValuePair<string, string> item in reference)
		{
			if (!supplied.TryGetValue(item.Key, out var value) || string.IsNullOrWhiteSpace(value))
			{
				report.Missing.Add(item.Key);
			}
			else if (!PlaceholderSet(item.Value).SequenceEqual(PlaceholderSet(value)))
			{
				report.InvalidPlaceholders.Add(item.Key);
			}
		}
		foreach (string key in supplied.Keys)
		{
			if (!reference.ContainsKey(key))
			{
				report.Unknown.Add(key);
			}
		}
		report.Missing.Sort(StringComparer.OrdinalIgnoreCase);
		report.Unknown.Sort(StringComparer.OrdinalIgnoreCase);
		report.InvalidPlaceholders.Sort(StringComparer.OrdinalIgnoreCase);
		return report;
	}

	private static string[] PlaceholderSet(string value)
	{
		return (from Match match in Placeholder.Matches(value ?? string.Empty)
			select match.Groups[1].Value).OrderBy<string, string>((string index) => index, StringComparer.Ordinal).ToArray();
	}
}
