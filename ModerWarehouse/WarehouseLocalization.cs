using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using YamlDotNet.Serialization;

namespace ModerWarehouse;

internal static class WarehouseLocalization
{
	public sealed class TranslationFile
	{
		public string language { get; set; }

		public string language_name { get; set; }

		public string translator { get; set; }

		public Dictionary<string, string> strings { get; set; }
	}

	private static readonly Dictionary<string, Dictionary<string, string>> Languages = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

	private static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	private static Action<string> _log;

	public static void Initialize(Action<string> log)
	{
		_log = log;
		Languages.Clear();
		Aliases.Clear();
		AddBuiltIn("en", English());
		AddBuiltIn("de", German());
		Aliases["english"] = "en";
		Aliases["eng"] = "en";
		Aliases["german"] = "de";
		Aliases["deutsch"] = "de";
		Aliases["ger"] = "de";
		string text = Path.Combine(Paths.ConfigPath, "ModerWarehouse", "localization");
		Directory.CreateDirectory(text);
		WriteTemplate(Path.Combine(text, "TranslationTemplate.json"));
		foreach (string item in Directory.GetFiles(text, "*.json").OrderBy<string, string>((string x) => x, StringComparer.OrdinalIgnoreCase))
		{
			if (!Path.GetFileName(item).Equals("TranslationTemplate.json", StringComparison.OrdinalIgnoreCase))
			{
				LoadFile(item);
			}
		}
		_log?.Invoke($"Moder Warehouse localization: {Languages.Count} language(s) available: " + string.Join(", ", Languages.Keys.OrderBy((string x) => x)) + ".");
	}

	public static string Resolve(string requested)
	{
		string text = (requested ?? string.Empty).Trim();
		if (Aliases.TryGetValue(text, out var value))
		{
			text = value;
		}
		text = text.Replace('_', '-').ToLowerInvariant();
		if (Languages.ContainsKey(text))
		{
			return text;
		}
		string text2 = text.Split(new char[1] { '-' })[0];
		if (Languages.ContainsKey(text2))
		{
			return text2;
		}
		return "en";
	}

	public static string Text(string language, string key)
	{
		language = Resolve(language);
		if (Languages.TryGetValue(language, out var value) && value.TryGetValue(key, out var value2) && !string.IsNullOrWhiteSpace(value2))
		{
			return value2;
		}
		if (Languages["en"].TryGetValue(key, out value2))
		{
			return value2;
		}
		return "[" + key + "]";
	}

	public static string Format(string language, string key, params object[] args)
	{
		try
		{
			return string.Format(CultureInfo.InvariantCulture, Text(language, key), args);
		}
		catch (FormatException)
		{
			string value;
			return Languages["en"].TryGetValue(key, out value) ? string.Format(CultureInfo.InvariantCulture, value, args) : ("[" + key + "]");
		}
	}

	public static bool StartsWithAnyTranslation(string text, params string[] keys)
	{
		string text2 = (text ?? string.Empty).TrimStart(Array.Empty<char>());
		if (text2.Length == 0 || keys == null || keys.Length == 0)
		{
			return false;
		}
		foreach (Dictionary<string, string> value2 in Languages.Values)
		{
			foreach (string text3 in keys)
			{
				if (!string.IsNullOrWhiteSpace(text3) && value2.TryGetValue(text3, out var value) && !string.IsNullOrWhiteSpace(value))
				{
					int num = value.IndexOfAny(new char[3] { '{', '\n', '\r' });
					string text4 = ((num >= 0) ? value.Substring(0, num) : value).Trim();
					if (text4.Length != 0 && text2.StartsWith(text4, StringComparison.OrdinalIgnoreCase) && (text2.Length == text4.Length || char.IsWhiteSpace(text2[text4.Length]) || text2[text4.Length] == ':'))
					{
						return true;
					}
				}
			}
		}
		return false;
	}

	private static void AddBuiltIn(string code, Dictionary<string, string> strings)
	{
		Languages[code] = strings;
	}

	private static void LoadFile(string path)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			TranslationFile translationFile = new DeserializerBuilder().IgnoreUnmatchedProperties().Build().Deserialize<TranslationFile>(File.ReadAllText(path));
			string text = (translationFile?.language ?? string.Empty).Trim().Replace('_', '-').ToLowerInvariant();
			if (string.IsNullOrWhiteSpace(text) || translationFile.strings == null || translationFile.strings.Count == 0)
			{
				throw new InvalidDataException("'language' and a non-empty 'strings' object are required");
			}
			if (!TranslationGate.IsValidLanguageCode(text))
			{
				throw new InvalidDataException("'" + text + "' is not a valid language code; use a code such as 'es' or 'sr-Latn'");
			}
			if (string.IsNullOrWhiteSpace(translationFile.language_name))
			{
				throw new InvalidDataException("'language_name' is required (for example 'Español')");
			}
			if (text == "en" || text == "de")
			{
				throw new InvalidDataException("'" + text + "' is built in and cannot be replaced by an external file");
			}
			if (Languages.ContainsKey(text))
			{
				throw new InvalidDataException("language code '" + text + "' is already loaded; keep one file per language code");
			}
			if (!string.IsNullOrWhiteSpace(translationFile.language_name) && Aliases.TryGetValue(translationFile.language_name.Trim(), out var value) && !string.Equals(value, text, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidDataException("language_name '" + translationFile.language_name + "' is already used by '" + value + "'");
			}
			Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (KeyValuePair<string, string> @string in translationFile.strings)
			{
				if (!string.IsNullOrWhiteSpace(@string.Key) && @string.Value != null)
				{
					dictionary[@string.Key] = @string.Value;
				}
			}
			TranslationGate.Report report = TranslationGate.Validate(Languages["en"], dictionary);
			if (!report.IsValid)
			{
				throw new InvalidDataException("placeholder mismatch in key(s): " + string.Join(", ", report.InvalidPlaceholders) + "; preserve every {0}, {1}, etc. exactly as in TranslationTemplate.json");
			}
			foreach (string item in report.Unknown)
			{
				dictionary.Remove(item);
			}
			Languages[text] = dictionary;
			Aliases[text] = text;
			if (!string.IsNullOrWhiteSpace(translationFile.language_name))
			{
				Aliases[translationFile.language_name.Trim()] = text;
			}
			_log?.Invoke(string.Format("Loaded warehouse translation '{0}' ({1}/{2} strings) from {3}.", text, dictionary.Count, Languages["en"].Count, Path.GetFileName(path)) + GateSummary(report));
		}
		catch (Exception ex)
		{
			_log?.Invoke("Ignored invalid warehouse translation '" + Path.GetFileName(path) + "': " + ex.Message);
		}
	}

	private static string GateSummary(TranslationGate.Report report)
	{
		List<string> list = new List<string>();
		if (report.Missing.Count > 0)
		{
			list.Add($" {report.Missing.Count} missing/empty key(s) use English fallback: " + string.Join(", ", report.Missing.Take(8)) + ((report.Missing.Count > 8) ? ", …" : string.Empty) + ".");
		}
		if (report.Unknown.Count > 0)
		{
			list.Add($" Ignored {report.Unknown.Count} unknown key(s): " + string.Join(", ", report.Unknown.Take(8)) + ((report.Unknown.Count > 8) ? ", …" : string.Empty) + ".");
		}
		if (list.Count != 0)
		{
			return string.Join(" ", list);
		}
		return " Translation gate: complete; placeholders valid.";
	}

	private static void WriteTemplate(string path)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		TranslationFile translationFile;
		if (File.Exists(path))
		{
			try
			{
				translationFile = new DeserializerBuilder().IgnoreUnmatchedProperties().Build().Deserialize<TranslationFile>(File.ReadAllText(path));
				if (translationFile?.strings == null)
				{
					_log?.Invoke("TranslationTemplate.json was not updated because its strings object could not be read.");
					return;
				}
				int num = 0;
				foreach (KeyValuePair<string, string> item in English())
				{
					if (!translationFile.strings.ContainsKey(item.Key))
					{
						translationFile.strings[item.Key] = item.Value;
						num++;
					}
				}
				if (num == 0)
				{
					return;
				}
				_log?.Invoke($"Updated TranslationTemplate.json with {num} new key(s); existing entries were preserved.");
			}
			catch (Exception ex)
			{
				_log?.Invoke("TranslationTemplate.json was not updated: " + ex.Message);
				return;
			}
		}
		else
		{
			translationFile = new TranslationFile
			{
				language = "xx",
				language_name = "Your language",
				translator = "Your name",
				strings = English()
			};
		}
		WriteTemplateFile(path, translationFile);
	}

	private static void WriteTemplateFile(string path, TranslationFile file)
	{
		StringBuilder stringBuilder = new StringBuilder("{\n  \"language\": \"").Append(JsonEscape(file.language)).Append("\",\n  \"language_name\": \"").Append(JsonEscape(file.language_name))
			.Append("\",\n  \"translator\": \"")
			.Append(JsonEscape(file.translator))
			.Append("\",\n  \"strings\": {\n");
		List<KeyValuePair<string, string>> list = file.strings.OrderBy((KeyValuePair<string, string> x) => x.Key).ToList();
		for (int num = 0; num < list.Count; num++)
		{
			KeyValuePair<string, string> keyValuePair = list[num];
			stringBuilder.Append("    \"").Append(JsonEscape(keyValuePair.Key)).Append("\": \"")
				.Append(JsonEscape(keyValuePair.Value))
				.Append('"');
			if (num + 1 < list.Count)
			{
				stringBuilder.Append(',');
			}
			stringBuilder.AppendLine();
		}
		stringBuilder.Append("  }\n}\n");
		File.WriteAllText(path, stringBuilder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
	}

	private static string JsonEscape(string value)
	{
		return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r")
			.Replace("\n", "\\n");
	}

	internal static Dictionary<string, string> English()
	{
		return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["help.title"] = "WAREHOUSE HELP",
			["help.in"] = "IN: DEPOSIT ALL",
			["help.req"] = "REQ: WOOD 100",
			["help.out"] = "OUT: COLLECT",
			["help.board"] = "BOARD: E = FILTER",
			["request.title"] = "REQUEST",
			["request.example_item"] = "Wood",
			["request.updating"] = "UPDATING",
			["request.target"] = "TARGET",
			["request.error_example"] = "ERROR: e.g. Tin 20",
			["request.done"] = "DONE {0} x{1}",
			["request.partial"] = "PARTIAL {0} x{1}/{2}",
			["request.output_full"] = "OUTPUT FULL {0}",
			["request.not_found"] = "NOT FOUND {0}",
			["request.wildcard_too_many"] = "TOO MANY MATCHES {0}: {1} (MAX {2})",
			["request.craft_header"] = "CRAFTING {0} x{1}:",
			["request.craft_header_level"] = "CRAFTING {0} LVL {1} x{2}:",
			["request.craft_in_output"] = "IN OUT {0} x{1}",
			["request.craft_unknown"] = "NO RECIPE {0}",
			["request.craft_max_level"] = "MAX LEVEL {0}: {1}",
			["request.error"] = "ERROR: {0}",
			["request.suggestions"] = "AVAILABLE",
			["request.shown_on_board"] = "SEARCH {0}\n{1} HIT(S) ON MAIN BOARD",
			["request.stock_live"] = "LIVE",
			["request.stock_seen"] = "AS OF {0}",
			["request.stock_unknown"] = "UNKNOWN",
			["request.idle"] = "REQ",
			["shopping.title"] = "NEEDED",
			["shopping.fulfilled"] = "TARGETS MET",
			["board.warehouse"] = "WAREHOUSE",
			["board.stock"] = "STOCK",
			["board.page"] = "PAGE",
			["board.chests"] = "CHESTS",
			["board.display_sleeping"] = "DISPLAY\nSLEEPING",
			["board.wake"] = "{0} METRES\nWAKE UP",
			["board.sleep_word"] = "SLEEPMODE",
			["board.expand"] = "EXPAND!",
			["board.deposited"] = "DEPOSITED",
			["board.since_visit"] = "SINCE VISIT",
			["board.rejected"] = "REJECTED AT IN",
			["board.internal_item"] = "INTERNAL / CHEAT ITEM",
			["board.positions"] = "ITEMS",
			["board.no_matches"] = "NO MATCHES",
			["info.free"] = "FREE",
			["info.empty_reserved"] = "EMPTY RESERVED",
			["info.occupied"] = "OCCUPIED",
			["common.auto"] = "AUTO",
			["stats.new"] = "NEW",
			["network.active"] = "NETWORK\nACTIVE",
			["network.local"] = "LOCAL",
			["network.statistics"] = "NETWORK\nSTATISTICS",
			["network.warehouses"] = "WAREHOUSES",
			["network.chests_world"] = "CHESTS WORLD",
			["network.stock_world"] = "STOCK WORLD",
			["network.types_world"] = "ITEM TYPES WORLD",
			["stats.title"] = "STATISTICS",
			["stats.period"] = "PERIOD",
			["stats.today"] = "TODAY",
			["stats.days"] = "{0} DAYS",
			["stats.sleep_word"] = "STATISTICS",
			["stats.input"] = "INPUT",
			["stats.output"] = "OUTPUT",
			["stats.balance"] = "BALANCE",
			["stats.per_day"] = "MOVES / DAY",
			["stats.top_input"] = "TOP INPUT",
			["stats.top_output"] = "TOP OUTPUT",
			["stats.current"] = "CURRENT TOTAL",
			["stats.item_types"] = "ITEM TYPES",
			["stats.chests_now"] = "CHESTS NOW",
			["stats.avg_use"] = "AVG USE",
			["stats.peak"] = "PEAK",
			["stats.low"] = "LOW",
			["stats.rising"] = "RISING",
			["stats.falling"] = "FALLING",
			["stats.empty_first"] = "EMPTY FIRST",
			["stats.ratio"] = "IN / OUT",
			["stats.busy_in"] = "BUSIEST INPUT",
			["stats.busy_out"] = "BUSIEST OUTPUT",
			["stats.tracked_since"] = "TRACKED SINCE",
			["stats.days_tracked"] = "DAYS TRACKED",
			["stats.days_short"] = "DAYS",
			["stats.eta"] = "DAYS LEFT",
			["stats.input_change"] = "INPUT VS 7D",
			["stats.output_change"] = "OUTPUT VS 7D",
			["stats.confidence"] = "DATA BASIS",
			["stats.not_enough"] = "TOO LITTLE DATA",
			["stats.net_day"] = "NET / DAY",
			["stats.date"] = "DATE",
			["stats.amount"] = "AMOUNT",
			["stats.previous"] = "PREVIOUS",
			["stats.control"] = "STAT CONTROL",
			["stats.manual"] = "MANUAL",
			["stats.auto_in"] = "AUTO IN {0}s",
			["stats.page_1"] = "OVERVIEW",
			["stats.page_2"] = "COMPARE",
			["stats.page_3"] = "TREND",
			["stats.page_4"] = "RECORDS"
		};
	}

	internal static Dictionary<string, string> German()
	{
		return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["help.title"] = "LAGERHILFE",
			["help.in"] = "IN: ALLES REIN",
			["help.req"] = "REQ: HOLZ 100",
			["help.out"] = "OUT: ABHOLEN",
			["help.board"] = "TAFEL: E = FILTER",
			["request.title"] = "BESTELLUNG",
			["request.example_item"] = "Holz",
			["request.updating"] = "WIRD AKTUALISIERT",
			["request.target"] = "SOLL",
			["request.error_example"] = "FEHLER: z.B. Zinn 20",
			["request.done"] = "FERTIG {0} x{1}",
			["request.partial"] = "TEILWEISE {0} x{1}/{2}",
			["request.output_full"] = "AUSGABE VOLL {0}",
			["request.not_found"] = "NICHT GEFUNDEN {0}",
			["request.wildcard_too_many"] = "ZU VIELE TREFFER {0}: {1} (MAX. {2})",
			["request.craft_header"] = "HERSTELLUNG {0} x{1}:",
			["request.craft_header_level"] = "HERSTELLUNG {0} STUFE {1} x{2}:",
			["request.craft_in_output"] = "IN AUSGABE {0} x{1}",
			["request.craft_unknown"] = "KEIN REZEPT {0}",
			["request.craft_max_level"] = "MAX. STUFE {0}: {1}",
			["request.error"] = "FEHLER: {0}",
			["request.suggestions"] = "VERFÜGBAR",
			["request.shown_on_board"] = "SUCHE {0}\n{1} TREFFER AM LAGERPANEL",
			["request.stock_live"] = "LIVE",
			["request.stock_seen"] = "STAND {0}",
			["request.stock_unknown"] = "UNBEKANNT",
			["request.idle"] = "FRAGE",
			["shopping.title"] = "BEDARF",
			["shopping.fulfilled"] = "SOLL ERFÜLLT",
			["board.warehouse"] = "LAGER",
			["board.stock"] = "BESTAND",
			["board.page"] = "SEITE",
			["board.chests"] = "KISTEN",
			["board.display_sleeping"] = "ANZEIGE\nSCHLÄFT",
			["board.wake"] = "{0} METER\nAUFWECKEN",
			["board.sleep_word"] = "RUHEMODUS",
			["board.expand"] = "AUSBAUEN!",
			["board.deposited"] = "EINGELAGERT",
			["board.since_visit"] = "SEIT BESUCH",
			["board.rejected"] = "BEI IN ABGEWIESEN",
			["board.internal_item"] = "INTERNES / CHEAT-OBJEKT",
			["board.positions"] = "POSITIONEN",
			["board.no_matches"] = "KEINE TREFFER",
			["info.free"] = "FREI",
			["info.empty_reserved"] = "LEER RESERVIERT",
			["info.occupied"] = "BELEGT",
			["common.auto"] = "AUTO",
			["stats.new"] = "NEU",
			["network.active"] = "LAGERNETZ\nAKTIV",
			["network.local"] = "LOKAL",
			["network.statistics"] = "LAGERNETZ\nSTATISTIK",
			["network.warehouses"] = "LAGER",
			["network.chests_world"] = "KISTEN WELT",
			["network.stock_world"] = "BESTAND WELT",
			["network.types_world"] = "ARTEN WELT",
			["stats.title"] = "STATISTIK",
			["stats.period"] = "ZEITRAUM",
			["stats.today"] = "HEUTE",
			["stats.days"] = "{0} TAGE",
			["stats.sleep_word"] = "ZAHLENRUHE",
			["stats.input"] = "EINGANG",
			["stats.output"] = "AUSGANG",
			["stats.balance"] = "SALDO",
			["stats.per_day"] = "BEWEGT / TAG",
			["stats.top_input"] = "TOP EINGANG",
			["stats.top_output"] = "TOP AUSGANG",
			["stats.current"] = "BESTAND TOTAL",
			["stats.item_types"] = "ARTIKELARTEN",
			["stats.chests_now"] = "KISTEN JETZT",
			["stats.avg_use"] = "Ø AUSLASTUNG",
			["stats.peak"] = "HÖCHSTSTAND",
			["stats.low"] = "TIEFSTSTAND",
			["stats.rising"] = "STEIGEND",
			["stats.falling"] = "FALLEND",
			["stats.empty_first"] = "ZUERST LEER",
			["stats.ratio"] = "EIN / AUS",
			["stats.busy_in"] = "MAX EINGANG",
			["stats.busy_out"] = "MAX AUSGANG",
			["stats.tracked_since"] = "ERFASST SEIT",
			["stats.days_tracked"] = "TAGE ERFASST",
			["stats.days_short"] = "TAGE",
			["stats.eta"] = "NOCH TAGE",
			["stats.input_change"] = "EIN VS 7T",
			["stats.output_change"] = "AUS VS 7T",
			["stats.confidence"] = "DATENBASIS",
			["stats.not_enough"] = "ZU WENIG DATEN",
			["stats.net_day"] = "NETTO / TAG",
			["stats.date"] = "DATUM",
			["stats.amount"] = "MENGE",
			["stats.previous"] = "VORPERIODE",
			["stats.control"] = "STAT-STEUERUNG",
			["stats.manual"] = "MANUELL",
			["stats.auto_in"] = "AUTO IN {0}s",
			["stats.page_1"] = "ÜBERSICHT",
			["stats.page_2"] = "VERGLEICH",
			["stats.page_3"] = "TREND",
			["stats.page_4"] = "REKORDE"
		};
	}
}
