using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ModerWarehouse;

internal static class WarehouseText
{
	internal sealed class MatchCache
	{
		private readonly Regex _regex;

		private readonly Dictionary<string, Match> _cache = new Dictionary<string, Match>(StringComparer.Ordinal);

		internal MatchCache(Regex regex)
		{
			_regex = regex;
		}

		internal Match Match(string value)
		{
			value = value ?? string.Empty;
			if (_cache.TryGetValue(value, out var value2))
			{
				return value2;
			}
			Match match = _regex.Match(value);
			if (_cache.Count >= 16384)
			{
				_cache.Clear();
			}
			_cache[value] = match;
			return match;
		}

		internal bool IsMatch(string value)
		{
			return Match(value).Success;
		}
	}

	private static readonly Regex RichTextTag = new Regex("<.*?>", RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private const int NormalizeCacheLimit = 16384;

	private static readonly Dictionary<string, string> NormalizeCache = new Dictionary<string, string>(StringComparer.Ordinal);

	private static readonly Dictionary<string, string> PlainCache = new Dictionary<string, string>(StringComparer.Ordinal);

	internal const string LabelSeparator = "\u001f";

	private static readonly char[] LabelSeparatorChars = new char[1] { '\u001f' };

	internal static string PlainText(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return string.Empty;
		}
		if (value.IndexOf('<') < 0)
		{
			return value;
		}
		if (PlainCache.TryGetValue(value, out var value2))
		{
			return value2;
		}
		string text = RichTextTag.Replace(value, string.Empty);
		if (PlainCache.Count >= 16384)
		{
			PlainCache.Clear();
		}
		PlainCache[value] = text;
		return text;
	}

	internal static string Normalize(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return string.Empty;
		}
		if (NormalizeCache.TryGetValue(value, out var value2))
		{
			return value2;
		}
		string text = PlainText(value).ToLowerInvariant();
		StringBuilder stringBuilder = new StringBuilder(text.Length);
		string text2 = text;
		foreach (char c in text2)
		{
			if (char.IsLetterOrDigit(c))
			{
				stringBuilder.Append(c);
			}
		}
		string text3 = stringBuilder.ToString();
		if (NormalizeCache.Count >= 16384)
		{
			NormalizeCache.Clear();
		}
		NormalizeCache[value] = text3;
		return text3;
	}

	internal static IEnumerable<string> GermanAliases(string name)
	{
		if (name == null)
		{
			yield break;
		}
		switch (name.Length)
		{
		default:
			yield break;
		case 6:
			switch (name[0])
			{
			case 'c':
				if (name == "copper")
				{
					yield return "kupfer";
					yield return "kupferbarren";
				}
				break;
			case 's':
				if (name == "silver")
				{
					yield return "silber";
					yield return "silberbarren";
				}
				break;
			case 'b':
				if (name == "bronze")
				{
					yield return "bronze";
					yield return "bronzebarren";
				}
				break;
			}
			yield break;
		case 9:
			switch (name[1])
			{
			case 'o':
				if (name == "copperore")
				{
					yield return "kupfererz";
				}
				break;
			case 'r':
				if (!(name == "ironscrap"))
				{
					break;
				}
				goto IL_0484;
			case 'c':
				if (!(name == "scrapiron"))
				{
					break;
				}
				goto IL_0484;
			case 'i':
				{
					if (name == "silverore")
					{
						yield return "silbererz";
					}
					break;
				}
				IL_0484:
				yield return "altmetall";
				yield return "alteisen";
				break;
			}
			yield break;
		case 4:
			switch (name[0])
			{
			case 'i':
				if (name == "iron")
				{
					yield return "eisen";
					yield return "eisenbarren";
				}
				break;
			case 'w':
				if (name == "wood")
				{
					yield return "holz";
				}
				break;
			case 'c':
				if (name == "coal")
				{
					yield return "kohle";
				}
				break;
			}
			yield break;
		case 7:
			switch (name[0])
			{
			default:
				yield break;
			case 'i':
				if (name == "ironore")
				{
					yield return "eisenerz";
				}
				yield break;
			case 't':
				if (name == "thistle")
				{
					yield return "distel";
				}
				yield break;
			case 'f':
				break;
			}
			if (!(name == "feather"))
			{
				yield break;
			}
			break;
		case 8:
		{
			char c = name[2];
			if ((uint)c <= 110u)
			{
				switch (c)
				{
				default:
					yield break;
				case 'n':
					if (name == "finewood")
					{
						yield return "edelholz";
					}
					yield break;
				case 'a':
					break;
				}
				if (!(name == "feathers"))
				{
					yield break;
				}
				break;
			}
			switch (c)
			{
			default:
				yield break;
			case 'u':
				if (!(name == "roundlog"))
				{
					yield break;
				}
				break;
			case 'r':
				if (!(name == "corewood"))
				{
					yield break;
				}
				break;
			}
			yield return "kernholz";
			yield break;
		}
		case 5:
			switch (name[0])
			{
			case 's':
				if (name == "stone")
				{
					yield return "stein";
				}
				break;
			case 'r':
				if (name == "resin")
				{
					yield return "harz";
				}
				break;
			}
			yield break;
		case 3:
			if (name == "tin")
			{
				yield return "zinn";
				yield return "zinnbarren";
			}
			yield break;
		case 10:
			if (name == "blackmetal")
			{
				yield return "schwarzmetall";
				yield return "schwarzmetallbarren";
			}
			yield break;
		case 11:
			if (name == "ancientbark")
			{
				yield return "uralteborke";
			}
			yield break;
		}
		yield return "feder";
		yield return "federn";
	}

	internal static bool LabelNamesItem(string label, string[] aliases, Func<string, bool> isAmbiguous, out bool needsSample)
	{
		needsSample = false;
		if (string.IsNullOrEmpty(label) || aliases == null)
		{
			return false;
		}
		string[] array = label.Split(LabelSeparatorChars);
		for (int i = 0; i < array.Length; i++)
		{
			string text = Normalize(array[i]);
			if (text.Length == 0 || Array.IndexOf(aliases, text) < 0)
			{
				return false;
			}
			if (isAmbiguous != null && isAmbiguous(text))
			{
				needsSample = true;
			}
		}
		return true;
	}

	internal static string[] BuildAliases(string prefabName, string token, string englishNormalized, string germanNormalized)
	{
		List<string> aliases = new List<string>(8);
		string text = Normalize(prefabName);
		string text2 = Normalize(token?.Replace("$item_", string.Empty));
		Add(text);
		Add(text2);
		Add(englishNormalized);
		Add(germanNormalized);
		foreach (string item in GermanAliases(text))
		{
			Add(item);
		}
		foreach (string item2 in GermanAliases(text2))
		{
			Add(item2);
		}
		return aliases.ToArray();
		void Add(string alias)
		{
			if (!string.IsNullOrEmpty(alias) && !aliases.Contains(alias))
			{
				aliases.Add(alias);
			}
		}
	}
}
