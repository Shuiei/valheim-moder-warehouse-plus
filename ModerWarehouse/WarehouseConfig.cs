using BepInEx.Configuration;
using ServersideQoL;

namespace ModerWarehouse;

public sealed class WarehouseConfig : ConfigBase<WarehouseConfig>
{
	public override ConfigEntry<bool> Enabled { get; }

	public ConfigEntry<string> Language { get; }

	public ConfigEntry<float> Radius { get; }

	public ConfigEntry<float> MinimumRadius { get; }

	public ConfigEntry<float> MinimumWarehouseDistance { get; }

	public ConfigEntry<float> MarkerDistance { get; }

	public ConfigEntry<float> ScanInterval { get; }

	public ConfigEntry<int> MaxMoves { get; }

	public ConfigEntry<float> StaleInputLockSeconds { get; }

	public ConfigEntry<float> StaleInputLockPlayerDistance { get; }

	public ConfigEntry<float> StaleInputLockMaxSeconds { get; }

	public ConfigEntry<bool> AutoCreateLabels { get; }

	public ConfigEntry<float> LabelDistance { get; }

	public ConfigEntry<float> AutoLabelDelay { get; }

	public ConfigEntry<float> RequestResultSeconds { get; }

	public ConfigEntry<float> BoardPageSeconds { get; }

	public ConfigEntry<float> BoardWakeDistance { get; }

	public ConfigEntry<float> DeliveryNoticeSeconds { get; }

	public ConfigEntry<float> VisitSummarySeconds { get; }

	public ConfigEntry<int> CapacityWarningPercent { get; }

	public ConfigEntry<int> CapacityCriticalPercent { get; }

	public WarehouseConfig(ConfigFile file, Logger logger)
		: base(file, logger)
	{
		Enabled = ConfigBase<WarehouseConfig>.BindEx<bool>(file, "Warehouse", true, "Enable the warehouse.", key: "Enabled");
		Language = ConfigBase<WarehouseConfig>.BindEx<string>(file, "Localization", "English", "Default display language. Built in: English, German. External JSON translations may add more.", key: "Language");
		Radius = ConfigBase<WarehouseConfig>.BindEx<float>(file, "Warehouse", 100f, "Storage radius around the input chest.", key: "Radius");
		MinimumRadius = ConfigBase<WarehouseConfig>.BindEx<float>(file, "Warehouse", 10f, "Smallest radius selectable on an IN sign.", key: "MinimumRadius");
		MinimumWarehouseDistance = ConfigBase<WarehouseConfig>.BindEx<float>(file, "Warehouse", 210f, "Minimum distance between warehouse IN chests.", key: "MinimumWarehouseDistance");
		MarkerDistance = ConfigBase<WarehouseConfig>.BindEx<float>(file, "Warehouse", 3f, "Maximum sign-to-chest distance.", key: "MarkerDistance");
		ScanInterval = ConfigBase<WarehouseConfig>.BindEx<float>(file, "Warehouse", 3f, "Seconds between scans.", key: "ScanInterval");
		MaxMoves = ConfigBase<WarehouseConfig>.BindEx<int>(file, "Safety", 250, "Maximum stack moves per scan.", key: "MaxMovesPerCycle");
		StaleInputLockSeconds = ConfigBase<WarehouseConfig>.BindEx<float>(file, "Safety", 30f, "Seconds the IN chest may stay marked in use after its opener left or disconnected before the lock is released. 0 disables.", key: "StaleInputLockSeconds");
		StaleInputLockPlayerDistance = ConfigBase<WarehouseConfig>.BindEx<float>(file, "Safety", 4f, "The player who opened the IN chest keeps its lock while within this distance (m).", key: "StaleInputLockPlayerDistance");
		StaleInputLockMaxSeconds = ConfigBase<WarehouseConfig>.BindEx<float>(file, "Safety", 90f, "Hard limit (s): the IN chest lock is released after this long even if its opener is still nearby.", key: "StaleInputLockMaxSeconds");
		AutoCreateLabels = ConfigBase<WarehouseConfig>.BindEx<bool>(file, "Labels", true, "Create persistent localized white labels for homogeneous storage chests.", key: "AutoCreateLabels");
		LabelDistance = ConfigBase<WarehouseConfig>.BindEx<float>(file, "Labels", 1f, "Maximum distance for ordinary storage labels and blank adoption signs.", key: "LabelDistance");
		AutoLabelDelay = ConfigBase<WarehouseConfig>.BindEx<float>(file, "Labels", 90f, "Seconds an unlabeled homogeneous chest must remain unchanged before a sign is created.", key: "AutoLabelDelay");
		RequestResultSeconds = ConfigBase<WarehouseConfig>.BindEx<float>(file, "Requests", 8f, "Seconds withdrawal results remain visible before the REQ sign returns to its target-stock view (maximum 8 seconds).", key: "ResultDisplaySeconds");
		BoardPageSeconds = ConfigBase<WarehouseConfig>.BindEx<float>(file, "Display", 3f, "Seconds each warehouse board page remains visible.", key: "BoardPageSeconds");
		BoardWakeDistance = ConfigBase<WarehouseConfig>.BindEx<float>(file, "Display", 5f, "Player distance in metres at which the stock board wakes up.", key: "BoardWakeDistance");
		DeliveryNoticeSeconds = ConfigBase<WarehouseConfig>.BindEx<float>(file, "Display", 6f, "Seconds a completed delivery remains visible on the stock board.", key: "DeliveryNoticeSeconds");
		VisitSummarySeconds = ConfigBase<WarehouseConfig>.BindEx<float>(file, "Display", 12f, "Seconds stock changes since the last visit remain visible after waking the board.", key: "VisitSummarySeconds");
		CapacityWarningPercent = ConfigBase<WarehouseConfig>.BindEx<int>(file, "Display", 80, "Occupied chest percentage at which the capacity display becomes orange.", key: "CapacityWarningPercent");
		CapacityCriticalPercent = ConfigBase<WarehouseConfig>.BindEx<int>(file, "Display", 95, "Occupied chest percentage at which the capacity display becomes red and requests expansion.", key: "CapacityCriticalPercent");
	}
}
