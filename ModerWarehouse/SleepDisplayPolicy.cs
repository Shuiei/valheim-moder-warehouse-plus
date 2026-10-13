using System;

namespace ModerWarehouse;

internal static class SleepDisplayPolicy
{
	internal sealed class Layout
	{
		internal string Text { get; }

		internal int LabelLength { get; }

		internal Layout(string text, int labelLength)
		{
			Text = text;
			LabelLength = labelLength;
		}
	}

	internal static Layout Create(string localizedLabel, int cellCount)
	{
		if (cellCount <= 0)
		{
			return new Layout(string.Empty, 0);
		}
		string text = (localizedLabel ?? string.Empty).Trim().TrimEnd('Z', 'z');
		return new Layout(labelLength: Math.Min(text.Length, cellCount), text: (text + new string('Z', cellCount)).Substring(0, cellCount));
	}
}
