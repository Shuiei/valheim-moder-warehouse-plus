using System;

namespace ModerWarehouse;

// The warehouse's clock: the time waits and statistics are measured with (help signs, automatic
// labels, request results, stale IN locks, daily history). It is the real time on a server; the
// tests set their own to step through those waits.
internal static class WarehouseClock
{
	internal static Func<DateTime> Source = () => DateTime.UtcNow;

	internal static DateTime UtcNow => Source();
}
