using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ModerWarehouse;

internal sealed class WarehouseStatsHistory
{
	internal sealed class Day
	{
		public int Key;

		public int Input;

		public int Output;

		public int Stock;

		public int Occupied;

		public int Capacity;

		public readonly Dictionary<string, int> Inputs = new Dictionary<string, int>();

		public readonly Dictionary<string, int> Outputs = new Dictionary<string, int>();
	}

	internal const int TotalsDays = 365;

	internal const int DetailDays = 90;

	private const string StartPrefix = "S|";

	private static DateTime UtcNow() => WarehouseClock.UtcNow;

	private readonly SortedDictionary<int, Day> _days = new SortedDictionary<int, Day>();

	private int _startKey;

	public readonly Dictionary<string, int> CurrentStock = new Dictionary<string, int>();

	public bool Changed { get; private set; }

	public int TrackedDays
	{
		get
		{
			if (_startKey != 0)
			{
				return Math.Max(1, (UtcNow().Date - ParseKey(_startKey)).Days + 1);
			}
			return 0;
		}
	}

	public int StartDayKey
	{
		get
		{
			if (_startKey != 0)
			{
				return _startKey;
			}
			return Key(UtcNow().Date);
		}
	}

	public static WarehouseStatsHistory Parse(string value)
	{
		WarehouseStatsHistory warehouseStatsHistory = new WarehouseStatsHistory();
		string[] array = (value ?? string.Empty).Split(new char[1] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
		foreach (string text in array)
		{
			if (text.StartsWith("S|", StringComparison.Ordinal))
			{
				if (int.TryParse(text.Substring("S|".Length), NumberStyles.None, CultureInfo.InvariantCulture, out var result) && IsValidKey(result))
				{
					warehouseStatsHistory._startKey = result;
				}
				continue;
			}
			string[] array2 = text.Split(new char[1] { '|' });
			if (array2.Length == 8 && int.TryParse(array2[0], out var result2) && IsValidKey(result2))
			{
				Day day = new Day
				{
					Key = result2
				};
				int.TryParse(array2[1], out day.Input);
				int.TryParse(array2[2], out day.Output);
				int.TryParse(array2[3], out day.Stock);
				int.TryParse(array2[4], out day.Occupied);
				int.TryParse(array2[5], out day.Capacity);
				ParseMap(array2[6], day.Inputs);
				ParseMap(array2[7], day.Outputs);
				warehouseStatsHistory._days[result2] = day;
			}
		}
		if (warehouseStatsHistory._startKey == 0 && warehouseStatsHistory._days.Count > 0)
		{
			warehouseStatsHistory._startKey = warehouseStatsHistory._days.Keys.First();
			warehouseStatsHistory.Changed = true;
		}
		warehouseStatsHistory.Prune();
		return warehouseStatsHistory;
	}

	public void AddInput(string token, int amount)
	{
		Add(token, amount, input: true);
	}

	public void AddOutput(string token, int amount)
	{
		Add(token, amount, input: false);
	}

	private void Add(string token, int amount, bool input)
	{
		if (!string.IsNullOrWhiteSpace(token) && amount > 0)
		{
			Day day = Today();
			Dictionary<string, int> dictionary = (input ? day.Inputs : day.Outputs);
			dictionary[token] = (dictionary.TryGetValue(token, out var value) ? (value + amount) : amount);
			if (input)
			{
				day.Input += amount;
			}
			else
			{
				day.Output += amount;
			}
			Changed = true;
		}
	}

	public void SetSnapshot(Dictionary<string, int> stockByToken, int occupied, int capacity)
	{
		CurrentStock.Clear();
		if (stockByToken != null)
		{
			foreach (KeyValuePair<string, int> item in stockByToken)
			{
				if (item.Value > 0)
				{
					CurrentStock[item.Key] = item.Value;
				}
			}
		}
		Day day = Today();
		int num = CurrentStock.Values.Sum();
		if (day.Stock != num || day.Occupied != occupied || day.Capacity != capacity)
		{
			day.Stock = num;
			day.Occupied = occupied;
			day.Capacity = capacity;
			Changed = true;
		}
	}

	public List<Day> Window(int days)
	{
		int minimum = Key(UtcNow().Date.AddDays(-(Clamp(days) - 1)));
		return _days.Values.Where((Day x) => x.Key >= minimum).ToList();
	}

	public List<Day> PreviousWindow(int days)
	{
		DateTime date = UtcNow().Date;
		int minimum = Key(date.AddDays(-(Clamp(days) * 2 - 1)));
		int maximum = Key(date.AddDays(-Clamp(days)));
		return _days.Values.Where((Day x) => x.Key >= minimum && x.Key <= maximum).ToList();
	}

	public Dictionary<string, int> SumItems(int days, bool input)
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		foreach (Day item in Window(Math.Min(Clamp(days), 90)))
		{
			foreach (KeyValuePair<string, int> item2 in input ? item.Inputs : item.Outputs)
			{
				dictionary[item2.Key] = (dictionary.TryGetValue(item2.Key, out var value) ? (value + item2.Value) : item2.Value);
			}
		}
		return dictionary;
	}

	public bool TryCapacityForecast(int days, out double chestsPerDay, out double daysToFull, int minimumSpan = 7)
	{
		chestsPerDay = 0.0;
		daysToFull = double.PositiveInfinity;
		List<Day> list = (from day3 in Window(days)
			where day3.Capacity > 0
			select day3).ToList();
		if (list.Count < 2)
		{
			return false;
		}
		Day day = list[0];
		Day day2 = list[list.Count - 1];
		int days2 = (ParseKey(day2.Key) - ParseKey(day.Key)).Days;
		if (days2 < Math.Max(1, minimumSpan))
		{
			return false;
		}
		chestsPerDay = (double)(day2.Occupied - day.Occupied) / (double)days2;
		int num = day2.Capacity - day2.Occupied;
		if (num <= 0)
		{
			daysToFull = 0.0;
		}
		else if (chestsPerDay > 0.0)
		{
			daysToFull = (double)num / chestsPerDay;
		}
		return true;
	}

	public string ToCsv(Func<string, string> itemName)
	{
		StringBuilder stringBuilder = new StringBuilder("date;input;output;stock;occupied_chests;chests;top_input;top_output\n");
		foreach (Day value in _days.Values)
		{
			stringBuilder.Append(ParseKey(value.Key).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(';').Append(value.Input)
				.Append(';')
				.Append(value.Output)
				.Append(';')
				.Append(value.Stock)
				.Append(';')
				.Append(value.Occupied)
				.Append(';')
				.Append(value.Capacity)
				.Append(';')
				.Append(Top(value.Inputs, itemName))
				.Append(';')
				.Append(Top(value.Outputs, itemName))
				.Append('\n');
		}
		return stringBuilder.ToString();
	}

	private static string Top(Dictionary<string, int> map, Func<string, string> itemName)
	{
		if (map.Count == 0)
		{
			return string.Empty;
		}
		KeyValuePair<string, int> keyValuePair = map.OrderByDescending((KeyValuePair<string, int> x) => x.Value).ThenBy<KeyValuePair<string, int>, string>((KeyValuePair<string, int> x) => x.Key, StringComparer.Ordinal).First();
		return (itemName?.Invoke(keyValuePair.Key) ?? keyValuePair.Key).Replace(";", ",") + " " + keyValuePair.Value.ToString(CultureInfo.InvariantCulture);
	}

	public string Serialize()
	{
		Prune();
		Changed = false;
		IEnumerable<string> enumerable = _days.Values.Select((Day d) => string.Join("|", d.Key, d.Input, d.Output, d.Stock, d.Occupied, d.Capacity, FormatMap(d.Inputs), FormatMap(d.Outputs)));
		if (_startKey != 0)
		{
			return string.Join("\n", new string[1] { "S|" + _startKey.ToString(CultureInfo.InvariantCulture) }.Concat(enumerable));
		}
		return string.Join("\n", enumerable);
	}

	private Day Today()
	{
		Prune();
		int num = Key(UtcNow().Date);
		if (_startKey == 0)
		{
			_startKey = num;
			Changed = true;
		}
		if (!_days.TryGetValue(num, out var value))
		{
			value = new Day
			{
				Key = num
			};
			_days[num] = value;
			Changed = true;
		}
		return value;
	}

	private void Prune()
	{
		DateTime date = UtcNow().Date;
		int totalsMinimum = Key(date.AddDays(-364.0));
		int detailMinimum = Key(date.AddDays(-89.0));
		foreach (int item in _days.Keys.Where((int x) => x < totalsMinimum).ToList())
		{
			_days.Remove(item);
			Changed = true;
		}
		foreach (Day item2 in _days.Values.Where((Day x) => x.Key < detailMinimum && (x.Inputs.Count > 0 || x.Outputs.Count > 0)))
		{
			item2.Inputs.Clear();
			item2.Outputs.Clear();
			Changed = true;
		}
	}

	internal static int Clamp(int days)
	{
		return Math.Max(1, Math.Min(365, days));
	}

	internal static int Key(DateTime date)
	{
		return date.Year * 10000 + date.Month * 100 + date.Day;
	}

	internal static DateTime ParseKey(int key)
	{
		return new DateTime(key / 10000, key / 100 % 100, key % 100);
	}

	private static bool IsValidKey(int key)
	{
		int num = key / 10000;
		int num2 = key / 100 % 100;
		int num3 = key % 100;
		if (num >= 2000 && num <= 9999 && num2 >= 1 && num2 <= 12 && num3 >= 1)
		{
			return num3 <= DateTime.DaysInMonth(num, num2);
		}
		return false;
	}

	private static string FormatMap(Dictionary<string, int> map)
	{
		return string.Join(",", from x in map.OrderBy<KeyValuePair<string, int>, string>((KeyValuePair<string, int> x) => x.Key, StringComparer.Ordinal)
			select Uri.EscapeDataString(x.Key) + "=" + x.Value.ToString(CultureInfo.InvariantCulture));
	}

	private static void ParseMap(string value, Dictionary<string, int> map)
	{
		string[] array = (value ?? string.Empty).Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
		foreach (string text in array)
		{
			int num = text.LastIndexOf('=');
			if (num > 0 && int.TryParse(text.Substring(num + 1), out var result))
			{
				map[Uri.UnescapeDataString(text.Substring(0, num))] = result;
			}
		}
	}
}
