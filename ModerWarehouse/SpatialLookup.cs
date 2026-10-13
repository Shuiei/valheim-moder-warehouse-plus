using System;
using System.Collections.Generic;

namespace ModerWarehouse;

internal sealed class SpatialLookup<T> where T : class
{
	private sealed class Entry
	{
		internal T Item;

		internal float X;

		internal float Y;

		internal float Z;

		internal int Order;
	}

	private const float CellSize = 16f;

	private readonly Dictionary<Tuple<int, int, int>, List<Entry>> cells = new Dictionary<Tuple<int, int, int>, List<Entry>>();

	internal SpatialLookup(IEnumerable<T> items, Func<T, float> x, Func<T, float> y, Func<T, float> z)
	{
		int num = 0;
		foreach (T item in items)
		{
			Entry entry = new Entry
			{
				Item = item,
				X = x(item),
				Y = y(item),
				Z = z(item),
				Order = num++
			};
			Tuple<int, int, int> key = Tuple.Create(Cell(entry.X), Cell(entry.Y), Cell(entry.Z));
			if (!cells.TryGetValue(key, out var value))
			{
				List<Entry> list = (cells[key] = new List<Entry>());
				value = list;
			}
			value.Add(entry);
		}
	}

	private static int Cell(float value)
	{
		return (int)Math.Floor(value / 16f);
	}

	internal T Nearest(float x, float y, float z, float radiusSquared, Func<T, bool> include = null, bool preferLastOnTie = false)
	{
		if (radiusSquared < 0f || float.IsNaN(radiusSquared))
		{
			return null;
		}
		if (radiusSquared > 4096f)
		{
			Entry entry = null;
			float num = radiusSquared;
			foreach (List<Entry> value2 in cells.Values)
			{
				foreach (Entry item in value2)
				{
					float num2 = item.X - x;
					float num3 = item.Y - y;
					float num4 = item.Z - z;
					float num5 = num2 * num2 + num3 * num3 + num4 * num4;
					if (!(num5 > num) && (num5 != num || entry == null || !(preferLastOnTie ? (item.Order < entry.Order) : (item.Order > entry.Order))) && (include == null || include(item.Item)))
					{
						entry = item;
						num = num5;
					}
				}
			}
			if (entry == null)
			{
				return null;
			}
			return entry.Item;
		}
		float num6 = (float)Math.Sqrt(radiusSquared);
		Entry entry2 = null;
		float num7 = radiusSquared;
		for (int i = Cell(x - num6); i <= Cell(x + num6); i++)
		{
			for (int j = Cell(y - num6); j <= Cell(y + num6); j++)
			{
				for (int k = Cell(z - num6); k <= Cell(z + num6); k++)
				{
					if (!cells.TryGetValue(Tuple.Create(i, j, k), out var value))
					{
						continue;
					}
					foreach (Entry item2 in value)
					{
						float num8 = item2.X - x;
						float num9 = item2.Y - y;
						float num10 = item2.Z - z;
						float num11 = num8 * num8 + num9 * num9 + num10 * num10;
						if (!(num11 > num7) && (num11 != num7 || entry2 == null || !(preferLastOnTie ? (item2.Order < entry2.Order) : (item2.Order > entry2.Order))) && (include == null || include(item2.Item)))
						{
							entry2 = item2;
							num7 = num11;
						}
					}
				}
			}
		}
		if (entry2 == null)
		{
			return null;
		}
		return entry2.Item;
	}
}
