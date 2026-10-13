using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ServersideQoL;

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

	private readonly SortedDictionary<int, Day> _days = new SortedDictionary<int, Day>();

	public readonly Dictionary<string, int> CurrentStock = new Dictionary<string, int>();

	public bool Changed { get; private set; }

	public int TrackedDays
	{
		get
		{
			if (_days.Count != 0)
			{
				return Math.Max(1, (DateTime.UtcNow.Date - ParseKey(_days.Keys.First())).Days + 1);
			}
			return 0;
		}
	}

	public int FirstDayKey
	{
		get
		{
			if (_days.Count != 0)
			{
				return _days.Keys.First();
			}
			return Key(DateTime.UtcNow.Date);
		}
	}

	public static WarehouseStatsHistory Parse(string value)
	{
		WarehouseStatsHistory warehouseStatsHistory = new WarehouseStatsHistory();
		string[] array = (value ?? string.Empty).Split(new char[1] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split(new char[1] { '|' });
			if (array2.Length == 8 && int.TryParse(array2[0], out var result))
			{
				Day day = new Day
				{
					Key = result
				};
				int.TryParse(array2[1], out day.Input);
				int.TryParse(array2[2], out day.Output);
				int.TryParse(array2[3], out day.Stock);
				int.TryParse(array2[4], out day.Occupied);
				int.TryParse(array2[5], out day.Capacity);
				ParseMap(array2[6], day.Inputs);
				ParseMap(array2[7], day.Outputs);
				warehouseStatsHistory._days[result] = day;
			}
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

	public void SetSnapshot(List<ServersideQoLZDO> stores, Func<ServersideQoLZDO, IEnumerable<ItemData>> inventory)
	{
		CurrentStock.Clear();
		int num = 0;
		foreach (ServersideQoLZDO store in stores)
		{
			IEnumerable<ItemData> enumerable = inventory(store);
			if (enumerable == null)
			{
				continue;
			}
			bool flag = false;
			foreach (ItemData item in enumerable.Where((ItemData x) => x != null && x.m_stack > 0 && x.m_shared != null))
			{
				flag = true;
				string name = item.m_shared.m_name;
				CurrentStock[name] = (CurrentStock.TryGetValue(name, out var value) ? (value + item.m_stack) : item.m_stack);
			}
			if (flag)
			{
				num++;
			}
		}
		Day day = Today();
		int num2 = CurrentStock.Values.Sum();
		if (day.Stock != num2 || day.Occupied != num || day.Capacity != stores.Count)
		{
			day.Stock = num2;
			day.Occupied = num;
			day.Capacity = stores.Count;
			Changed = true;
		}
	}

	public List<Day> Window(int days)
	{
		int minimum = Key(DateTime.UtcNow.Date.AddDays(-(Math.Max(1, days) - 1)));
		return _days.Values.Where((Day x) => x.Key >= minimum).ToList();
	}

	public List<Day> PreviousWindow(int days)
	{
		DateTime date = DateTime.UtcNow.Date;
		int minimum = Key(date.AddDays(-(Math.Max(1, days) * 2 - 1)));
		int maximum = Key(date.AddDays(-Math.Max(1, days)));
		return _days.Values.Where((Day x) => x.Key >= minimum && x.Key <= maximum).ToList();
	}

	public Dictionary<string, int> SumItems(int days, bool input)
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		foreach (Day item in Window(days))
		{
			foreach (KeyValuePair<string, int> item2 in input ? item.Inputs : item.Outputs)
			{
				dictionary[item2.Key] = (dictionary.TryGetValue(item2.Key, out var value) ? (value + item2.Value) : item2.Value);
			}
		}
		return dictionary;
	}

	public string Serialize()
	{
		Prune();
		Changed = false;
		return string.Join("\n", _days.Values.Select((Day d) => string.Join("|", d.Key, d.Input, d.Output, d.Stock, d.Occupied, d.Capacity, FormatMap(d.Inputs), FormatMap(d.Outputs))));
	}

	private Day Today()
	{
		Prune();
		int key = Key(DateTime.UtcNow.Date);
		if (!_days.TryGetValue(key, out var value))
		{
			value = new Day
			{
				Key = key
			};
			_days[key] = value;
			Changed = true;
		}
		return value;
	}

	private void Prune()
	{
		int minimum = Key(DateTime.UtcNow.Date.AddDays(-89.0));
		foreach (int item in _days.Keys.Where((int x) => x < minimum).ToList())
		{
			_days.Remove(item);
			Changed = true;
		}
	}

	private static int Key(DateTime date)
	{
		return date.Year * 10000 + date.Month * 100 + date.Day;
	}

	private static DateTime ParseKey(int key)
	{
		return new DateTime(key / 10000, key / 100 % 100, key % 100);
	}

	private static string FormatMap(Dictionary<string, int> map)
	{
		return string.Join(",", map.OrderBy(delegate(KeyValuePair<string, int> x)
		{
			KeyValuePair<string, int> keyValuePair = x;
			return keyValuePair.Key;
		}).Select(delegate(KeyValuePair<string, int> x)
		{
			KeyValuePair<string, int> keyValuePair = x;
			string text = Uri.EscapeDataString(keyValuePair.Key);
			keyValuePair = x;
			return text + "=" + keyValuePair.Value.ToString(CultureInfo.InvariantCulture);
		}));
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
