using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using ServersideQoL;
using ServersideQoL.Processors;
using ServersideQoL.Utilities;
using UnityEngine;

namespace ModerWarehouse;

[Processor("1f3823e7-1db0-4857-b8b8-e377bd8cd191")]
[RunAfter<ContainerRegistryProcessor>]
public sealed class WarehouseProcessor : Processor<ProcessorPrefabInfo<Sign>>
{
	private sealed class PersistedContainerSpatialLookup
	{
		private readonly SpatialLookup<ZDO> lookup;

		private readonly HashSet<ZDO> checkedObjects = new HashSet<ZDO>();

		private readonly HashSet<ZDO> containers = new HashSet<ZDO>();

		internal PersistedContainerSpatialLookup(List<ZDO> objects)
		{
			lookup = new SpatialLookup<ZDO>(objects, (ZDO zdo) => zdo.GetPosition().x, (ZDO zdo) => zdo.GetPosition().y, (ZDO zdo) => zdo.GetPosition().z);
		}

		internal ZDO Nearest(ZDO sign, float range2)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			Vector3 position = sign.GetPosition();
			return lookup.Nearest(position.x, position.y, position.z, range2, IsContainer, preferLastOnTie: true);
		}

		private bool IsContainer(ZDO candidate)
		{
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0069: Expected O, but got Unknown
			if (containers.Contains(candidate))
			{
				return true;
			}
			if (checkedObjects.Contains(candidate))
			{
				return false;
			}
			checkedObjects.Add(candidate);
			ZNetScene instance = ZNetScene.instance;
			GameObject val = (((UnityEngine.Object)(object)instance != (UnityEngine.Object)null) ? instance.GetPrefab(candidate.GetPrefab()) : null);
			if ((UnityEngine.Object)(((UnityEngine.Object)(object)val != (UnityEngine.Object)null) ? val.GetComponent<Container>() : null) == (UnityEngine.Object)null)
			{
				return false;
			}
			containers.Add(candidate);
			return true;
		}
	}

	private sealed class Request
	{
		public ServersideQoLZDO Sign { get; }

		public string Payload { get; }

		public Request(ServersideQoLZDO sign, string payload)
		{
			Sign = sign;
			Payload = payload;
		}
	}

	private sealed class RequestSuggestion
	{
		public string Token { get; set; }

		public string Name { get; set; }

		public int Score { get; set; }

		public Dictionary<string, int> Amounts { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

		public Dictionary<string, string> Statuses { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
	}

	private sealed class RequestSearchNotice
	{
		public string Query { get; }

		public List<RequestSuggestion> Matches { get; }

		public DateTime StartedAt { get; }

		public DateTime Until { get; }

		public RequestSearchNotice(string query, List<RequestSuggestion> matches, DateTime startedAt, DateTime until)
		{
			Query = query;
			Matches = matches;
			StartedAt = startedAt;
			Until = until;
		}
	}

	private sealed class PersistedSign
	{
		public ZDO Zdo { get; }

		public string Text { get; }

		public PersistedSign(ZDO zdo, string text)
		{
			Zdo = zdo;
			Text = text;
		}
	}

	private sealed class PersistedContainer
	{
		public ZDO Zdo { get; }

		public List<ItemData> Items { get; }

		public PersistedContainer(ZDO zdo, List<ItemData> items)
		{
			Zdo = zdo;
			Items = items ?? new List<ItemData>();
		}
	}

	private sealed class PersistedWarehouse
	{
		public string Id { get; }

		public ZDO Sign { get; }

		public ZDO Input { get; }

		public float Radius { get; }

		public PersistedWarehouse(string id, ZDO sign, ZDO input, float radius)
		{
			Id = id;
			Sign = sign;
			Input = input;
			Radius = radius;
		}
	}

	private sealed class PersistedInputCandidate
	{
		public string Id { get; }

		public ZDO Sign { get; }

		public ZDO Input { get; }

		public int Infrastructure { get; }

		public bool Canonical { get; }

		public PersistedInputCandidate(string id, ZDO sign, ZDO input, int infrastructure, bool canonical)
		{
			Id = id;
			Sign = sign;
			Input = input;
			Infrastructure = infrastructure;
			Canonical = canonical;
		}
	}

	private sealed class WarehouseNode
	{
		public string Id { get; }

		public ServersideQoLZDO Sign { get; }

		public ServersideQoLZDO Input { get; }

		public float Radius { get; }

		public string Language { get; }

		public bool Valid { get; set; } = true;

		public string Error { get; set; } = string.Empty;

		public WarehouseNode(string id, ServersideQoLZDO sign, ServersideQoLZDO input, float radius, string language)
		{
			Id = id;
			Sign = sign;
			Input = input;
			Radius = radius;
			Language = language;
		}
	}

	private sealed class NetworkOverview
	{
		public string Signature { get; }

		public List<BoardMessage> Rows { get; }

		public NetworkOverview(string signature, List<BoardMessage> rows)
		{
			Signature = signature;
			Rows = rows;
		}
	}

	private sealed class PerfWindow
	{
		public DateTime Since { get; set; }

		public int Cycles { get; set; }

		public int Skipped { get; set; }

		public int AwakeCycles { get; set; }

		public int SleepingCycles { get; set; }

		public long TotalMs { get; set; }

		public long MaxMs { get; set; }

		public long TopologyMs { get; set; }

		public long ClassifyMs { get; set; }

		public long DisplayMs { get; set; }

		public long TransferMs { get; set; }

		public int Containers { get; set; }

		public int Signs { get; set; }

		public int Stores { get; set; }

		public int InventoryReads { get; set; }

		public int Peers { get; set; }

		public int PingAverage { get; set; }

		public int PingMaximum { get; set; }

		public float LocalQualityMinimum { get; set; } = 1f;

		public float RemoteQualityMinimum { get; set; } = 1f;

		public float OutBytesPerSecond { get; set; }

		public float InBytesPerSecond { get; set; }
	}

	private sealed class PendingLabel
	{
		public string ItemType { get; }

		public DateTime Since { get; }

		public PendingLabel(string itemType, DateTime since)
		{
			ItemType = itemType;
			Since = since;
		}
	}

	private sealed class StockEntry
	{
		public string Token { get; }

		public string Name { get; }

		public int Amount { get; }

		public StockEntry(string token, string name, int amount)
		{
			Token = token;
			Name = name;
			Amount = amount;
		}
	}

	private sealed class BoardControl
	{
		public string Prefix { get; set; }

		public int? Page { get; set; }

		public DateTime ChangedAt { get; set; }
	}

	private sealed class StatsControl
	{
		public int? Days { get; set; }

		public int? Page { get; set; }

		public DateTime ChangedAt { get; set; }
	}

	private sealed class TargetStatus
	{
		public string Name { get; }

		public int Current { get; }

		public int Target { get; }

		public TargetStatus(string name, int current, int target)
		{
			Name = name;
			Current = current;
			Target = target;
		}
	}

	private sealed class DeliveryNotice
	{
		public Dictionary<string, int> Items { get; }

		public DateTime Until { get; }

		public DeliveryNotice(Dictionary<string, int> items, DateTime until)
		{
			Items = items;
			Until = until;
		}
	}

	private sealed class BoardVisitState
	{
		public bool Awake { get; set; }

		public Dictionary<string, StockEntry> Baseline { get; set; } = new Dictionary<string, StockEntry>(StringComparer.OrdinalIgnoreCase);

		public List<StockChange> Changes { get; set; } = new List<StockChange>();

		public DateTime SummaryUntil { get; set; }
	}

	private sealed class StockChange
	{
		public string Name { get; }

		public int Amount { get; }

		public StockChange(string name, int amount)
		{
			Name = name;
			Amount = amount;
		}
	}

	private sealed class BoardMessage
	{
		public string Name { get; }

		public string Value { get; }

		public string Color { get; }

		public BoardMessage(string name, string value, string color)
		{
			Name = name;
			Value = value;
			Color = color;
		}
	}

	internal static readonly Regex Marker = new Regex("^\\s*\\[WH:(?<id>[A-Za-z0-9_-]+):(?<role>IN|OUT|REQ|INFO|BOARD|STATS)\\](?<payload>[\\s\\S]*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

	internal static readonly Regex IgnoreMarker = new Regex("^\\s*\\[WH:IGNORE\\]\\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

	internal static readonly Regex DeleteMarker = new Regex("^\\s*\\[WH:DELETE\\]\\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

	internal static readonly Regex CleanMarker = new Regex("^\\s*\\[WH:CLEAN\\]\\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private const string RequestWarehouseKey = "ModerWarehouse.RequestWarehouse";

	private const string InfoWarehouseKey = "ModerWarehouse.InfoWarehouse";

	private const string BoardWarehouseKey = "ModerWarehouse.BoardWarehouse";

	private const string BoardGroupKey = "ModerWarehouse.BoardGroup";

	private const string BoardCellKey = "ModerWarehouse.BoardCell";

	private const string BoardRenderedTextKey = "ModerWarehouse.BoardRenderedText";

	private const string StatsWarehouseKey = "ModerWarehouse.StatsWarehouse";

	private const string StatsGroupKey = "ModerWarehouse.StatsGroup";

	private const string StatsCellKey = "ModerWarehouse.StatsCell";

	private const string StatsHistoryKey = "ModerWarehouse.StatsHistory";

	private const string StatsControlKey = "ModerWarehouse.StatsControl";

	private const string RequestRenderedTextKey = "ModerWarehouse.RequestRenderedText";

	private const string RequestTargetsKey = "ModerWarehouse.RequestTargets";

	private const string HelpGroupKey = "ModerWarehouse.HelpGroup";

	private const string HelpChildKey = "ModerWarehouse.HelpChild";

	private const string HelpWarehouseKey = "ModerWarehouse.HelpWarehouse";

	private const int BoardColumns = 4;

	private const int BoardRows = 4;

	private const int BoardCellCount = 16;

	private const int BoardPageSize = 12;

	private const int BoardAnchorCell = 12;

	private const int StatsColumns = 4;

	private const int StatsRows = 4;

	private const int StatsCellCount = 16;

	private const int StatsAnchorCell = 12;

	private const string AutoChestXKey = "ModerWarehouse.AutoChestX";

	private const string AutoChestYKey = "ModerWarehouse.AutoChestY";

	private const string AutoChestZKey = "ModerWarehouse.AutoChestZ";

	private const string AutoLabelItemKey = "ModerWarehouse.AutoLabelItem";

	private const string AutoLabelManagedKey = "ModerWarehouse.AutoLabelManaged";

	private const string InputRadiusKey = "ModerWarehouse.InputRadius";

	private const string InputWarehouseKey = "ModerWarehouse.InputWarehouse";

	private const string InputCanonicalKey = "ModerWarehouse.InputCanonical";

	private const float GroupRecoveryDistance = 50f;

	private readonly HashSet<ServersideQoLZDO> _signs = new HashSet<ServersideQoLZDO>();

	private static readonly Dictionary<string, string> GermanNames = new Dictionary<string, string>();

	private static readonly Dictionary<string, string> GermanDisplayNames = new Dictionary<string, string>();

	private static readonly Dictionary<string, string> EnglishNames = new Dictionary<string, string>();

	private static readonly Dictionary<string, string> EnglishDisplayNames = new Dictionary<string, string>();

	private static readonly Dictionary<string, string> PrefabNames = new Dictionary<string, string>();

	internal static readonly Dictionary<string, HashSet<string>> AliasOwners = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

	private static bool _germanNamesLoaded;

	private SectorDictionary<HashSet<ServersideQoLZDO>> _containers;

	private readonly Dictionary<ServersideQoLZDO, PendingLabel> _pendingLabels = new Dictionary<ServersideQoLZDO, PendingLabel>();

	private readonly Dictionary<ServersideQoLZDO, string> _labelItemTokens = new Dictionary<ServersideQoLZDO, string>();

	private readonly Dictionary<ServersideQoLZDO, DateTime> _requestClearAt = new Dictionary<ServersideQoLZDO, DateTime>();

	private readonly Dictionary<string, DateTime> _orphanBoardSeenAt = new Dictionary<string, DateTime>(StringComparer.Ordinal);

	private readonly Dictionary<string, BoardControl> _boardControls = new Dictionary<string, BoardControl>(StringComparer.Ordinal);

	private readonly Dictionary<ServersideQoLZDO, DateTime> _helpPendingAt = new Dictionary<ServersideQoLZDO, DateTime>();

	private readonly Dictionary<string, DateTime> _boardEnsurePendingAt = new Dictionary<string, DateTime>(StringComparer.Ordinal);

	private readonly HashSet<string> _boardReadyGroups = new HashSet<string>(StringComparer.Ordinal);

	private readonly Dictionary<string, DateTime> _statsEnsurePendingAt = new Dictionary<string, DateTime>(StringComparer.Ordinal);

	private readonly HashSet<string> _statsReadyGroups = new HashSet<string>(StringComparer.Ordinal);

	private readonly Dictionary<string, StatsControl> _statsControls = new Dictionary<string, StatsControl>(StringComparer.Ordinal);

	private readonly Dictionary<string, DeliveryNotice> _deliveryNotices = new Dictionary<string, DeliveryNotice>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, BoardVisitState> _boardVisits = new Dictionary<string, BoardVisitState>(StringComparer.Ordinal);

	private NetworkOverview _networkOverview;

	private DateTime _networkOverviewAt;

	private bool _networkOverviewDirty = true;

	private readonly Dictionary<string, DateTime> _warehouseNextRun = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

	private readonly HashSet<string> _warehousesRunning = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private readonly HashSet<string> _warehousesDirty = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, WarehouseNode> _knownNodes = new Dictionary<string, WarehouseNode>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, bool> _lastAwake = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, DateTime> _lastCompleted = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<List<ServersideQoLZDO>, SpatialLookup<ServersideQoLZDO>> _spatialLookups = new Dictionary<List<ServersideQoLZDO>, SpatialLookup<ServersideQoLZDO>>();

	private List<ServersideQoLZDO> _storageSigns;

	private HashSet<ServersideQoLZDO> _localStorage;

	private readonly Dictionary<string, DateTime> _slowCycleLoggedAt = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, DateTime> _slowSampleAt = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, DateTime> _transferDiagnosticAt = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, PerfWindow> _performanceWindows = new Dictionary<string, PerfWindow>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, DateTime> _boardAwakeUntil = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, Dictionary<string, int>> _warehouseStockSnapshots = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, DateTime> _warehouseStockTimes = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

	private readonly HashSet<string> _freshlyReadWarehouses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, string> _globalInputErrors = new Dictionary<string, string>(StringComparer.Ordinal);

	private DateTime _globalInputValidationAt;

	private string _validatedTopology;

	private readonly Dictionary<string, RequestSearchNotice> _requestSearchNotices = new Dictionary<string, RequestSearchNotice>(StringComparer.OrdinalIgnoreCase);

	private Dictionary<ServersideQoLZDO, IInventory> _cycleInventories;

	private readonly Dictionary<ServersideQoLZDO, IInventory> _inventoryIndex = new Dictionary<ServersideQoLZDO, IInventory>();

	private string _performanceLogPath;

	private string _stockCachePath;

	private bool _performanceLogDisabled;

	private int _cycleContainers;

	private int _cycleSigns;

	private int _cycleStores;

	private int _cycleInventoryReads;

	private long _cycleTopologyMs;

	private long _cycleClassifyMs;

	private long _cycleDisplayMs;

	private long _cycleTransferMs;

	private int _cyclePeers;

	private int _cyclePingAverage;

	private int _cyclePingMaximum;

	private float _cycleLocalQualityMinimum;

	private float _cycleRemoteQualityMinimum;

	private float _cycleOutBytesPerSecond;

	private float _cycleInBytesPerSecond;

	private readonly Dictionary<ZDOID, DateTime> _inputInUseSince = new Dictionary<ZDOID, DateTime>();

	internal const int MaxWildcardItems = 10;

	private static readonly Dictionary<int, bool> SignPrefabs = new Dictionary<int, bool>();

	private IEnumerable<ServersideQoLZDO> StorageSigns
	{
		get
		{
			IEnumerable<ServersideQoLZDO> storageSigns = _storageSigns;
			return storageSigns ?? _signs;
		}
	}

	protected override void Initialize()
	{
		_signs.Clear();
		_pendingLabels.Clear();
		_requestClearAt.Clear();
		_orphanBoardSeenAt.Clear();
		_boardControls.Clear();
		_helpPendingAt.Clear();
		_boardEnsurePendingAt.Clear();
		_boardReadyGroups.Clear();
		_statsEnsurePendingAt.Clear();
		_statsReadyGroups.Clear();
		_statsControls.Clear();
		_deliveryNotices.Clear();
		_boardVisits.Clear();
		_networkOverview = null;
		_networkOverviewDirty = true;
		_warehouseNextRun.Clear();
		_warehousesRunning.Clear();
		_warehousesDirty.Clear();
		_knownNodes.Clear();
		_validatedTopology = null;
		_lastAwake.Clear();
		_lastCompleted.Clear();
		_spatialLookups.Clear();
		_storageSigns = null;
		_localStorage = null;
		_slowCycleLoggedAt.Clear();
		_slowSampleAt.Clear();
		_performanceWindows.Clear();
		_boardAwakeUntil.Clear();
		_warehouseStockSnapshots.Clear();
		_warehouseStockTimes.Clear();
		_freshlyReadWarehouses.Clear();
		_globalInputErrors.Clear();
		_globalInputValidationAt = DateTime.MinValue;
		_requestSearchNotices.Clear();
		_inventoryIndex.Clear();
		InitializePerformanceLog();
		InitializeStockCache();
		ContainerRegistryProcessor val = Processor.Instance<ContainerRegistryProcessor>();
		_containers = val.GetContainers(100f);
		val.ContainerChanged -= OnContainerChanged;
		val.ContainerChanged += OnContainerChanged;
		WarehouseLocalization.Initialize(delegate(string message)
		{
			this.Logger.LogInfo((object)message);
		});
		LoadGermanNames();
		this.Logger.LogInfo((object)(WarehousePlugin.Name + " " + WarehousePlugin.Version + ": localization, configurable capacity alerts, player-safe input ownership, solo-friendly visit deltas, target stock shopping list, delivery confirmation, automatic IN help sign, interactive rotating 4x4 stock board, rolling 4x4 statistics board, persistent linked automatic signs, safe transfers and multi-item withdrawal initialized; " + $"{GermanNames.Count} EN/DE item names loaded."));
		this.Logger.LogInfo((object)"Performance revision spatial-local-20260923: local labels, spatial links, coalesced scheduling, on-demand world reads.");
		this.Logger.LogInfo((object)"Local patch 10: craft accepts * wildcards; patch 9: ModerWarehouse owns chest signs inside a warehouse (ContainerSigns contents lists are adopted as labels, 'Item 123' labels mean Item); patch 8: one save per chest per cycle; patch 7: faster world scans; patch 6: craft also covers hammer/hoe/cultivator pieces; patch 5: REQ/INFO links and help sign survive ContainerSigns sign recreation, craft REQ (craft <item> [lvl N] [xN]), wildcard REQ (*), sign-only IN/OUT marker scan, stale IN lock release (owner-based).");
	}

	private void OnContainerChanged(ServersideQoLZDO changed, ContainerState state)
	{
		_networkOverviewDirty = true;
		if (changed != null)
		{
			_inventoryIndex.Remove(changed);
			_cycleInventories?.Remove(changed);
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (ServersideQoLZDO sign in _signs)
		{
			string text = WarehouseId(sign);
			if (!string.IsNullOrWhiteSpace(text) && hashSet.Add(text) && (changed == null || !_knownNodes.TryGetValue(text, out var value) || !(Distance2(changed, value.Input) > value.Radius * value.Radius)) && _warehousesDirty.Add(text))
			{
				this.ScheduleReprocessing(sign, 0.5f);
			}
		}
	}

	// The stock cache and performance log live in BepInEx/config/ModerWarehousePlus: mod managers
	// replace a mod's plugin folder on updates, and the config folder is left alone.
	private static string DataDirectory => Path.Combine(Paths.ConfigPath, "ModerWarehousePlus");

	// Where the original Moder Warehouse keeps its stock cache (its plugin folder).
	private static string OriginalStockCachePath => Path.Combine(Paths.PluginPath, "ModerWarehouse", "ModerWarehouse-stock-cache.tsv");

	private void InitializePerformanceLog()
	{
		_performanceLogDisabled = false;
		string text = DataDirectory;
		_performanceLogPath = Path.Combine(text, "ModerWarehouse-performance.log");
		try
		{
			Directory.CreateDirectory(text);
			File.WriteAllText(_performanceLogPath, WarehousePlugin.Name + " " + WarehousePlugin.Version + " performance log\r\n" + $"Started UTC: {WarehouseClock.UtcNow:O}\r\n" + "Revision: spatial-local-20260923; SUMMARY phase times are independent maxima; SLOW sampled at most once per 10s; skipped means throttled callbacks, not failed work.\r\nUTC | kind | warehouse | state | cycles | skipped | avg_ms | max_ms | topology_ms | classify_ms | display_ms | transfer_ms | containers | signs | stores | inventory_reads | peers | ping_avg_ms | ping_max_ms | local_quality_min | remote_quality_min | out_Bps | in_Bps\r\n", Encoding.UTF8);
			DeleteLegacyLog(Path.Combine(Paths.BepInExRootPath, "ModerWarehouse-performance.log"), "performance");
			this.Logger.LogInfo((object)("Performance log reset: " + _performanceLogPath));
		}
		catch (Exception ex)
		{
			_performanceLogDisabled = true;
			this.Logger.LogWarning((object)("Could not create warehouse performance log: " + ex.Message));
		}
	}

	private void InitializeStockCache()
	{
		string text = DataDirectory;
		_stockCachePath = Path.Combine(text, "ModerWarehouse-stock-cache.tsv");
		try
		{
			Directory.CreateDirectory(text);
			// First start after switching from the original mod: its cache is copied, and left
			// where it is for going back.
			if (WarehousePlugin.CopyIfMissing(OriginalStockCachePath, _stockCachePath))
			{
				this.Logger.LogInfo((object)("Stock cache copied from " + OriginalStockCachePath + "."));
			}
			if (!File.Exists(_stockCachePath))
			{
				return;
			}
			string[] array = File.ReadAllLines(_stockCachePath, Encoding.UTF8);
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split(new char[1] { '\t' });
				if (array2.Length >= 3 && int.TryParse(array2[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) && result > 0)
				{
					string key = Encoding.UTF8.GetString(Convert.FromBase64String(array2[0]));
					string key2 = Encoding.UTF8.GetString(Convert.FromBase64String(array2[1]));
					if (!_warehouseStockSnapshots.TryGetValue(key, out var value))
					{
						value = new Dictionary<string, int>(StringComparer.Ordinal);
						_warehouseStockSnapshots[key] = value;
					}
					value[key2] = result;
					if (array2.Length >= 4 && DateTime.TryParse(array2[3], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var result2))
					{
						_warehouseStockTimes[key] = result2.ToUniversalTime();
					}
				}
			}
			this.Logger.LogInfo((object)$"Loaded last-known stock for {_warehouseStockSnapshots.Count} warehouse(s).");
		}
		catch (Exception ex)
		{
			this.Logger.LogWarning((object)("Could not read warehouse stock cache: " + ex.Message));
		}
	}

	private void SaveStockCache()
	{
		if (string.IsNullOrWhiteSpace(_stockCachePath))
		{
			return;
		}
		try
		{
			string text = _stockCachePath + ".tmp";
			DateTime value;
			IEnumerable<string> contents = _warehouseStockSnapshots.OrderBy<KeyValuePair<string, Dictionary<string, int>>, string>((KeyValuePair<string, Dictionary<string, int>> pair) => pair.Key, StringComparer.OrdinalIgnoreCase).SelectMany((KeyValuePair<string, Dictionary<string, int>> warehouse) => warehouse.Value.Where((KeyValuePair<string, int> stock) => stock.Value > 0).OrderBy<KeyValuePair<string, int>, string>((KeyValuePair<string, int> stock) => stock.Key, StringComparer.Ordinal).Select(delegate(KeyValuePair<string, int> stock)
			{
				string[] obj = new string[7]
				{
					Convert.ToBase64String(Encoding.UTF8.GetBytes(warehouse.Key)),
					"\t",
					null,
					null,
					null,
					null,
					null
				};
				Encoding uTF = Encoding.UTF8;
				KeyValuePair<string, int> keyValuePair = stock;
				obj[2] = Convert.ToBase64String(uTF.GetBytes(keyValuePair.Key));
				obj[3] = "\t";
				keyValuePair = stock;
				obj[4] = keyValuePair.Value.ToString(CultureInfo.InvariantCulture);
				obj[5] = "\t";
				obj[6] = (_warehouseStockTimes.TryGetValue(warehouse.Key, out value) ? value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) : string.Empty);
				return string.Concat(obj);
			}));
			File.WriteAllLines(text, contents, Encoding.UTF8);
			if (File.Exists(_stockCachePath))
			{
				File.Delete(_stockCachePath);
			}
			File.Move(text, _stockCachePath);
		}
		catch (Exception ex)
		{
			this.Logger.LogWarning((object)("Could not save warehouse stock cache: " + ex.Message));
		}
	}

	private static bool StockEqual(Dictionary<string, int> left, Dictionary<string, int> right)
	{
		if (left == right)
		{
			return true;
		}
		if (left == null || right == null || left.Count != right.Count)
		{
			return false;
		}
		int value;
		return left.All((KeyValuePair<string, int> pair) => right.TryGetValue(pair.Key, out value) && value == pair.Value);
	}

	private static string WarehouseId(ServersideQoLZDO sign)
	{
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		string[] array = new string[4] { "ModerWarehouse.RequestWarehouse", "ModerWarehouse.InfoWarehouse", "ModerWarehouse.BoardWarehouse", "ModerWarehouse.StatsWarehouse" };
		foreach (string text in array)
		{
			string text2 = sign.ZDO.GetString(text, string.Empty);
			if (!string.IsNullOrWhiteSpace(text2))
			{
				return text2.Trim();
			}
		}
		Regex marker = Marker;
		ServersideQoLZDO.ZDOVars vars = sign.Vars;
		Match match = marker.Match(PlainText(vars.GetText("")));
		if (!match.Success)
		{
			return string.Empty;
		}
		return match.Groups["id"].Value.Trim();
	}

	protected override bool ClaimExclusive(ServersideQoLZDO zdo)
	{
		return false;
	}

	protected override ProcessResult Process(ServersideQoLZDO zdo, IReadOnlyList<Peer> peers, ProcessorPrefabInfo<Sign> prefabInfo)
	{
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b48: Unknown result type (might be due to invalid IL or missing references)
		//IL_07ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d33: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f7a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0940: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f14: Unknown result type (might be due to invalid IL or missing references)
		//IL_090d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b15: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ad9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a2b: Unknown result type (might be due to invalid IL or missing references)
		zdo.Fields<Piece>().Set((Func<Expression<Func<Piece, bool>>>)(() => (Piece x) => x.m_canBeRemoved), true);
		ServersideQoLZDO.ZDOVars vars = zdo.Vars;
		string text = PlainText(vars.GetText(""));
		if (CleanMarker.IsMatch(text))
		{
			List<ServersideQoLZDO> list = _signs.Where((ServersideQoLZDO other) => other != zdo && other.IsModCreator() && Distance2(other, zdo) < 0.0025f).ToList();
			foreach (ServersideQoLZDO item in list)
			{
				item.Destroy();
			}
			_signs.Remove(zdo);
			this.Logger.LogInfo((object)$"Cleaned warehouse sign stack: removed {list.Count + 1} sign(s).");
			return (ProcessResult)4;
		}
		if (DeleteMarker.IsMatch(text))
		{
			string boardGroup = zdo.ZDO.GetString("ModerWarehouse.BoardGroup", string.Empty);
			if (!string.IsNullOrWhiteSpace(boardGroup))
			{
				foreach (ServersideQoLZDO item2 in _signs.Where((ServersideQoLZDO x) => x.ZDO.GetString("ModerWarehouse.BoardGroup", string.Empty) == boardGroup).ToList())
				{
					_signs.Remove(item2);
					item2.Destroy();
				}
				this.Logger.LogInfo((object)("Deleted warehouse board group '" + boardGroup + "'."));
				return (ProcessResult)4;
			}
			string statsGroup = zdo.ZDO.GetString("ModerWarehouse.StatsGroup", string.Empty);
			if (!string.IsNullOrWhiteSpace(statsGroup))
			{
				foreach (ServersideQoLZDO item3 in _signs.Where((ServersideQoLZDO x) => x.ZDO.GetString("ModerWarehouse.StatsGroup", string.Empty) == statsGroup).ToList())
				{
					_signs.Remove(item3);
					item3.Destroy();
				}
				this.Logger.LogInfo((object)("Deleted warehouse statistics group '" + statsGroup + "'."));
				return (ProcessResult)4;
			}
			_signs.Remove(zdo);
			this.Logger.LogInfo((object)"Deleted warehouse sign marked [WH:DELETE].");
			return (ProcessResult)4;
		}
		bool num = _signs.Add(zdo);
		if (num)
		{
			zdo.Destroyed += delegate(ServersideQoLZDO destroyed)
			{
				_signs.Remove(destroyed);
			};
		}
		Match match = Marker.Match(text);
		if (num && match.Success)
		{
			_warehousesDirty.Add(match.Groups["id"].Value);
		}
		if (!match.Success)
		{
			RestoreChestSignLinks(zdo, text);
		}
		string text2 = zdo.ZDO.GetString("ModerWarehouse.RequestWarehouse", string.Empty);
		string text3 = zdo.ZDO.GetString("ModerWarehouse.InfoWarehouse", string.Empty);
		string text4 = zdo.ZDO.GetString("ModerWarehouse.BoardWarehouse", string.Empty);
		string text5 = zdo.ZDO.GetString("ModerWarehouse.StatsWarehouse", string.Empty);
		if (match.Success && match.Groups["role"].Value.Equals("REQ", StringComparison.OrdinalIgnoreCase))
		{
			text2 = match.Groups["id"].Value;
			zdo.ZDO.Set("ModerWarehouse.RequestWarehouse", text2);
			SaveChestSignLinks(zdo);
		}
		if (match.Success && match.Groups["role"].Value.Equals("INFO", StringComparison.OrdinalIgnoreCase))
		{
			text3 = match.Groups["id"].Value;
			zdo.ZDO.Set("ModerWarehouse.InfoWarehouse", text3);
			SaveChestSignLinks(zdo);
		}
		if (match.Success && match.Groups["role"].Value.Equals("BOARD", StringComparison.OrdinalIgnoreCase))
		{
			text4 = match.Groups["id"].Value;
			zdo.ZDO.Set("ModerWarehouse.BoardWarehouse", text4);
			if (string.IsNullOrWhiteSpace(zdo.ZDO.GetString("ModerWarehouse.BoardGroup", string.Empty)))
			{
				zdo.ZDO.Set("ModerWarehouse.BoardGroup", Guid.NewGuid().ToString("N"));
			}
			zdo.ZDO.Set("ModerWarehouse.BoardCell", 12);
			EnsureBoard(zdo, text4);
		}
		if (match.Success && match.Groups["role"].Value.Equals("STATS", StringComparison.OrdinalIgnoreCase))
		{
			text5 = match.Groups["id"].Value;
			zdo.ZDO.Set("ModerWarehouse.StatsWarehouse", text5);
			if (string.IsNullOrWhiteSpace(zdo.ZDO.GetString("ModerWarehouse.StatsGroup", string.Empty)))
			{
				zdo.ZDO.Set("ModerWarehouse.StatsGroup", Guid.NewGuid().ToString("N"));
			}
			zdo.ZDO.Set("ModerWarehouse.StatsCell", 12);
			EnsureStatsBoard(zdo, text5);
		}
		if (match.Success && match.Groups["role"].Value.Equals("IN", StringComparison.OrdinalIgnoreCase))
		{
			string text6 = match.Groups["id"].Value.Trim();
			float radius = ParseInputRadius(match.Groups["payload"].Value);
			WarehouseRenamePolicy.TryResolvePreviousId(zdo.ZDO.GetString("ModerWarehouse.InputWarehouse", string.Empty), text6, out var previousId);
			bool flag = HasOtherInputWithId(zdo, text6);
			if (WarehouseRenamePolicy.CanApplyRename(previousId, text6, flag))
			{
				MigrateWarehouseSigns(zdo, previousId, text6, radius);
				zdo.ZDO.Set("ModerWarehouse.InputWarehouse", text6);
			}
			else if (string.IsNullOrWhiteSpace(previousId) && !flag)
			{
				zdo.ZDO.Set("ModerWarehouse.InputWarehouse", text6);
			}
			else if (WarehouseRenamePolicy.ShouldLogRenameConflict(previousId, flag))
			{
				this.Logger.LogWarning((object)("Warehouse rename to '" + text6 + "' was not applied: another IN marker already uses that name."));
			}
			zdo.ZDO.Set("ModerWarehouse.InputRadius", ParseInputRadius(match.Groups["payload"].Value));
			EnsureHelpSign(zdo, text6, ResolveLanguage(match.Groups["payload"].Value));
		}
		if (!string.IsNullOrWhiteSpace(text2))
		{
			string text7 = (match.Success ? match.Groups["payload"].Value.Trim() : text.Trim());
			string text8 = zdo.ZDO.GetString("ModerWarehouse.RequestRenderedText", string.Empty);
			if (string.IsNullOrWhiteSpace(text7))
			{
				_requestClearAt.Remove(zdo);
				WriteRequestPrompt(zdo, text2, LanguageForWarehouse(text2));
				return this.ScheduleReprocessing(Mathf.Max(1f, ConfigBase<WarehouseConfig>.Instance.ScanInterval.Value));
			}
			if (text7.StartsWith("SOLL ", StringComparison.OrdinalIgnoreCase) || text7.StartsWith("TARGET ", StringComparison.OrdinalIgnoreCase))
			{
				int startIndex = (text7.StartsWith("SOLL ", StringComparison.OrdinalIgnoreCase) ? 5 : 7);
				if (TryParseTargets(text7.Substring(startIndex).Trim(), out var targets))
				{
					zdo.ZDO.Set("ModerWarehouse.RequestTargets", SerializeTargets(targets));
					SaveChestSignLinks(zdo);
					this.Logger.LogInfo((object)$"Warehouse '{text2}': saved {targets.Count} target stock value(s).");
					ProcessWarehouse(text2, peers, force: true);
				}
				else
				{
					string language = LanguageForWarehouse(text2);
					WriteRequestText(zdo, "<color=#ff6666>" + WarehouseLocalization.Text(language, "request.error_example") + "\n<color=white>" + WarehouseLocalization.Text(language, "request.target") + " Wood 1000");
				}
				return this.ScheduleReprocessing(Mathf.Max(1f, ConfigBase<WarehouseConfig>.Instance.ScanInterval.Value));
			}
			if (IsRequestPrompt(text7))
			{
				ProcessWarehouse(text2, peers);
				return this.ScheduleReprocessing(Mathf.Max(1f, ConfigBase<WarehouseConfig>.Instance.ScanInterval.Value));
			}
			if (!string.IsNullOrWhiteSpace(text8) && string.Equals(text7, text8, StringComparison.Ordinal))
			{
				if (IsStatus(text7) && !_requestClearAt.ContainsKey(zdo))
				{
					_requestClearAt[zdo] = WarehouseClock.UtcNow.AddSeconds(RequestDisplayPolicy.ClampResultSeconds(ConfigBase<WarehouseConfig>.Instance.RequestResultSeconds.Value));
				}
				if (_requestClearAt.TryGetValue(zdo, out var value) && WarehouseClock.UtcNow >= value)
				{
					_requestClearAt.Remove(zdo);
					WriteRequestPrompt(zdo, text2, LanguageForWarehouse(text2));
					ProcessWarehouse(text2, peers, force: true);
				}
				else
				{
					ProcessWarehouse(text2, peers);
				}
				return this.ScheduleReprocessing(Mathf.Max(1f, ConfigBase<WarehouseConfig>.Instance.ScanInterval.Value));
			}
			if (IsStatus(text7))
			{
				if (!_requestClearAt.TryGetValue(zdo, out var value2))
				{
					value2 = WarehouseClock.UtcNow.AddSeconds(RequestDisplayPolicy.ClampResultSeconds(ConfigBase<WarehouseConfig>.Instance.RequestResultSeconds.Value));
					_requestClearAt[zdo] = value2;
				}
				if (WarehouseClock.UtcNow >= value2)
				{
					WriteRequestPrompt(zdo, text2, LanguageForWarehouse(text2));
					_requestClearAt.Remove(zdo);
				}
				return this.ScheduleReprocessing(Mathf.Max(1f, ConfigBase<WarehouseConfig>.Instance.ScanInterval.Value));
			}
			_requestClearAt.Remove(zdo);
			ProcessWarehouse(text2, peers, force: true);
			return this.ScheduleReprocessing(Mathf.Max(1f, ConfigBase<WarehouseConfig>.Instance.ScanInterval.Value));
		}
		if (!string.IsNullOrWhiteSpace(text3))
		{
			ProcessWarehouse(text3, peers);
			return this.ScheduleReprocessing(Mathf.Max(1f, ConfigBase<WarehouseConfig>.Instance.ScanInterval.Value));
		}
		if (!string.IsNullOrWhiteSpace(text4))
		{
			string group = zdo.ZDO.GetString("ModerWarehouse.BoardGroup", string.Empty);
			int num2 = zdo.ZDO.GetInt("ModerWarehouse.BoardCell", -1);
			CaptureBoardControlInput(zdo, group, num2);
			if (!string.IsNullOrWhiteSpace(group))
			{
				DateTime value3;
				if (_signs.Any((ServersideQoLZDO sign) => sign.ZDO.GetString("ModerWarehouse.BoardGroup", string.Empty) == group && sign.ZDO.GetInt("ModerWarehouse.BoardCell", -1) == 12))
				{
					_orphanBoardSeenAt.Remove(group);
				}
				else if (!_orphanBoardSeenAt.TryGetValue(group, out value3))
				{
					_orphanBoardSeenAt[group] = WarehouseClock.UtcNow;
				}
				else if ((WarehouseClock.UtcNow - value3).TotalSeconds >= 10.0)
				{
					List<ServersideQoLZDO> list2 = _signs.Where((ServersideQoLZDO sign) => sign.ZDO.GetString("ModerWarehouse.BoardGroup", string.Empty) == group).ToList();
					foreach (ServersideQoLZDO item4 in list2)
					{
						_signs.Remove(item4);
						item4.Destroy();
					}
					_orphanBoardSeenAt.Remove(group);
					this.Logger.LogInfo((object)$"Removed {list2.Count} orphaned stock-board sign(s) from group '{group}'.");
					return (ProcessResult)4;
				}
			}
			if (num2 == 12)
			{
				EnsureBoard(zdo, text4);
			}
			if (num2 == 12)
			{
				ProcessWarehouse(text4, peers);
			}
			return this.ScheduleReprocessing(Mathf.Min(1f, Mathf.Max(0.5f, ConfigBase<WarehouseConfig>.Instance.BoardPageSeconds.Value / 2f)));
		}
		if (!string.IsNullOrWhiteSpace(text5))
		{
			int num3 = zdo.ZDO.GetInt("ModerWarehouse.StatsCell", -1);
			string statsGroup2 = zdo.ZDO.GetString("ModerWarehouse.StatsGroup", string.Empty);
			if (num3 == 6 && !string.IsNullOrWhiteSpace(zdo.ZDO.GetString("ModerWarehouse.StatsHistory", string.Empty)))
			{
				foreach (ServersideQoLZDO item5 in _signs.Where((ServersideQoLZDO x) => x != zdo && x.ZDO.GetString("ModerWarehouse.StatsGroup", string.Empty) == statsGroup2).ToList())
				{
					_signs.Remove(item5);
					item5.Destroy();
				}
				zdo.ZDO.Set("ModerWarehouse.StatsCell", 12);
				_statsReadyGroups.Remove(statsGroup2);
				_statsEnsurePendingAt.Remove(statsGroup2);
				this.Logger.LogInfo((object)("Warehouse '" + text5 + "': migrated 3x3 statistics board to 4x4."));
				num3 = 12;
			}
			if (num3 == 12)
			{
				EnsureStatsBoard(zdo, text5);
			}
			if (num3 == 12 || zdo.ZDO.GetBool("ModerWarehouse.StatsControl", false))
			{
				ProcessWarehouse(text5, peers);
			}
			return this.ScheduleReprocessing(Mathf.Min(1f, Mathf.Max(0.5f, ConfigBase<WarehouseConfig>.Instance.BoardPageSeconds.Value / 2f)));
		}
		if (match.Success || IgnoreMarker.IsMatch(text))
		{
			WriteWhite(zdo, text);
		}
		if (match.Success)
		{
			ProcessWarehouse(match.Groups["id"].Value, peers);
		}
		return this.ScheduleReprocessing(Mathf.Max(1f, ConfigBase<WarehouseConfig>.Instance.ScanInterval.Value));
	}

	private string LanguageForWarehouse(string id)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		foreach (ServersideQoLZDO sign in _signs)
		{
			Regex marker = Marker;
			ServersideQoLZDO.ZDOVars vars = sign.Vars;
			Match match = marker.Match(PlainText(vars.GetText("")));
			if (match.Success && match.Groups["id"].Value.Equals(id, StringComparison.OrdinalIgnoreCase) && match.Groups["role"].Value.Equals("IN", StringComparison.OrdinalIgnoreCase))
			{
				return ResolveLanguage(match.Groups["payload"].Value);
			}
		}
		return ResolveLanguage(string.Empty);
	}

	internal static string ResolveLanguage(string payload)
	{
		Match match = Regex.Match(payload ?? string.Empty, "(?:^|[\\r\\n])\\s*LANG(?:UAGE)?\\s*=\\s*(?<language>[A-Za-z][A-Za-z0-9_-]*)\\s*(?:$|[\\r\\n])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		return WarehouseLocalization.Resolve(match.Success ? match.Groups["language"].Value : ConfigBase<WarehouseConfig>.Instance.Language.Value);
	}

	private void ProcessWarehouse(string id, IReadOnlyList<Peer> peers, bool force = false)
	{
		if (string.IsNullOrWhiteSpace(id))
		{
			return;
		}
		string text = id.Trim();
		DateTime utcNow = WarehouseClock.UtcNow;
		if (_warehousesRunning.Contains(text))
		{
			PerformanceWindow(text).Skipped++;
			return;
		}
		bool flag = HasAwakeBoard(text, peers);
		bool value;
		bool woke = flag && (!_lastAwake.TryGetValue(text, out value) || !value);
		_lastAwake[text] = flag;
		_warehouseNextRun.TryGetValue(text, out var value2);
		_lastCompleted.TryGetValue(text, out var value3);
		if (!WarehouseSchedule.CanRun(utcNow, value2, value3, force, woke, _warehousesDirty.Contains(text)))
		{
			PerformanceWindow(text).Skipped++;
			return;
		}
		_warehousesDirty.Remove(text);
		double num = Math.Max(1.0, ConfigBase<WarehouseConfig>.Instance.ScanInterval.Value);
		double value4 = (flag ? 1.0 : Math.Max(30.0, num * 10.0));
		_warehouseNextRun[text] = utcNow.AddSeconds(value4);
		_warehousesRunning.Add(text);
		_cycleInventories = new Dictionary<ServersideQoLZDO, IInventory>();
		_cycleContainers = (_cycleSigns = (_cycleStores = (_cycleInventoryReads = 0)));
		_cycleTopologyMs = (_cycleClassifyMs = (_cycleDisplayMs = (_cycleTransferMs = 0L)));
		CapturePeerMetrics(peers);
		Stopwatch stopwatch = Stopwatch.StartNew();
		try
		{
			ProcessWarehouseCore(text, peers);
		}
		finally
		{
			FlushInventorySaves();
			stopwatch.Stop();
			_cycleInventories = null;
			_spatialLookups.Clear();
			_storageSigns = null;
			_localStorage = null;
			_warehousesRunning.Remove(text);
			_lastCompleted[text] = WarehouseClock.UtcNow;
			_warehouseNextRun[text] = WarehouseClock.UtcNow.AddSeconds(value4);
			RecordPerformance(text, flag, stopwatch.ElapsedMilliseconds);
			long num2 = (flag ? 150 : 250);
			if (stopwatch.ElapsedMilliseconds >= num2 && (!_slowCycleLoggedAt.TryGetValue(text, out var value5) || (WarehouseClock.UtcNow - value5).TotalSeconds >= 120.0))
			{
				_slowCycleLoggedAt[text] = WarehouseClock.UtcNow;
				this.Logger.LogWarning((object)($"Warehouse '{text}': slow coordinated cycle {stopwatch.ElapsedMilliseconds} ms " + "(board " + (flag ? "awake" : "sleeping") + ")."));
			}
		}
	}

	private bool HasAwakeBoard(string id, IReadOnlyList<Peer> peers)
	{
		if (peers == null || peers.Count == 0)
		{
			return false;
		}
		float distance2 = Mathf.Pow(Mathf.Max(1f, ConfigBase<WarehouseConfig>.Instance.BoardWakeDistance.Value), 2f);
		return _signs.Any(delegate(ServersideQoLZDO sign)
		{
			string text = sign.ZDO.GetString("ModerWarehouse.BoardWarehouse", string.Empty);
			string text2 = sign.ZDO.GetString("ModerWarehouse.StatsWarehouse", string.Empty);
			return (text.Equals(id, StringComparison.OrdinalIgnoreCase) || text2.Equals(id, StringComparison.OrdinalIgnoreCase)) && peers.Any(delegate(Peer peer)
			{
				//IL_0004: Unknown result type (might be due to invalid IL or missing references)
				//IL_0014: Unknown result type (might be due to invalid IL or missing references)
				//IL_0019: Unknown result type (might be due to invalid IL or missing references)
				//IL_001e: Unknown result type (might be due to invalid IL or missing references)
				if (peer != null)
				{
					Vector3 val = peer.RefPos - sign.ZDO.GetPosition();
					return val.sqrMagnitude <= distance2;
				}
				return false;
			});
		});
	}

	private void ProcessWarehouseCore(string id, IReadOnlyList<Peer> peers)
	{
		//IL_023a: Unknown result type (might be due to invalid IL or missing references)
		//IL_023f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0414: Unknown result type (might be due to invalid IL or missing references)
		//IL_0419: Unknown result type (might be due to invalid IL or missing references)
		//IL_0954: Unknown result type (might be due to invalid IL or missing references)
		//IL_0959: Unknown result type (might be due to invalid IL or missing references)
		//IL_099a: Unknown result type (might be due to invalid IL or missing references)
		Stopwatch stopwatch = Stopwatch.StartNew();
		string language = LanguageForWarehouse(id);
		List<ServersideQoLZDO> list = AllContainers();
		_cycleContainers = list.Count;
		_cycleSigns = _signs.Count;
		float num = ConfigBase<WarehouseConfig>.Instance.MarkerDistance.Value * ConfigBase<WarehouseConfig>.Instance.MarkerDistance.Value;
		List<WarehouseNode> list2 = DiscoverWarehouseNodes(list, num);
		ValidateWarehouseNodes(list2, num);
		ReconcileLegacyWarehouseSigns(list2);
		_knownNodes.Clear();
		foreach (WarehouseNode item2 in list2.Where((WarehouseNode x) => x.Valid))
		{
			_knownNodes[item2.Id] = item2;
		}
		UpdateInputHelpSigns(list2);
		UpdateUnpairedInputSigns(list2);
		_cycleTopologyMs = stopwatch.ElapsedMilliseconds;
		WarehouseNode localNode = list2.FirstOrDefault((WarehouseNode x) => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
		if (localNode == null || !localNode.Valid)
		{
			return;
		}
		_localStorage = new HashSet<ServersideQoLZDO>(list.Where((ServersideQoLZDO x) => Distance2(x, localNode.Input) <= localNode.Radius * localNode.Radius));
		float signRadius = localNode.Radius + Math.Max(ConfigBase<WarehouseConfig>.Instance.LabelDistance.Value, ConfigBase<WarehouseConfig>.Instance.MarkerDistance.Value) + 2f;
		_storageSigns = _signs.Where((ServersideQoLZDO sign) => Distance2(sign, localNode.Input) <= signRadius * signRadius || LinkedInside(sign, localNode)).ToList();
		HashSet<ServersideQoLZDO> endpoints = new HashSet<ServersideQoLZDO>();
		ServersideQoLZDO input = localNode.Input;
		ServersideQoLZDO val = null;
		List<Request> list3 = new List<Request>();
		List<ServersideQoLZDO> list4 = new List<ServersideQoLZDO>();
		List<ServersideQoLZDO> list5 = new List<ServersideQoLZDO>();
		List<ServersideQoLZDO> list6 = new List<ServersideQoLZDO>();
		float num2 = ConfigBase<WarehouseConfig>.Instance.LabelDistance.Value * ConfigBase<WarehouseConfig>.Instance.LabelDistance.Value;
		ServersideQoLZDO.ZDOVars vars;
		foreach (ServersideQoLZDO sign in _signs)
		{
			Regex marker = Marker;
			vars = sign.Vars;
			Match match = marker.Match(PlainText(vars.GetText("")));
			string text = sign.ZDO.GetString("ModerWarehouse.RequestWarehouse", string.Empty);
			string text2 = sign.ZDO.GetString("ModerWarehouse.InfoWarehouse", string.Empty);
			string text3 = sign.ZDO.GetString("ModerWarehouse.BoardWarehouse", string.Empty);
			string text4 = sign.ZDO.GetString("ModerWarehouse.StatsWarehouse", string.Empty);
			bool flag = !match.Success && text.Equals(id, StringComparison.OrdinalIgnoreCase);
			bool flag2 = !match.Success && text2.Equals(id, StringComparison.OrdinalIgnoreCase);
			bool flag3 = !match.Success && text3.Equals(id, StringComparison.OrdinalIgnoreCase);
			bool flag4 = !match.Success && text4.Equals(id, StringComparison.OrdinalIgnoreCase);
			if (!flag && !flag2 && !flag3 && !flag4 && (!match.Success || !match.Groups["id"].Value.Equals(id, StringComparison.OrdinalIgnoreCase)))
			{
				continue;
			}
			string text5 = (flag ? "REQ" : (flag2 ? "INFO" : (flag3 ? "BOARD" : (flag4 ? "STATS" : match.Groups["role"].Value.ToUpperInvariant()))));
			switch (text5)
			{
			case "REQ":
			{
				string text6;
				if (!flag)
				{
					text6 = match.Groups["payload"].Value.Trim();
				}
				else
				{
					vars = sign.Vars;
					text6 = PlainText(vars.GetText("")).Trim();
				}
				string payload = text6;
				list3.Add(new Request(sign, payload));
				continue;
			}
			case "INFO":
				list4.Add(sign);
				continue;
			case "BOARD":
				list5.Add(sign);
				continue;
			case "STATS":
				list6.Add(sign);
				continue;
			}
			ServersideQoLZDO val2 = NearestContainer(sign, list, num);
			if (val2 != null)
			{
				endpoints.Add(val2);
				if (text5 == "IN" && val2 == localNode.Input)
				{
					input = val2;
				}
				if (text5 == "OUT")
				{
					val = val2;
				}
			}
		}
		if (input == null)
		{
			return;
		}
		ContainerRegistryProcessor registry = Processor.Instance<ContainerRegistryProcessor>();
		ContainerState state = registry.GetState(input);
		if (state == null)
		{
			return;
		}
		IInventory val3 = InventoryFor(registry, input);
		if (val3 == null)
		{
			return;
		}
		float radius2 = localNode.Radius * localNode.Radius;
		HashSet<ServersideQoLZDO> ignored = DiscoverIgnored(list, num);
		RefreshAutomaticLabels(list, num2, registry, language);
		Dictionary<ServersideQoLZDO, string> labels = DiscoverLabels(list, num2);
		Dictionary<ServersideQoLZDO, ServersideQoLZDO> dictionary = DiscoverBlankSigns(list, num2);
		if (RemoveSupersededGeneratedLabels(dictionary, list, num2))
		{
			labels = DiscoverLabels(list, num2);
			dictionary = DiscoverBlankSigns(list, num2);
		}
		RepairGeneratedLabelPlacement(list, num2);
		HashSet<ServersideQoLZDO> optedIn = new HashSet<ServersideQoLZDO>(labels.Keys);
		optedIn.UnionWith(dictionary.Keys);
		List<ServersideQoLZDO> list7 = (from x in list
			where optedIn.Contains(x) && CanManageContainer(x, endpoints, ignored, optedIn.Contains(x))
			where Distance2(x, input) <= radius2
			where ContainsOnlyWarehouseItems(InventoryFor(registry, x))
			orderby Distance2(x, input)
			select x).ToList();
		int num3 = SanitizeForbiddenItems(list7, registry, list, num2);
		if (num3 > 0)
		{
			this.Logger.LogWarning((object)($"Warehouse '{id}' janitor purged {num3} forbidden internal item(s) " + "from signed managed storage; IN was left untouched."));
		}
		_cycleStores = list7.Count;
		int moves = Math.Max(1, ConfigBase<WarehouseConfig>.Instance.MaxMoves.Value);
		int num4 = CorrectMisfiledItems(id, list7, labels, registry, ref moves);
		if (num4 > 0)
		{
			this.Logger.LogInfo((object)$"Warehouse '{id}' janitor refiled {num4} valid misplaced item(s) in total.");
		}
		ServersideQoLZDO val4 = ((IEnumerable<ServersideQoLZDO>)list6).FirstOrDefault((Func<ServersideQoLZDO, bool>)((ServersideQoLZDO sign) => sign.ZDO.GetInt("ModerWarehouse.StatsCell", -1) == 12));
		WarehouseStatsHistory warehouseStatsHistory = ((val4 == null) ? null : WarehouseStatsHistory.Parse(val4.ZDO.GetString("ModerWarehouse.StatsHistory", string.Empty)));
		warehouseStatsHistory?.SetSnapshot(list7, delegate(ServersideQoLZDO store)
		{
			IInventory val6 = InventoryFor(registry, store);
			return (val6 != null) ? val6.Items : null;
		});
		Dictionary<string, int> dictionary2 = (from val6 in list7.SelectMany(delegate(ServersideQoLZDO store)
			{
				IInventory val6 = InventoryFor(registry, store);
				IEnumerable<ItemData> enumerable = ((val6 != null) ? val6.Items : null);
				return enumerable ?? Enumerable.Empty<ItemData>();
			})
			where val6 != null && val6.m_stack > 0 && val6.m_shared != null
			group val6 by val6.m_shared.m_name).ToDictionary<IGrouping<string, ItemData>, string, int>((IGrouping<string, ItemData> group) => group.Key, (IGrouping<string, ItemData> group) => group.Sum((ItemData val6) => val6.m_stack), StringComparer.Ordinal);
		if (!_warehouseStockSnapshots.TryGetValue(id, out var value) || !StockEqual(value, dictionary2))
		{
			_warehouseStockSnapshots[id] = dictionary2;
			_warehouseStockTimes[id] = WarehouseClock.UtcNow;
			SaveStockCache();
		}
		_cycleClassifyMs = stopwatch.ElapsedMilliseconds - _cycleTopologyMs;
		WhitenWarehouseLabels(list7.Concat(endpoints).Concat(ignored).Distinct()
			.ToList(), list, num2);
		LabelHomogeneousStores(list7, labels, dictionary, registry, language);
		AdoptContainerSigns(list7, list, num2);
		WriteWarehouseInfo(id, language, list4, list7, labels, registry);
		WriteShoppingLists(id, language, list3.Select((Request x) => x.Sign), list7, registry);
		WriteWarehouseBoard(id, language, list5, list7, registry, peers, list2, list, num);
		_cycleDisplayMs = stopwatch.ElapsedMilliseconds - _cycleTopologyMs - _cycleClassifyMs;
		int num5 = 0;
		HashSet<ServersideQoLZDO> hashSet = new HashSet<ServersideQoLZDO>();
		Dictionary<string, int> dictionary3 = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);
		bool flag5 = val3.Items.Any((ItemData val6) => IsWarehouseItem(val6) && val6.m_stack > 0);
		vars = input.Vars;
		bool flag6 = vars.GetInUse(false);
		if (flag6 && TryReleaseStaleInputLock(id, input, state, peers))
		{
			flag6 = false;
		}
		else if (!flag6)
		{
			_inputInUseSince.Remove(input.ZDO.m_uid);
		}
		bool flag7 = state.ZDO.IsOwnerOrUnassigned();
		bool flag8 = flag5 && !flag6 && Own(state);
		int num6 = 0;
		int num7 = 0;
		int num8 = 0;
		int num9 = 0;
		if (flag5 && !flag6 && flag8)
		{
			foreach (ItemData item in val3.Items.ToList())
			{
				if (moves <= 0 || item == null || item.m_stack <= 0)
				{
					break;
				}
				if (!IsWarehouseItem(item))
				{
					continue;
				}
				int num10 = item.m_stack;
				string value4;
				foreach (ServersideQoLZDO item3 in list7.Where((ServersideQoLZDO x) => labels.TryGetValue(x, out value4) ? LabelMatches(x, value4, item, registry) : IsSingleItemType(InventoryFor(registry, x), item)).OrderByDescending(delegate(ServersideQoLZDO x)
				{
					if (HasItem(InventoryFor(registry, x), item))
					{
						return 2;
					}
					return (labels.TryGetValue(x, out value4) && LabelMatches(x, value4, item, registry)) ? 1 : 0;
				}))
				{
					if (moves <= 0 || num10 <= 0)
					{
						break;
					}
					num6++;
					ContainerState state2 = registry.GetState(item3);
					if (state2 == null)
					{
						num7++;
						continue;
					}
					if (!Own(state2))
					{
						num8++;
						continue;
					}
					IInventory val5 = InventoryFor(registry, item3);
					bool flag9 = !labels.ContainsKey(item3) && IsSingleItemType(val5, item);
					int num11 = Move(val3, val5, item, num10);
					if (num11 <= 0)
					{
						num9++;
					}
					if (num11 <= 0)
					{
						continue;
					}
					string key = LocalizedItemName(item, language);
					dictionary3[key] = (dictionary3.TryGetValue(key, out var value2) ? (value2 + num11) : num11);
					num5 += num11;
					warehouseStatsHistory?.AddInput(item.m_shared.m_name, num11);
					num10 -= num11;
					moves--;
					hashSet.Add(item3);
					if (flag9 && ConfigBase<WarehouseConfig>.Instance.AutoCreateLabels.Value)
					{
						string value3 = CreateAutomaticLabel(item3, item, dictionary, language);
						if (!string.IsNullOrEmpty(value3))
						{
							labels[item3] = value3;
						}
					}
				}
			}
		}
		if (val3.Items.Any((ItemData val6) => val6 != null && val6.m_stack > 0) && num5 == 0 && ShouldLogTransferDiagnostic(id))
		{
			string text7 = string.Join(", ", from val6 in val3.Items
				where val6 != null && val6.m_stack > 0
				select $"{ItemName(val6)} x{val6.m_stack}" + string.Format(" [shared='{0}', cheated={1}, ", val6.m_shared?.m_name ?? "<null>", val6.m_cheated) + "type='" + (val6.m_shared?.m_itemType.ToString() ?? "<null>") + "']");
			int num12 = val3.Items.Count((ItemData val6) => val6 != null && val6.m_stack > 0 && !IsWarehouseItem(val6));
			this.Logger.LogWarning((object)("Warehouse '" + id + "': transfer diagnostic; input=[" + text7 + "], " + $"warehouseItems={flag5}, rejectedItemStacks={num12}, " + $"INInUse={flag6}, sourceOwnerBefore={flag7}, " + $"sourceOwnershipGranted={flag8}, stores={list7.Count}, " + $"matchingTargets={num6}, missingTargetStates={num7}, " + $"targetOwnershipBlocked={num8}, moveReturnedZero={num9}."));
		}
		if (num5 > 0)
		{
			_deliveryNotices[id] = new DeliveryNotice(dictionary3, WarehouseClock.UtcNow.AddSeconds(Math.Max(2f, ConfigBase<WarehouseConfig>.Instance.DeliveryNoticeSeconds.Value)));
			this.Logger.LogInfo((object)($"Warehouse '{id}': stored {num5} item(s) into " + $"{hashSet.Count} chest(s); {list7.Count} candidate chest(s) scanned."));
		}
		if (val != null)
		{
			foreach (Request item4 in list3)
			{
				ProcessRequest(id, language, item4, val, list7, registry, warehouseStatsHistory, ref moves, list2, list, num);
			}
		}
		if (warehouseStatsHistory != null)
		{
			warehouseStatsHistory.SetSnapshot(list7, delegate(ServersideQoLZDO store)
			{
				IInventory val6 = InventoryFor(registry, store);
				return (val6 != null) ? val6.Items : null;
			});
			if (warehouseStatsHistory.Changed)
			{
				val4.ZDO.Set("ModerWarehouse.StatsHistory", warehouseStatsHistory.Serialize());
			}
			WriteStatisticsBoard(id, language, list6, warehouseStatsHistory, list7, registry, peers, list2, list, num);
		}
		_cycleTransferMs = stopwatch.ElapsedMilliseconds - _cycleTopologyMs - _cycleClassifyMs - _cycleDisplayMs;
	}

	private bool TryReleaseStaleInputLock(string id, ServersideQoLZDO input, ContainerState state, IReadOnlyList<Peer> peers)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_0155: Unknown result type (might be due to invalid IL or missing references)
		//IL_016f: Unknown result type (might be due to invalid IL or missing references)
		float value = ConfigBase<WarehouseConfig>.Instance.StaleInputLockSeconds.Value;
		if (value <= 0f)
		{
			return false;
		}
		ZDOID uid = input.ZDO.m_uid;
		DateTime utcNow = WarehouseClock.UtcNow;
		if (!_inputInUseSince.TryGetValue(uid, out var value2))
		{
			_inputInUseSince[uid] = utcNow;
			this.Logger.LogInfo((object)("Warehouse '" + id + "': IN chest is marked in use; watching for a stale lock."));
			return false;
		}
		double totalSeconds = (utcNow - value2).TotalSeconds;
		Vector3 position = input.ZDO.GetPosition();
		float num = Mathf.Max(0f, ConfigBase<WarehouseConfig>.Instance.StaleInputLockPlayerDistance.Value);
		long owner = input.ZDO.GetOwner();
		Peer val = peers?.FirstOrDefault((Func<Peer, bool>)((Peer peer) => ((peer != null) ? peer.ZNetPeer : null) != null && peer.ZNetPeer.m_uid == owner));
		int num2;
		if (val != null)
		{
			Vector3 val2 = val.RefPos - position;
			num2 = ((val2.sqrMagnitude <= num * num) ? 1 : 0);
		}
		else
		{
			num2 = 0;
		}
		bool flag = (byte)num2 != 0;
		float num3 = Mathf.Max(value, ConfigBase<WarehouseConfig>.Instance.StaleInputLockMaxSeconds.Value);
		if (totalSeconds < (double)value || (flag && totalSeconds < (double)num3))
		{
			return false;
		}
		if (!Own(state))
		{
			this.Logger.LogWarning((object)$"Warehouse '{id}': stale IN chest lock ({totalSeconds:0} s) could not be released: ownership was not granted.");
			return false;
		}
		ServersideQoLZDO.ZDOVars vars = input.Vars;
		vars.SetInUse(false, "/tmp/claude-1000/-home-thibs-Desktop-Valheim-BepInEx-plugins-ModerWarehouse/1f4d0c08-19f3-4578-b39d-9e1f66c8da17/scratchpad/build/ModerWarehouse/WarehouseProcessor.cs", 1518);
		_inputInUseSince.Remove(uid);
		string arg = ((val == null) ? "its owner is not connected" : (flag ? $"hard limit {num3:0} s reached" : $"its owner is more than {num:0.#} m away"));
		this.Logger.LogWarning((object)$"Warehouse '{id}': released stale IN chest lock after {totalSeconds:0} s; {arg}.");
		return true;
	}

	internal static float ParseInputRadius(string payload)
	{
		float num = Mathf.Max(10f, ConfigBase<WarehouseConfig>.Instance.Radius.Value);
		float num2 = Mathf.Clamp(ConfigBase<WarehouseConfig>.Instance.MinimumRadius.Value, 1f, num);
		Match match = Regex.Match(payload ?? string.Empty, "(?:^|\\s)R(?:ADIUS)?\\s*=\\s*(?<radius>\\d+(?:[\\.,]\\d+)?)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		if (!match.Success || !float.TryParse(match.Groups["radius"].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			return num;
		}
		return Mathf.Clamp(result, num2, num);
	}

	private bool HasOtherInputWithId(ServersideQoLZDO inputSign, string id)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		foreach (ServersideQoLZDO sign in _signs)
		{
			if (sign != inputSign)
			{
				Regex marker = Marker;
				ServersideQoLZDO.ZDOVars vars = sign.Vars;
				Match match = marker.Match(PlainText(vars.GetText("")));
				if (match.Success && match.Groups["role"].Value.Equals("IN", StringComparison.OrdinalIgnoreCase) && match.Groups["id"].Value.Equals(id, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
		}
		return false;
	}

	private void MigrateWarehouseSigns(ServersideQoLZDO inputSign, string oldId, string newId, float radius)
	{
		//IL_0315: Unknown result type (might be due to invalid IL or missing references)
		//IL_031a: Unknown result type (might be due to invalid IL or missing references)
		//IL_040d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0412: Unknown result type (might be due to invalid IL or missing references)
		int num = 0;
		string text = inputSign.ZDO.GetString("ModerWarehouse.HelpGroup", string.Empty);
		HashSet<string> hashSet = new HashSet<string>(from sign in _signs
			where sign.ZDO.GetString("ModerWarehouse.BoardWarehouse", string.Empty).Equals(oldId, StringComparison.OrdinalIgnoreCase) || sign.ZDO.GetString("ModerWarehouse.BoardWarehouse", string.Empty).Equals(newId, StringComparison.OrdinalIgnoreCase)
			select sign.ZDO.GetString("ModerWarehouse.BoardGroup", string.Empty) into @group
			where !string.IsNullOrWhiteSpace(@group)
			select @group, StringComparer.Ordinal);
		HashSet<string> hashSet2 = new HashSet<string>(from sign in _signs
			where sign.ZDO.GetString("ModerWarehouse.StatsWarehouse", string.Empty).Equals(oldId, StringComparison.OrdinalIgnoreCase) || sign.ZDO.GetString("ModerWarehouse.StatsWarehouse", string.Empty).Equals(newId, StringComparison.OrdinalIgnoreCase)
			select sign.ZDO.GetString("ModerWarehouse.StatsGroup", string.Empty) into @group
			where !string.IsNullOrWhiteSpace(@group)
			select @group, StringComparer.Ordinal);
		foreach (ServersideQoLZDO item in _signs.Where((ServersideQoLZDO sign) => sign != inputSign).ToList())
		{
			bool flag = false;
			string[] array = new string[5] { "ModerWarehouse.RequestWarehouse", "ModerWarehouse.InfoWarehouse", "ModerWarehouse.BoardWarehouse", "ModerWarehouse.StatsWarehouse", "ModerWarehouse.HelpWarehouse" };
			foreach (string text2 in array)
			{
				if (item.ZDO.GetString(text2, string.Empty).Equals(oldId, StringComparison.OrdinalIgnoreCase))
				{
					SetWarehouseKey(item, text2, newId);
					flag = true;
				}
			}
			string text3 = item.ZDO.GetString("ModerWarehouse.BoardGroup", string.Empty);
			if (!string.IsNullOrWhiteSpace(text3) && hashSet.Contains(text3) && !item.ZDO.GetString("ModerWarehouse.BoardWarehouse", string.Empty).Equals(newId, StringComparison.OrdinalIgnoreCase))
			{
				SetWarehouseKey(item, "ModerWarehouse.BoardWarehouse", newId);
				flag = true;
			}
			string text4 = item.ZDO.GetString("ModerWarehouse.StatsGroup", string.Empty);
			if (!string.IsNullOrWhiteSpace(text4) && hashSet2.Contains(text4) && !item.ZDO.GetString("ModerWarehouse.StatsWarehouse", string.Empty).Equals(newId, StringComparison.OrdinalIgnoreCase))
			{
				SetWarehouseKey(item, "ModerWarehouse.StatsWarehouse", newId);
				flag = true;
			}
			if (!string.IsNullOrWhiteSpace(text) && item.ZDO.GetBool("ModerWarehouse.HelpChild", false) && item.ZDO.GetString("ModerWarehouse.HelpGroup", string.Empty) == text && !item.ZDO.GetString("ModerWarehouse.HelpWarehouse", string.Empty).Equals(newId, StringComparison.OrdinalIgnoreCase))
			{
				SetWarehouseKey(item, "ModerWarehouse.HelpWarehouse", newId);
				flag = true;
			}
			ServersideQoLZDO.ZDOVars vars = item.Vars;
			string input = PlainText(vars.GetText(""));
			Match match = Marker.Match(input);
			if (match.Success && !match.Groups["role"].Value.Equals("IN", StringComparison.OrdinalIgnoreCase) && match.Groups["id"].Value.Equals(oldId, StringComparison.OrdinalIgnoreCase))
			{
				string text5 = "[WH:" + newId + ":" + match.Groups["role"].Value.ToUpperInvariant() + "]" + match.Groups["payload"].Value;
				if (!item.IsOwnerOrUnassigned())
				{
					item.ClaimOwnership();
				}
				vars = item.Vars;
				vars.SetText(text5);
				flag = true;
			}
			if (flag)
			{
				num++;
			}
		}
		MigrateWarehouseRuntimeState(oldId, newId);
		_warehousesDirty.Add(newId);
		inputSign.ZDO.Set("ModerWarehouse.HelpWarehouse", newId);
		this.Logger.LogInfo((object)$"Warehouse renamed '{oldId}' -> '{newId}': migrated {num} linked sign(s), including {hashSet.Count} stock-board and {hashSet2.Count} statistics-board group(s) by persistent group identity (legacy radius {radius:0.#} m); board state and statistics history retained.");
	}

	private void MigrateWarehouseRuntimeState(string oldId, string newId)
	{
		Dictionary<string, int> value;
		bool num = _warehouseStockSnapshots.TryGetValue(oldId, out value);
		DateTime value2;
		bool flag = _warehouseStockTimes.TryGetValue(oldId, out value2);
		_warehouseStockSnapshots.Remove(oldId);
		_warehouseStockTimes.Remove(oldId);
		if (num)
		{
			_warehouseStockSnapshots.Remove(newId);
			_warehouseStockSnapshots[newId] = value;
		}
		_warehouseStockTimes.Remove(newId);
		if (flag)
		{
			_warehouseStockTimes[newId] = value2;
		}
		if (num || flag)
		{
			SaveStockCache();
		}
		MoveRuntimeValue(_warehouseNextRun, oldId, newId);
		MoveRuntimeValue(_lastAwake, oldId, newId);
		MoveRuntimeValue(_lastCompleted, oldId, newId);
		_slowCycleLoggedAt.Remove(oldId);
		_slowSampleAt.Remove(oldId);
		_performanceWindows.Remove(oldId);
		_knownNodes.Remove(oldId);
		bool num2 = _warehousesDirty.Remove(oldId);
		MoveRuntimeValue(_deliveryNotices, oldId, newId);
		MoveRuntimeValue(_requestSearchNotices, oldId, newId);
		MoveRuntimeValue(_boardAwakeUntil, oldId, newId);
		if (num2)
		{
			_warehousesDirty.Add(newId);
		}
		if (_freshlyReadWarehouses.Remove(oldId))
		{
			_freshlyReadWarehouses.Add(newId);
		}
		_networkOverview = null;
		_networkOverviewDirty = true;
	}

	private static void MoveRuntimeValue<T>(Dictionary<string, T> values, string oldId, string newId)
	{
		if (values.TryGetValue(oldId, out var value))
		{
			values.Remove(oldId);
			if (!values.ContainsKey(newId))
			{
				values[newId] = value;
			}
		}
	}

	private void ReconcileLegacyWarehouseSigns(List<WarehouseNode> network)
	{
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0407: Unknown result type (might be due to invalid IL or missing references)
		//IL_040c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0551: Unknown result type (might be due to invalid IL or missing references)
		//IL_0556: Unknown result type (might be due to invalid IL or missing references)
		List<WarehouseNode> list = network.Where((WarehouseNode node) => node.Valid).ToList();
		if (list.Count == 0)
		{
			return;
		}
		float maxDistance = 2500f;
		HashSet<string> hashSet = new HashSet<string>(list.Select((WarehouseNode node) => node.Id), StringComparer.OrdinalIgnoreCase);
		ServersideQoLZDO.ZDOVars vars;
		foreach (ServersideQoLZDO sign in _signs)
		{
			string text = sign.ZDO.GetString("ModerWarehouse.InputWarehouse", string.Empty);
			if (!string.IsNullOrWhiteSpace(text))
			{
				hashSet.Add(text.Trim());
			}
			Regex marker = Marker;
			vars = sign.Vars;
			Match match = marker.Match(PlainText(vars.GetText("")));
			if (match.Success && match.Groups["role"].Value.Equals("IN", StringComparison.OrdinalIgnoreCase))
			{
				hashSet.Add(match.Groups["id"].Value.Trim());
			}
		}
		int num = 0;
		foreach (IGrouping<string, ServersideQoLZDO> item in (from sign in _signs
			where !string.IsNullOrWhiteSpace(sign.ZDO.GetString("ModerWarehouse.BoardGroup", string.Empty))
			group sign by sign.ZDO.GetString("ModerWarehouse.BoardGroup", string.Empty)).ToList())
		{
			WarehouseNode warehouseNode = NearestUnambiguousWarehouse(((IEnumerable<ServersideQoLZDO>)item).FirstOrDefault((Func<ServersideQoLZDO, bool>)((ServersideQoLZDO sign) => sign.ZDO.GetInt("ModerWarehouse.BoardCell", -1) == 12)), list, maxDistance);
			if (warehouseNode == null)
			{
				continue;
			}
			foreach (ServersideQoLZDO item2 in item)
			{
				if (SetWarehouseKey(item2, "ModerWarehouse.BoardWarehouse", warehouseNode.Id))
				{
					num++;
				}
			}
		}
		foreach (IGrouping<string, ServersideQoLZDO> item3 in (from sign in _signs
			where !string.IsNullOrWhiteSpace(sign.ZDO.GetString("ModerWarehouse.StatsGroup", string.Empty))
			group sign by sign.ZDO.GetString("ModerWarehouse.StatsGroup", string.Empty)).ToList())
		{
			WarehouseNode warehouseNode2 = NearestUnambiguousWarehouse(((IEnumerable<ServersideQoLZDO>)item3).FirstOrDefault((Func<ServersideQoLZDO, bool>)((ServersideQoLZDO sign) => sign.ZDO.GetInt("ModerWarehouse.StatsCell", -1) == 12)), list, maxDistance);
			if (warehouseNode2 == null)
			{
				continue;
			}
			foreach (ServersideQoLZDO item4 in item3)
			{
				if (SetWarehouseKey(item4, "ModerWarehouse.StatsWarehouse", warehouseNode2.Id))
				{
					num++;
				}
			}
		}
		foreach (ServersideQoLZDO sign2 in _signs)
		{
			WarehouseNode warehouseNode3 = NearestUnambiguousWarehouse(sign2, list, maxDistance);
			if (warehouseNode3 == null)
			{
				continue;
			}
			if (sign2.ZDO.GetBool("ModerWarehouse.HelpChild", false) && SetWarehouseKey(sign2, "ModerWarehouse.HelpWarehouse", warehouseNode3.Id))
			{
				num++;
			}
			string[] array = new string[2] { "ModerWarehouse.RequestWarehouse", "ModerWarehouse.InfoWarehouse" };
			foreach (string text2 in array)
			{
				string text3 = sign2.ZDO.GetString(text2, string.Empty);
				if (!string.IsNullOrWhiteSpace(text3) && !hashSet.Contains(text3) && SetWarehouseKey(sign2, text2, warehouseNode3.Id))
				{
					num++;
				}
			}
			Regex marker2 = Marker;
			vars = sign2.Vars;
			Match match2 = marker2.Match(PlainText(vars.GetText("")));
			if (!match2.Success || match2.Groups["role"].Value.Equals("IN", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			string text4 = match2.Groups["id"].Value.Trim();
			if (text4.Equals(warehouseNode3.Id, StringComparison.OrdinalIgnoreCase) || hashSet.Contains(text4))
			{
				continue;
			}
			string text5 = match2.Groups["role"].Value.ToUpperInvariant();
			string text6 = text5 switch
			{
				"STATS" => "ModerWarehouse.StatsWarehouse", 
				"BOARD" => "ModerWarehouse.BoardWarehouse", 
				"INFO" => "ModerWarehouse.InfoWarehouse", 
				"REQ" => "ModerWarehouse.RequestWarehouse", 
				"OUT" => null, 
				_ => null, 
			};
			if (!(text5 != "OUT") || text6 != null)
			{
				if (!sign2.IsOwnerOrUnassigned())
				{
					sign2.ClaimOwnership();
				}
				vars = sign2.Vars;
				vars.SetText("[WH:" + warehouseNode3.Id + ":" + text5 + "]" + match2.Groups["payload"].Value);
				if (text6 != null)
				{
					SetWarehouseKey(sign2, text6, warehouseNode3.Id);
				}
				num++;
			}
		}
		if (num > 0)
		{
			this.Logger.LogInfo((object)$"Recovered {num} stale sign link(s) by nearby warehouse identity (within {50f:0} m); board groups and sleep/wake panels reattached.");
		}
	}

	private static WarehouseNode NearestUnambiguousWarehouse(ServersideQoLZDO sign, List<WarehouseNode> warehouses, float maxDistance2)
	{
		if (sign == null)
		{
			return null;
		}
		var list = (from node in warehouses
			select new
			{
				Node = node,
				Distance2 = Distance2(sign, node.Input)
			} into item
			where item.Distance2 <= maxDistance2
			orderby item.Distance2
			select item).Take(2).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		if (list.Count > 1 && Mathf.Abs(list[1].Distance2 - list[0].Distance2) < 1f)
		{
			return null;
		}
		return list[0].Node;
	}

	private static bool SetWarehouseKey(ServersideQoLZDO sign, string key, string id)
	{
		if (sign.ZDO.GetString(key, string.Empty).Equals(id, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		if (!sign.IsOwnerOrUnassigned())
		{
			sign.ClaimOwnership();
		}
		sign.ZDO.Set(key, id);
		return true;
	}

	private List<WarehouseNode> DiscoverWarehouseNodes(List<ServersideQoLZDO> containers, float markerRange2)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		List<WarehouseNode> list = new List<WarehouseNode>();
		foreach (ServersideQoLZDO sign in _signs)
		{
			Regex marker = Marker;
			ServersideQoLZDO.ZDOVars vars = sign.Vars;
			Match match = marker.Match(PlainText(vars.GetText("")));
			if (match.Success && match.Groups["role"].Value.Equals("IN", StringComparison.OrdinalIgnoreCase))
			{
				ServersideQoLZDO val = NearestContainer(sign, containers, markerRange2);
				if (val != null)
				{
					float num = ParseInputRadius(match.Groups["payload"].Value);
					sign.ZDO.Set("ModerWarehouse.InputRadius", num);
					list.Add(new WarehouseNode(match.Groups["id"].Value, sign, val, num, ResolveLanguage(match.Groups["payload"].Value)));
				}
			}
		}
		return list;
	}

	private unsafe void ValidateWarehouseNodes(List<WarehouseNode> nodes, float markerRange2)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		List<string> list = new List<string>();
		foreach (ServersideQoLZDO sign in _signs)
		{
			Regex marker = Marker;
			ServersideQoLZDO.ZDOVars vars = sign.Vars;
			Match match = marker.Match(PlainText(vars.GetText("")));
			if (match.Success && match.Groups["role"].Value.Equals("IN", StringComparison.OrdinalIgnoreCase))
			{
				string text = sign.ZDO.m_uid.ToString();
				hashSet.Add(text);
				string[] array = new string[5]
				{
					text,
					":",
					match.Groups["id"].Value,
					":",
					null
				};
				Vector3 position = sign.ZDO.GetPosition();
				array[4] = position.ToString("R");
				list.Add(string.Concat(array));
			}
		}
		string text2 = string.Join(";", nodes.Select(delegate(WarehouseNode node)
		{
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			//IL_0075: Unknown result type (might be due to invalid IL or missing references)
			//IL_007a: Unknown result type (might be due to invalid IL or missing references)
			string[] array2 = new string[9];
			ZDOID uid = node.Sign.ZDO.m_uid;
			array2[0] = uid.ToString();
			array2[1] = ":";
			array2[2] = node.Id;
			array2[3] = ":";
			uid = node.Input.ZDO.m_uid;
			array2[4] = uid.ToString();
			array2[5] = ":";
			Vector3 position2 = node.Input.ZDO.GetPosition();
			array2[6] = position2.ToString("R");
			array2[7] = ":";
			array2[8] = node.Radius.ToString("R", CultureInfo.InvariantCulture);
			return string.Concat(array2);
		}).OrderBy<string, string>((string result) => result, StringComparer.Ordinal)) + "|" + string.Join(";", list.OrderBy<string, string>((string result) => result, StringComparer.Ordinal)) + "|" + ConfigBase<WarehouseConfig>.Instance.MinimumWarehouseDistance.Value.ToString("R", CultureInfo.InvariantCulture);
		if (hashSet.Any((string uid) => !_globalInputErrors.ContainsKey(uid)) || text2 != _validatedTopology || (WarehouseClock.UtcNow - _globalInputValidationAt).TotalMinutes >= 5.0)
		{
			RefreshGlobalInputValidation(markerRange2);
			_validatedTopology = text2;
		}
		foreach (WarehouseNode node in nodes)
		{
			if (_globalInputErrors.TryGetValue(node.Sign.ZDO.m_uid.ToString(), out var value) && !string.IsNullOrWhiteSpace(value))
			{
				node.Valid = false;
				node.Error = value;
			}
		}
	}

	private void RefreshGlobalInputValidation(float markerRange2)
	{
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Expected O, but got Unknown
		//IL_036c: Unknown result type (might be due to invalid IL or missing references)
		//IL_037d: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (!(typeof(ZDOMan).GetField("m_objectsByID", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(ZDOMan.instance) is IDictionary dictionary))
			{
				return;
			}
			List<ZDO> list = new List<ZDO>(dictionary.Count);
			List<PersistedSign> list2 = new List<PersistedSign>();
			foreach (DictionaryEntry item in dictionary)
			{
				object value = item.Value;
				ZDO val = (ZDO)((value is ZDO) ? value : null);
				// Local patch 7: classify by cached prefab first. Only chests feed the spatial index and
				// only signs have their text read, instead of every object in the world.
				if (val == null)
				{
					continue;
				}
				if (IsContainerPrefab(val.GetPrefab()))
				{
					list.Add(val);
				}
				else if (IsSignObject(val))
				{
					string value2 = val.GetString(ZDOVars.s_text, string.Empty);
					if (!string.IsNullOrWhiteSpace(value2))
					{
						list2.Add(new PersistedSign(val, PlainText(value2)));
					}
				}
			}
			PersistedContainerSpatialLookup persistedContainerSpatialLookup = new PersistedContainerSpatialLookup(list);
			List<PersistedInputCandidate> list3 = new List<PersistedInputCandidate>();
			int num = 0;
			_globalInputErrors.Clear();
			foreach (PersistedSign item2 in list2)
			{
				Match match = Marker.Match(item2.Text);
				if (!match.Success || !match.Groups["role"].Value.Equals("IN", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				ZDO input = persistedContainerSpatialLookup.Nearest(item2.Zdo, markerRange2);
				string id = match.Groups["id"].Value.Trim();
				if (input == null)
				{
					_globalInputErrors[item2.Zdo.m_uid.ToString()] = "NO_INPUT";
					num++;
					this.Logger.LogWarning((object)$"IN '{id}' rejected: no input chest found within {Mathf.Sqrt(markerRange2):0.#} m.");
					continue;
				}
				int infrastructure = list2.Count(delegate(PersistedSign other)
				{
					//IL_0092: Unknown result type (might be due to invalid IL or missing references)
					//IL_009d: Unknown result type (might be due to invalid IL or missing references)
					//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
					//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
					if (other.Zdo.GetString("ModerWarehouse.BoardWarehouse", string.Empty).Equals(id, StringComparison.OrdinalIgnoreCase) || other.Zdo.GetString("ModerWarehouse.StatsWarehouse", string.Empty).Equals(id, StringComparison.OrdinalIgnoreCase) || other.Zdo.GetString("ModerWarehouse.RequestWarehouse", string.Empty).Equals(id, StringComparison.OrdinalIgnoreCase) || other.Zdo.GetString("ModerWarehouse.InfoWarehouse", string.Empty).Equals(id, StringComparison.OrdinalIgnoreCase))
					{
						Vector3 val2 = other.Zdo.GetPosition() - input.GetPosition();
						return val2.sqrMagnitude <= 14400f;
					}
					return false;
				});
				list3.Add(new PersistedInputCandidate(id, item2.Zdo, input, infrastructure, item2.Zdo.GetBool("ModerWarehouse.InputCanonical", false)));
			}
			List<PersistedInputCandidate> list4 = new List<PersistedInputCandidate>();
			float minimum = Mathf.Max(1f, ConfigBase<WarehouseConfig>.Instance.MinimumWarehouseDistance.Value);
			list3.Sort((PersistedInputCandidate left, PersistedInputCandidate right) => WarehouseDuplicatePolicy.Compare(left.Canonical, left.Infrastructure, left.Sign.m_uid.ToString(), right.Canonical, right.Infrastructure, right.Sign.m_uid.ToString()));
			foreach (PersistedInputCandidate candidate in list3)
			{
				if (list4.FirstOrDefault((PersistedInputCandidate item) => item.Id.Equals(candidate.Id, StringComparison.OrdinalIgnoreCase)) != null)
				{
					_globalInputErrors[candidate.Sign.m_uid.ToString()] = "DUPLICATE";
					this.Logger.LogWarning((object)("IN '" + candidate.Id + "' rejected: warehouse name already exists."));
					continue;
				}
				PersistedInputCandidate persistedInputCandidate = list4.FirstOrDefault((PersistedInputCandidate item) => WarehouseDistancePolicy.IsTooClose(Vector3.Distance(item.Input.GetPosition(), candidate.Input.GetPosition()), minimum));
				if (persistedInputCandidate != null)
				{
					float num2 = Vector3.Distance(persistedInputCandidate.Input.GetPosition(), candidate.Input.GetPosition());
					_globalInputErrors[candidate.Sign.m_uid.ToString()] = $"TOO_CLOSE|{persistedInputCandidate.Id.ToUpperInvariant()}|{num2:0}|{minimum:0}";
					this.Logger.LogWarning((object)$"IN '{candidate.Id}' rejected: {num2:0.#} m from '{persistedInputCandidate.Id}', minimum is {minimum:0.#} m.");
				}
				else
				{
					list4.Add(candidate);
					if (!candidate.Canonical)
					{
						candidate.Sign.Set("ModerWarehouse.InputCanonical", true);
					}
					_globalInputErrors[candidate.Sign.m_uid.ToString()] = string.Empty;
				}
			}
			HashSet<string> acceptedIds = new HashSet<string>(list4.Select((PersistedInputCandidate item) => item.Id), StringComparer.OrdinalIgnoreCase);
			bool flag = false;
			foreach (string item3 in _warehouseStockSnapshots.Keys.Where((string item) => !acceptedIds.Contains(item)).ToList())
			{
				_warehouseStockSnapshots.Remove(item3);
				_warehouseStockTimes.Remove(item3);
				_freshlyReadWarehouses.Remove(item3);
				flag = true;
			}
			if (flag)
			{
				SaveStockCache();
			}
			_globalInputValidationAt = WarehouseClock.UtcNow;
			this.Logger.LogInfo((object)($"Global warehouse topology: {list4.Count} accepted, " + $"{list3.Count - list4.Count + num} blocked IN marker(s)."));
		}
		catch (Exception ex)
		{
			this.Logger.LogWarning((object)("Global warehouse topology validation failed: " + ex.Message));
		}
	}

	private void UpdateInputHelpSigns(List<WarehouseNode> nodes)
	{
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		float num = Mathf.Max(1f, ConfigBase<WarehouseConfig>.Instance.MinimumWarehouseDistance.Value);
		foreach (WarehouseNode node in nodes)
		{
			string group = node.Sign.ZDO.GetString("ModerWarehouse.HelpGroup", string.Empty);
			ServersideQoLZDO val = ((IEnumerable<ServersideQoLZDO>)_signs).FirstOrDefault((Func<ServersideQoLZDO, bool>)((ServersideQoLZDO x) => x.ZDO.GetBool("ModerWarehouse.HelpChild", false) && x.ZDO.GetString("ModerWarehouse.HelpGroup", string.Empty) == group));
			bool flag = WarehouseLocalization.Resolve(node.Language) == "de";
			string text = FormatInputError(node.Error, flag, num, ConfigBase<WarehouseConfig>.Instance.MarkerDistance.Value);
			string text2 = (node.Valid ? string.Format("<color=#80ffff>{0} {1}\n<color=white>RADIUS {2:0.#} M\n<color=#80ff80>{3}\n<color=white>REQ: {4} 100", flag ? "LAGER" : "WAREHOUSE", node.Id.ToUpperInvariant(), node.Radius, flag ? "ABSTAND OK" : "DISTANCE OK", WarehouseLocalization.Text(node.Language, "request.example_item").ToUpperInvariant()) : string.Format("<color=#ff6666>{0}\n{1}\n<color=white>MIN {2:0} M\n{3}", flag ? "LAGER GESPERRT" : "WAREHOUSE LOCKED", text, num, flag ? "IN KORRIGIEREN" : "CORRECT IN MARKER"));
			if (val == null)
			{
				continue;
			}
			ServersideQoLZDO.ZDOVars vars = val.Vars;
			if (!(vars.GetText("") == text2))
			{
				if (!val.IsOwnerOrUnassigned())
				{
					val.ClaimOwnership();
				}
				vars = val.Vars;
				vars.SetText(text2);
			}
		}
	}

	private static string FormatInputError(string error, bool german, float minimum, float markerDistance)
	{
		if (string.Equals(error, "DUPLICATE", StringComparison.Ordinal))
		{
			if (!german)
			{
				return "NAME ALREADY EXISTS";
			}
			return "NAME BEREITS VERGEBEN";
		}
		if (string.Equals(error, "NO_INPUT", StringComparison.Ordinal))
		{
			if (!german)
			{
				return $"NO INPUT CHEST WITHIN {markerDistance:0.#} M";
			}
			return $"KEINE IN-KISTE INNERHALB {markerDistance:0.#} M";
		}
		if (!string.IsNullOrWhiteSpace(error) && error.StartsWith("TOO_CLOSE|", StringComparison.Ordinal))
		{
			string[] array = error.Split(new char[1] { '|' });
			if (array.Length >= 3)
			{
				float result;
				float num = ((array.Length >= 4 && float.TryParse(array[3], NumberStyles.Float, CultureInfo.InvariantCulture, out result)) ? result : minimum);
				if (!german)
				{
					return $"TOO CLOSE TO {array[1]}: {array[2]} M (MIN. {num:0} M)";
				}
				return $"ZU NAH AN {array[1]}: {array[2]} M (MIN. {num:0} M)";
			}
		}
		return error;
	}

	private void UpdateUnpairedInputSigns(List<WarehouseNode> nodes)
	{
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0220: Unknown result type (might be due to invalid IL or missing references)
		//IL_0225: Unknown result type (might be due to invalid IL or missing references)
		HashSet<string> hashSet = new HashSet<string>(nodes.Select((WarehouseNode node) => node.Sign.ZDO.m_uid.ToString()), StringComparer.Ordinal);
		float num = Mathf.Max(1f, ConfigBase<WarehouseConfig>.Instance.MinimumWarehouseDistance.Value);
		float value = ConfigBase<WarehouseConfig>.Instance.MarkerDistance.Value;
		foreach (ServersideQoLZDO sign in _signs)
		{
			string text = sign.ZDO.m_uid.ToString();
			if (hashSet.Contains(text) || !_globalInputErrors.TryGetValue(text, out var value2) || string.IsNullOrWhiteSpace(value2))
			{
				continue;
			}
			Regex marker = Marker;
			ServersideQoLZDO.ZDOVars vars = sign.Vars;
			Match match = marker.Match(PlainText(vars.GetText("")));
			if (!match.Success || !match.Groups["role"].Value.Equals("IN", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			bool flag = WarehouseLocalization.Resolve(ResolveLanguage(match.Groups["payload"].Value)) == "de";
			string text2 = FormatInputError(value2, flag, num, value);
			string group = sign.ZDO.GetString("ModerWarehouse.HelpGroup", string.Empty);
			ServersideQoLZDO val = ((IEnumerable<ServersideQoLZDO>)_signs).FirstOrDefault((Func<ServersideQoLZDO, bool>)((ServersideQoLZDO item) => item.ZDO.GetBool("ModerWarehouse.HelpChild", false) && item.ZDO.GetString("ModerWarehouse.HelpGroup", string.Empty) == group));
			if (val == null)
			{
				continue;
			}
			string text3 = "<color=#ff6666>" + (flag ? "LAGER GESPERRT" : "WAREHOUSE LOCKED") + "\n" + text2 + "\n" + string.Format("<color=white>MIN {0:0} M\n{1}", num, flag ? "IN KORRIGIEREN" : "CORRECT IN MARKER");
			vars = val.Vars;
			if (!(vars.GetText("") == text3))
			{
				if (!val.IsOwnerOrUnassigned())
				{
					val.ClaimOwnership();
				}
				vars = val.Vars;
				vars.SetText(text3);
			}
		}
	}

	private List<ServersideQoLZDO> StoresForNode(WarehouseNode node, List<ServersideQoLZDO> containers, float markerRange2, ContainerRegistryProcessor registry)
	{
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		HashSet<ServersideQoLZDO> endpoints = new HashSet<ServersideQoLZDO> { node.Input };
		foreach (ServersideQoLZDO sign in _signs)
		{
			Regex marker = Marker;
			ServersideQoLZDO.ZDOVars vars = sign.Vars;
			Match match = marker.Match(PlainText(vars.GetText("")));
			if (!match.Success || !match.Groups["id"].Value.Equals(node.Id, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			string text = match.Groups["role"].Value.ToUpperInvariant();
			if (!(text != "IN") || !(text != "OUT"))
			{
				ServersideQoLZDO val = NearestContainer(sign, containers, markerRange2);
				if (val != null)
				{
					endpoints.Add(val);
				}
			}
		}
		HashSet<ServersideQoLZDO> ignored = DiscoverIgnored(containers, markerRange2);
		List<ServersideQoLZDO> signs = StorageSignsForNode(node);
		Dictionary<ServersideQoLZDO, string> dictionary = DiscoverLabels(containers, ConfigBase<WarehouseConfig>.Instance.LabelDistance.Value * ConfigBase<WarehouseConfig>.Instance.LabelDistance.Value, signs, rememberItemTokens: false);
		Dictionary<ServersideQoLZDO, ServersideQoLZDO> dictionary2 = DiscoverBlankSigns(containers, ConfigBase<WarehouseConfig>.Instance.LabelDistance.Value * ConfigBase<WarehouseConfig>.Instance.LabelDistance.Value, signs);
		HashSet<ServersideQoLZDO> optedIn = new HashSet<ServersideQoLZDO>(dictionary.Keys);
		optedIn.UnionWith(dictionary2.Keys);
		float radius2 = node.Radius * node.Radius;
		return containers.Where((ServersideQoLZDO x) => optedIn.Contains(x) && CanManageContainer(x, endpoints, ignored, hasStorageSign: true) && Distance2(x, node.Input) <= radius2 && ContainsOnlyWarehouseItems(InventoryFor(registry, x))).ToList();
	}

	private List<ServersideQoLZDO> StorageSignsForNode(WarehouseNode node)
	{
		float signRadius = node.Radius + Math.Max(ConfigBase<WarehouseConfig>.Instance.LabelDistance.Value, ConfigBase<WarehouseConfig>.Instance.MarkerDistance.Value) + 2f;
		return _signs.Where((ServersideQoLZDO sign) => Distance2(sign, node.Input) <= signRadius * signRadius || LinkedInside(sign, node)).ToList();
	}

	private Dictionary<ServersideQoLZDO, string> DiscoverLabels(List<ServersideQoLZDO> containers, float range2, IEnumerable<ServersideQoLZDO> signs = null, bool rememberItemTokens = true)
	{
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		Dictionary<ServersideQoLZDO, string> dictionary = new Dictionary<ServersideQoLZDO, string>();
		if (rememberItemTokens)
		{
			_labelItemTokens.Clear();
		}
		foreach (ServersideQoLZDO item in signs ?? StorageSigns)
		{
			if (IsRememberedRequest(item) || IsRememberedInfo(item) || IsRememberedBoard(item) || IsRememberedStats(item) || item.ZDO.GetBool("ModerWarehouse.HelpChild", false))
			{
				continue;
			}
			ServersideQoLZDO.ZDOVars vars = item.Vars;
			string text = vars.GetText("") ?? string.Empty;
			// Local patch 9: inside a warehouse, ModerWarehouse owns chest signs. A live ServersideQoL
			// ContainerSigns contents list is not a label (DiscoverBlankSigns adopts that sign instead).
			if (IsContainerSignsList(text))
			{
				continue;
			}
			string text2 = PlainText(text);
			if (IsBlankLabel(text2) || Marker.IsMatch(text2) || IgnoreMarker.IsMatch(text2) || DeleteMarker.IsMatch(text2) || CleanMarker.IsMatch(text2))
			{
				continue;
			}
			if (TryStripContentCount(text2, out var cleanLabel))
			{
				// Local patch 9: "Wood 521" is a contents line frozen into a label: it means Wood.
				text = "<color=white>" + cleanLabel;
				if (rememberItemTokens)
				{
					if (!item.IsOwnerOrUnassigned())
					{
						item.ClaimOwnership();
					}
					if (item.IsOwnerOrUnassigned())
					{
						item.Vars.SetText(text);
						this.Logger.LogInfo((object)("Label '" + text2 + "' cleaned to '" + cleanLabel + "'."));
					}
				}
			}
			ServersideQoLZDO val = LinkedOrNearestContainer(item, containers, range2);
			if (val != null)
			{
				dictionary[val] = (dictionary.TryGetValue(val, out var value) ? (value + " " + text) : text);
				string value2 = item.ZDO.GetString("ModerWarehouse.AutoLabelItem", string.Empty);
				if (rememberItemTokens && !string.IsNullOrWhiteSpace(value2))
				{
					_labelItemTokens[val] = value2;
				}
			}
		}
		return dictionary;
	}

	private Dictionary<ServersideQoLZDO, ServersideQoLZDO> DiscoverBlankSigns(List<ServersideQoLZDO> containers, float range2, IEnumerable<ServersideQoLZDO> signs = null)
	{
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		Dictionary<ServersideQoLZDO, ServersideQoLZDO> dictionary = new Dictionary<ServersideQoLZDO, ServersideQoLZDO>();
		foreach (ServersideQoLZDO item in signs ?? StorageSigns)
		{
			if (IsRememberedRequest(item) || IsRememberedInfo(item) || IsRememberedBoard(item) || IsRememberedStats(item) || item.ZDO.GetBool("ModerWarehouse.HelpChild", false))
			{
				continue;
			}
			ServersideQoLZDO.ZDOVars vars = item.Vars;
			string signText = vars.GetText("");
			if (IsBlankLabel(PlainText(signText)) || IsContainerSignsList(signText))
			{
				ServersideQoLZDO val = NearestContainer(item, containers, range2);
				if (val != null && !dictionary.ContainsKey(val))
				{
					dictionary.Add(val, item);
				}
			}
		}
		return dictionary;
	}

	private bool RemoveSupersededGeneratedLabels(Dictionary<ServersideQoLZDO, ServersideQoLZDO> blankSigns, List<ServersideQoLZDO> containers, float range2)
	{
		bool result = false;
		foreach (KeyValuePair<ServersideQoLZDO, ServersideQoLZDO> pair in blankSigns)
		{
			List<ServersideQoLZDO> list = StorageSigns.Where((ServersideQoLZDO sign) => sign != pair.Value && sign.IsModCreator() && sign.ZDO.GetBool("ModerWarehouse.AutoLabelManaged", false) && LinkedOrNearestContainer(sign, containers, range2) == pair.Key).ToList();
			foreach (ServersideQoLZDO item in list)
			{
				_signs.Remove(item);
				_storageSigns?.Remove(item);
				item.Destroy();
				result = true;
			}
			if (list.Count > 0)
			{
				this.Logger.LogInfo((object)$"Removed {list.Count} generated label(s) superseded by a blank sign at the same chest.");
			}
		}
		return result;
	}

	private HashSet<ServersideQoLZDO> DiscoverIgnored(List<ServersideQoLZDO> containers, float range2)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		HashSet<ServersideQoLZDO> hashSet = new HashSet<ServersideQoLZDO>();
		foreach (ServersideQoLZDO sign in _signs)
		{
			Regex ignoreMarker = IgnoreMarker;
			ServersideQoLZDO.ZDOVars vars = sign.Vars;
			if (ignoreMarker.IsMatch(PlainText(vars.GetText(""))))
			{
				ServersideQoLZDO val = NearestContainer(sign, containers, range2);
				if (val != null)
				{
					hashSet.Add(val);
				}
			}
		}
		return hashSet;
	}

	private SpatialLookup<ServersideQoLZDO> Lookup(List<ServersideQoLZDO> containers)
	{
		if (!_spatialLookups.TryGetValue(containers, out var value))
		{
			return _spatialLookups[containers] = new SpatialLookup<ServersideQoLZDO>(containers, (ServersideQoLZDO c) => c.ZDO.GetPosition().x, (ServersideQoLZDO c) => c.ZDO.GetPosition().y, (ServersideQoLZDO c) => c.ZDO.GetPosition().z);
		}
		return value;
	}

	private ServersideQoLZDO NearestContainer(ServersideQoLZDO sign, List<ServersideQoLZDO> containers, float range2)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		Vector3 position = sign.ZDO.GetPosition();
		return Lookup(containers).Nearest(position.x, position.y, position.z, range2);
	}

	private static bool LinkedInside(ServersideQoLZDO sign, WarehouseNode node)
	{
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = new Vector3(sign.ZDO.GetFloat("ModerWarehouse.AutoChestX", float.NaN), sign.ZDO.GetFloat("ModerWarehouse.AutoChestY", float.NaN), sign.ZDO.GetFloat("ModerWarehouse.AutoChestZ", float.NaN)) - node.Input.ZDO.GetPosition();
		return val.sqrMagnitude <= node.Radius * node.Radius;
	}

	private string CreateAutomaticLabel(ServersideQoLZDO chest, ItemData item, Dictionary<ServersideQoLZDO, ServersideQoLZDO> blankSigns, string language)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01be: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ee: Unknown result type (might be due to invalid IL or missing references)
		string text = LocalizedItemName(item, language);
		ServersideQoLZDO.ZDOVars vars;
		if (blankSigns.TryGetValue(chest, out var value))
		{
			if (!value.IsOwnerOrUnassigned())
			{
				value.ClaimOwnership();
			}
			vars = value.Vars;
			vars.SetText("<color=white>" + text);
			MarkAutomaticLabel(value, chest, item?.m_shared?.m_name);
			this.Logger.LogInfo((object)("Reused blank sign and labeled chest '" + text + "'."));
			_pendingLabels.Remove(chest);
			return text;
		}
		DateTime utcNow = WarehouseClock.UtcNow;
		string text2 = item?.m_shared?.m_name ?? string.Empty;
		if (!_pendingLabels.TryGetValue(chest, out var value2) || value2.ItemType != text2)
		{
			_pendingLabels[chest] = new PendingLabel(text2, utcNow);
			return null;
		}
		if ((utcNow - value2.Since).TotalSeconds < (double)Math.Max(5f, ConfigBase<WarehouseConfig>.Instance.AutoLabelDelay.Value))
		{
			return null;
		}
		Vector3 val = AutomaticLabelPosition(chest);
		ServersideQoLZDO val2 = this.PlacePiece(val, Prefabs.Sign, chest.ZDO.GetRotation(), (CreatorMarkers)2);
		if (val2 == null)
		{
			return null;
		}
		val2.Fields<Piece>().Set((Func<Expression<Func<Piece, bool>>>)(() => (Piece x) => x.m_canBeRemoved), true);
		vars = val2.Vars;
		vars.SetText("<color=white>" + text);
		Vector3 position = chest.ZDO.GetPosition();
		val2.ZDO.Set("ModerWarehouse.AutoChestX", position.x);
		val2.ZDO.Set("ModerWarehouse.AutoChestY", position.y);
		val2.ZDO.Set("ModerWarehouse.AutoChestZ", position.z);
		val2.ZDO.Set("ModerWarehouse.AutoLabelManaged", true);
		val2.ZDO.Set("ModerWarehouse.AutoLabelItem", item?.m_shared?.m_name ?? string.Empty);
		if (_signs.Add(val2))
		{
			val2.Destroyed += delegate(ServersideQoLZDO destroyed)
			{
				_signs.Remove(destroyed);
			};
		}
		_pendingLabels.Remove(chest);
		this.Logger.LogInfo((object)("Created persistent automatic label '" + text + "'."));
		return text;
	}

	private void RepairGeneratedLabelPlacement(List<ServersideQoLZDO> containers, float range2)
	{
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		foreach (ServersideQoLZDO item in StorageSigns.Where((ServersideQoLZDO x) => x.IsModCreator() && x.ZDO.GetBool("ModerWarehouse.AutoLabelManaged", false)).ToList())
		{
			ServersideQoLZDO val = LinkedOrNearestContainer(item, containers, range2);
			if (val == null || (_localStorage != null && !_localStorage.Contains(val)))
			{
				continue;
			}
			Vector3 val2 = AutomaticLabelPosition(val);
			Vector3 val3 = item.ZDO.GetPosition() - val2;
			if (val3.sqrMagnitude <= 0.0225f)
			{
				continue;
			}
			ServersideQoLZDO val4 = this.PlacePiece(val2, Prefabs.Sign, val.ZDO.GetRotation(), (CreatorMarkers)2);
			if (val4 == null)
			{
				continue;
			}
			val4.Fields<Piece>().Set((Func<Expression<Func<Piece, bool>>>)(() => (Piece x) => x.m_canBeRemoved), true);
			ServersideQoLZDO.ZDOVars vars = val4.Vars;
			ServersideQoLZDO.ZDOVars vars2 = item.Vars;
			vars.SetText(vars2.GetText("") ?? string.Empty);
			MarkAutomaticLabel(val4, val, item.ZDO.GetString("ModerWarehouse.AutoLabelItem", string.Empty));
			if (_signs.Add(val4))
			{
				val4.Destroyed += delegate(ServersideQoLZDO destroyed)
				{
					_signs.Remove(destroyed);
				};
			}
			_signs.Remove(item);
			_storageSigns?.Remove(item);
			_storageSigns?.Add(val4);
			item.Destroy();
			this.Logger.LogInfo((object)"Moved a generated storage label from the back to the decorated front of its chest.");
		}
	}

	private static Vector3 AutomaticLabelPosition(ServersideQoLZDO chest)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		return chest.ZDO.GetPosition() + chest.ZDO.GetRotation() * new Vector3(0f, 0.35f, 0.65f);
	}

	private static void MarkAutomaticLabel(ServersideQoLZDO sign, ServersideQoLZDO chest, string itemToken)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		Vector3 position = chest.ZDO.GetPosition();
		sign.ZDO.Set("ModerWarehouse.AutoChestX", position.x);
		sign.ZDO.Set("ModerWarehouse.AutoChestY", position.y);
		sign.ZDO.Set("ModerWarehouse.AutoChestZ", position.z);
		sign.ZDO.Set("ModerWarehouse.AutoLabelManaged", true);
		sign.ZDO.Set("ModerWarehouse.AutoLabelItem", itemToken ?? string.Empty);
	}

	private void RefreshAutomaticLabels(List<ServersideQoLZDO> containers, float range2, ContainerRegistryProcessor registry, string language)
	{
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_016f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		foreach (ServersideQoLZDO storageSign in StorageSigns)
		{
			string text = storageSign.ZDO.GetString("ModerWarehouse.AutoLabelItem", string.Empty);
			bool num = storageSign.ZDO.GetBool("ModerWarehouse.AutoLabelManaged", false);
			bool flag = !float.IsNaN(storageSign.ZDO.GetFloat("ModerWarehouse.AutoChestX", float.NaN));
			if (!num && !flag)
			{
				continue;
			}
			ServersideQoLZDO val = LinkedOrNearestContainer(storageSign, containers, range2);
			if (val == null || (_localStorage != null && !_localStorage.Contains(val)))
			{
				continue;
			}
			ServersideQoLZDO.ZDOVars vars;
			if (string.IsNullOrWhiteSpace(text))
			{
				IInventory val2 = InventoryFor(registry, val);
				text = ((val2 != null) ? ((IEnumerable<ItemData>)val2.Items).FirstOrDefault((Func<ItemData, bool>)((ItemData x) => x != null && x.m_stack > 0 && x.m_shared != null)) : null)?.m_shared?.m_name;
				if (string.IsNullOrWhiteSpace(text))
				{
					vars = storageSign.Vars;
					text = FindTokenByDisplayName(PlainText(vars.GetText("")));
				}
				if (string.IsNullOrWhiteSpace(text))
				{
					continue;
				}
				MarkAutomaticLabel(storageSign, val, text);
			}
			string text2 = LocalizedTokenName(text, language);
			string text3 = "<color=white>" + text2;
			vars = storageSign.Vars;
			if (!string.Equals(vars.GetText(""), text3, StringComparison.Ordinal))
			{
				if (!storageSign.IsOwnerOrUnassigned())
				{
					storageSign.ClaimOwnership();
				}
				vars = storageSign.Vars;
				vars.SetText(text3);
			}
		}
	}

	private static string FindTokenByDisplayName(string displayName)
	{
		string normalized = Normalize(displayName);
		return GermanDisplayNames.Concat(EnglishDisplayNames).Where(delegate(KeyValuePair<string, string> x)
		{
			KeyValuePair<string, string> keyValuePair = x;
			return Normalize(keyValuePair.Value) == normalized;
		}).Select(delegate(KeyValuePair<string, string> x)
		{
			KeyValuePair<string, string> keyValuePair = x;
			return keyValuePair.Key;
		})
			.FirstOrDefault();
	}

	private static string LocalizedTokenName(string token, string language)
	{
		Dictionary<string, string> dictionary = ((WarehouseLocalization.Resolve(language) == "de") ? GermanDisplayNames : EnglishDisplayNames);
		if (!string.IsNullOrWhiteSpace(token) && dictionary.TryGetValue(token, out var value) && !string.IsNullOrWhiteSpace(value))
		{
			return value;
		}
		if (!string.IsNullOrWhiteSpace(token) && PrefabNames.TryGetValue(token, out var value2) && !string.IsNullOrWhiteSpace(value2))
		{
			return value2;
		}
		return token ?? string.Empty;
	}

	private void LabelHomogeneousStores(List<ServersideQoLZDO> stores, Dictionary<ServersideQoLZDO, string> labels, Dictionary<ServersideQoLZDO, ServersideQoLZDO> blankSigns, ContainerRegistryProcessor registry, string language)
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		if (!ConfigBase<WarehouseConfig>.Instance.AutoCreateLabels.Value)
		{
			return;
		}
		foreach (ServersideQoLZDO store in stores)
		{
			if (!labels.ContainsKey(store))
			{
				ServersideQoLZDO.ZDOVars vars = store.Vars;
				if (!vars.GetInUse(false))
				{
					if (registry.GetState(store) == null)
					{
						continue;
					}
					List<ItemData> list = InventoryFor(registry, store).Items.Where((ItemData x) => x != null && x.m_stack > 0 && x.m_shared != null).ToList();
					if (list.Count == 0)
					{
						_pendingLabels.Remove(store);
						continue;
					}
					string itemType = list[0].m_shared.m_name;
					if (list.Any((ItemData x) => x.m_shared.m_name != itemType))
					{
						_pendingLabels.Remove(store);
						continue;
					}
					string value = CreateAutomaticLabel(store, list[0], blankSigns, language);
					if (!string.IsNullOrEmpty(value))
					{
						labels[store] = value;
					}
					continue;
				}
			}
			_pendingLabels.Remove(store);
		}
	}

	private bool IsPrivateContainer(ServersideQoLZDO chest)
	{
		return WarehouseContainerPolicy.IsPrivatePrefab(Processor.GetPrefabInfo(chest).PrefabName);
	}

	private bool CanManageContainer(ServersideQoLZDO chest, HashSet<ServersideQoLZDO> endpoints, HashSet<ServersideQoLZDO> ignored, bool hasStorageSign)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		bool endpoint = endpoints.Contains(chest);
		bool ignored2 = ignored.Contains(chest);
		bool isPrivate = IsPrivateContainer(chest);
		ServersideQoLZDO.ZDOVars vars = chest.Vars;
		return WarehouseContainerPolicy.CanManage(endpoint, ignored2, isPrivate, vars.GetInUse(false), hasStorageSign);
	}

	private void WhitenWarehouseLabels(List<ServersideQoLZDO> managed, List<ServersideQoLZDO> containers, float range2)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		HashSet<ServersideQoLZDO> hashSet = new HashSet<ServersideQoLZDO>(managed);
		foreach (ServersideQoLZDO storageSign in StorageSigns)
		{
			if (IsRememberedInfo(storageSign) || IsRememberedBoard(storageSign) || IsRememberedStats(storageSign))
			{
				continue;
			}
			ServersideQoLZDO.ZDOVars vars = storageSign.Vars;
			string text = PlainText(vars.GetText(""));
			if (!IsBlankLabel(text) && !DeleteMarker.IsMatch(text) && !CleanMarker.IsMatch(text))
			{
				ServersideQoLZDO val = NearestContainer(storageSign, containers, range2);
				if (val != null && hashSet.Contains(val))
				{
					WriteWhite(storageSign, text);
				}
			}
		}
	}

	private static void WriteWhite(ServersideQoLZDO sign, string plain)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		ServersideQoLZDO.ZDOVars vars = sign.Vars;
		if (!(vars.GetText("") ?? string.Empty).StartsWith("<color=white>", StringComparison.OrdinalIgnoreCase))
		{
			if (!sign.IsOwnerOrUnassigned())
			{
				sign.ClaimOwnership();
			}
			vars = sign.Vars;
			vars.SetText("<color=white>" + plain);
		}
	}

	private static bool IsSingleItemType(IInventory inventory, ItemData incoming)
	{
		if (inventory != null)
		{
			if (inventory.Items.Count != 0)
			{
				return inventory.Items.All((ItemData x) => x?.m_shared?.m_name == incoming?.m_shared?.m_name);
			}
			return true;
		}
		return false;
	}

	private static bool ContainsOnlyWarehouseItems(IInventory inventory)
	{
		if (inventory != null)
		{
			return inventory.Items.All((ItemData item) => item == null || item.m_stack <= 0 || IsWarehouseItem(item));
		}
		return false;
	}

	private int CorrectMisfiledItems(string warehouseId, List<ServersideQoLZDO> stores, Dictionary<ServersideQoLZDO, string> labels, ContainerRegistryProcessor registry, ref int moves)
	{
		int num = 0;
		foreach (ServersideQoLZDO sourceZdo in stores.ToList())
		{
			if (moves <= 0 || !labels.TryGetValue(sourceZdo, out var value))
			{
				continue;
			}
			ContainerState state = registry.GetState(sourceZdo);
			IInventory val = InventoryFor(registry, sourceZdo);
			if (val == null)
			{
				continue;
			}
			foreach (ItemData item in val.Items.ToList())
			{
				if (moves <= 0)
				{
					break;
				}
				if (item == null || item.m_stack <= 0 || !IsWarehouseItem(item) || LabelMatches(sourceZdo, value, item, registry))
				{
					continue;
				}
				if (!Own(state))
				{
					break;
				}
				int num2 = item.m_stack;
				string value3;
				foreach (ServersideQoLZDO item2 in (from targetZdo in stores
					where targetZdo != sourceZdo
					where labels.TryGetValue(targetZdo, out value3) ? LabelMatches(targetZdo, value3, item, registry) : IsSingleItemType(InventoryFor(registry, targetZdo), item)
					select targetZdo).OrderByDescending(delegate(ServersideQoLZDO targetZdo)
				{
					if (HasItem(InventoryFor(registry, targetZdo), item))
					{
						return 2;
					}
					return (labels.TryGetValue(targetZdo, out value3) && LabelMatches(targetZdo, value3, item, registry)) ? 1 : 0;
				}).ThenBy((ServersideQoLZDO targetZdo) => Distance2(targetZdo, sourceZdo)))
				{
					if (moves <= 0 || num2 <= 0)
					{
						break;
					}
					ContainerState state2 = registry.GetState(item2);
					if (state2 != null && Own(state2))
					{
						int num3 = Move(val, InventoryFor(registry, item2), item, num2);
						if (num3 > 0)
						{
							string value2;
							string text = (labels.TryGetValue(item2, out value2) ? PlainText(value2).Trim() : "(free/unlabelled)");
							this.Logger.LogInfo((object)($"Warehouse '{warehouseId}' janitor moved {num3} x " + "'" + item.m_shared.m_name + "' from '" + PlainText(value).Trim() + "' to '" + text + "'."));
							num += num3;
							num2 -= num3;
							moves--;
						}
					}
				}
			}
		}
		return num;
	}

	private int SanitizeForbiddenItems(IEnumerable<ServersideQoLZDO> targets, ContainerRegistryProcessor registry, List<ServersideQoLZDO> allContainers, float markerRange2)
	{
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
		int num = 0;
		foreach (ServersideQoLZDO chest in targets.Where((ServersideQoLZDO x) => x != null).Distinct())
		{
			ContainerState state = registry.GetState(chest);
			IInventory val = InventoryFor(registry, chest);
			if (val == null)
			{
				continue;
			}
			ServersideQoLZDO.ZDOVars vars = chest.Vars;
			if (vars.GetInUse(false))
			{
				continue;
			}
			List<ItemData> list = val.Items.Where((ItemData item) => item != null && item.m_stack > 0 && !IsWarehouseItem(item)).ToList();
			if (list.Count == 0 || !Own(state))
			{
				continue;
			}
			int num2 = 0;
			foreach (ItemData item in list)
			{
				int stack = item.m_stack;
				if (val.Inventory.RemoveItem(item, stack))
				{
					num2 += stack;
				}
			}
			if (num2 == 0)
			{
				continue;
			}
			val.Save();
			num += num2;
			if (val.Items.Any((ItemData item) => item != null && item.m_stack > 0))
			{
				continue;
			}
			foreach (ServersideQoLZDO item2 in StorageSigns.Where((ServersideQoLZDO sign) => sign.ZDO.GetBool("ModerWarehouse.AutoLabelManaged", false) && LinkedOrNearestContainer(sign, allContainers, markerRange2) == chest))
			{
				if (!item2.IsOwnerOrUnassigned())
				{
					item2.ClaimOwnership();
				}
				item2.ZDO.Set("ModerWarehouse.AutoLabelItem", string.Empty);
				vars = item2.Vars;
				vars.SetText("<color=white>-");
			}
			_pendingLabels.Remove(chest);
		}
		return num;
	}

	private static bool IsWarehouseItem(ItemData item)
	{
		if (item?.m_shared == null || item.m_cheated)
		{
			return false;
		}
		string text = item.m_shared.m_name ?? string.Empty;
		if (text.StartsWith("$item_", StringComparison.OrdinalIgnoreCase) || text.StartsWith("$animal_fish", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		return string.Equals(item.m_shared.m_itemType.ToString(), "Trophy", StringComparison.OrdinalIgnoreCase);
	}

	private static string LocalizedItemName(ItemData item, string language)
	{
		string text = item?.m_shared?.m_name;
		Dictionary<string, string> dictionary = ((WarehouseLocalization.Resolve(language) == "de") ? GermanDisplayNames : EnglishDisplayNames);
		if (string.IsNullOrEmpty(text) || !dictionary.TryGetValue(text, out var value))
		{
			return ItemName(item);
		}
		return value;
	}

	private void ProcessRequest(string id, string language, Request request, ServersideQoLZDO outputZdo, List<ServersideQoLZDO> stores, ContainerRegistryProcessor registry, WarehouseStatsHistory stats, ref int moves, List<WarehouseNode> network, List<ServersideQoLZDO> containers, float markerRange2)
	{
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		if (string.IsNullOrWhiteSpace(request.Payload) || IsStatus(request.Payload) || IsRequestPrompt(request.Payload) || request.Payload.StartsWith("SOLL ", StringComparison.OrdinalIgnoreCase) || string.Equals(request.Payload, request.Sign.ZDO.GetString("ModerWarehouse.RequestRenderedText", string.Empty), StringComparison.Ordinal))
		{
			return;
		}
		ServersideQoLZDO.ZDOVars vars = outputZdo.Vars;
		if (vars.GetInUse(false))
		{
			return;
		}
		ContainerState state = registry.GetState(outputZdo);
		if (state == null || !Own(state))
		{
			return;
		}
		IInventory output = InventoryFor(registry, outputZdo);
		bool worldRead = false;
		Action action = delegate
		{
			if (!worldRead)
			{
				RefreshWorldStockIndex(network, containers, markerRange2, registry);
				worldRead = true;
			}
		};
		string[] array = (from x in request.Payload.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries)
			select x.Trim() into x
			where x.Length > 0
			select x).Take(10).ToArray();
		if (array.Length == 0)
		{
			WriteStatus(id, request.Sign, WarehouseLocalization.Text(language, "request.error_example"));
			return;
		}
		List<string> list = new List<string>();
		Dictionary<string, int> craftReserved = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		string[] array2 = array;
		foreach (string text in array2)
		{
			if (TryParseCraftRequest(text, out var craftQuery, out var craftCount, out var craftLevel))
			{
				list.Add(ProcessCraftRequest(id, language, craftQuery, craftCount, craftLevel, output, stores, registry, stats, ref moves, action, network, containers, markerRange2, craftReserved));
				continue;
			}
			if (text.IndexOf('*') >= 0)
			{
				list.Add(ProcessWildcardRequest(id, language, text, output, stores, registry, stats, ref moves));
				continue;
			}
			if (!TryParseRequest(text, out var query, out var amount))
			{
				action();
				list.Add(BuildRequestSuggestions(id, language, text));
				continue;
			}
			int locallyAvailable;
			string text2 = ProcessSingleRequest(id, language, query, amount, output, stores, registry, stats, ref moves, out locallyAvailable);
			if (locallyAvailable < amount)
			{
				action();
				List<string> list2 = FindRemoteStock(id, query, network, containers, markerRange2, registry);
				if (list2.Count > 0)
				{
					text2 = text2 + "\n" + string.Join(" | ", list2);
				}
			}
			if (locallyAvailable == 0)
			{
				string text3 = BuildRequestSuggestions(id, language, query);
				if (!string.IsNullOrWhiteSpace(text3))
				{
					text2 = text2 + "\n" + text3;
				}
			}
			list.Add(text2);
		}
		WriteStatus(id, request.Sign, string.Join("\n", list));
		_requestClearAt[request.Sign] = WarehouseClock.UtcNow.AddSeconds(RequestDisplayPolicy.ClampResultSeconds(ConfigBase<WarehouseConfig>.Instance.RequestResultSeconds.Value));
	}

	private string ProcessSingleRequest(string id, string language, string query, int amount, IInventory output, List<ServersideQoLZDO> stores, ContainerRegistryProcessor registry, WarehouseStatsHistory stats, ref int moves, out int locallyAvailable)
	{
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		int num = amount;
		int num2 = 0;
		string text = query;
		locallyAvailable = (from item in stores.SelectMany(delegate(ServersideQoLZDO store)
			{
				IInventory val2 = InventoryFor(registry, store);
				IEnumerable<ItemData> enumerable = ((val2 != null) ? val2.Items : null);
				return enumerable ?? Enumerable.Empty<ItemData>();
			})
			where item != null && item.m_stack > 0 && ItemMatchesQuery(item, query)
			select item).Sum((ItemData item) => item.m_stack);
		bool flag = locallyAvailable > 0;
		foreach (ServersideQoLZDO store in stores)
		{
			if (moves <= 0 || num <= 0)
			{
				break;
			}
			ServersideQoLZDO.ZDOVars vars = store.Vars;
			if (vars.GetInUse(false))
			{
				continue;
			}
			ContainerState state = registry.GetState(store);
			if (state == null || !Own(state))
			{
				continue;
			}
			IInventory val = InventoryFor(registry, store);
			foreach (ItemData item in val.Items.Where((ItemData x) => ItemMatchesQuery(x, query)).ToList())
			{
				if (moves <= 0 || num <= 0)
				{
					break;
				}
				flag = true;
				text = LocalizedItemName(item, language);
				int num3 = Move(val, output, item, num);
				if (num3 > 0)
				{
					num2 += num3;
					num -= num3;
					moves--;
					stats?.AddOutput(item.m_shared.m_name, num3);
				}
			}
		}
		string result = ((num2 == amount) ? WarehouseLocalization.Format(language, "request.done", text, num2) : ((num2 > 0) ? WarehouseLocalization.Format(language, "request.partial", text, num2, amount) : (flag ? WarehouseLocalization.Format(language, "request.output_full", text) : WarehouseLocalization.Format(language, "request.not_found", query))));
		this.Logger.LogInfo((object)($"Warehouse '{id}': withdrawal '{query}' moved {num2}/{amount} item(s)." + ((locallyAvailable < amount) ? $" Local stock {locallyAvailable}/{amount}; informational world lookup may follow." : string.Empty)));
		return result;
	}

	private List<string> FindRemoteStock(string localId, string query, List<WarehouseNode> network, List<ServersideQoLZDO> containers, float markerRange2, ContainerRegistryProcessor registry)
	{
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, Dictionary<string, int>> item in _warehouseStockSnapshots.Where((KeyValuePair<string, Dictionary<string, int>> pair) => !pair.Key.Equals(localId, StringComparison.OrdinalIgnoreCase)).OrderBy<KeyValuePair<string, Dictionary<string, int>>, string>((KeyValuePair<string, Dictionary<string, int>> pair) => pair.Key, StringComparer.OrdinalIgnoreCase))
		{
			int num = item.Value.Where((KeyValuePair<string, int> stock) => TokenMatchesQuery(stock.Key, query)).Sum((KeyValuePair<string, int> stock) => stock.Value);
			if (num > 0)
			{
				list.Add(item.Key.ToUpperInvariant() + " " + FormatAmount(num) + " " + StockStatus(item.Key, LanguageForWarehouse(localId)));
			}
		}
		return list;
	}

	private static bool TokenMatchesQuery(string token, string query)
	{
		string normalized = Normalize(query);
		if (string.IsNullOrWhiteSpace(normalized))
		{
			return false;
		}
		return new string[3]
		{
			token,
			LocalizedTokenName(token, "de"),
			LocalizedTokenName(token, "en")
		}.Select(Normalize).Any((string name) => name == normalized);
	}

	private string BuildRequestSuggestions(string localId, string language, string query)
	{
		string text = Normalize(query);
		if (string.IsNullOrWhiteSpace(text))
		{
			return WarehouseLocalization.Text(language, "request.error_example");
		}
		List<RequestSuggestion> list = new List<RequestSuggestion>();
		foreach (KeyValuePair<string, Dictionary<string, int>> warehouseStockSnapshot in _warehouseStockSnapshots)
		{
			foreach (KeyValuePair<string, int> stock in warehouseStockSnapshot.Value)
			{
				string text2 = LocalizedTokenName(stock.Key, language);
				int num = SuggestionScore(text, stock.Key, text2);
				if (num >= 0)
				{
					RequestSuggestion requestSuggestion = list.FirstOrDefault((RequestSuggestion candidate) => candidate.Token.Equals(stock.Key, StringComparison.Ordinal));
					if (requestSuggestion == null)
					{
						requestSuggestion = new RequestSuggestion
						{
							Token = stock.Key,
							Name = text2,
							Score = num
						};
						list.Add(requestSuggestion);
					}
					requestSuggestion.Score = Math.Min(requestSuggestion.Score, num);
					requestSuggestion.Amounts[warehouseStockSnapshot.Key] = stock.Value;
					requestSuggestion.Statuses[warehouseStockSnapshot.Key] = StockStatus(warehouseStockSnapshot.Key, language);
				}
			}
		}
		IEnumerable<RequestSuggestion> source = (list.Any((RequestSuggestion candidate) => candidate.Score <= 1) ? list.Where((RequestSuggestion candidate) => candidate.Score <= 1) : list);
		List<RequestSuggestion> list2 = source.OrderBy((RequestSuggestion candidate) => candidate.Score).ThenBy<RequestSuggestion, string>((RequestSuggestion candidate) => candidate.Name, StringComparer.CurrentCultureIgnoreCase).Take(36)
			.ToList();
		if (list2.Count == 0)
		{
			return WarehouseLocalization.Format(language, "request.not_found", query);
		}
		int num2 = Math.Max(1, (list2.Count + 12 - 1) / 12);
		double value = ((num2 == 1) ? 10.0 : ((double)num2 * 8.0 * 3.0));
		_requestSearchNotices[localId] = new RequestSearchNotice(query, list2, WarehouseClock.UtcNow, WarehouseClock.UtcNow.AddSeconds(value));
		return WarehouseLocalization.Format(language, "request.shown_on_board", query, list2.Count);
	}

	private static readonly HashSet<IInventory> PendingInventorySaves = new HashSet<IInventory>();

	private void FlushInventorySaves()
	{
		if (PendingInventorySaves.Count == 0)
		{
			return;
		}
		List<IInventory> list = PendingInventorySaves.ToList();
		PendingInventorySaves.Clear();
		foreach (IInventory item in list)
		{
			try
			{
				item.Save();
			}
			catch (Exception ex)
			{
				this.Logger.LogWarning((object)("Deferred chest save failed: " + ex.Message));
			}
		}
	}

	private void RefreshWorldStockIndex(List<WarehouseNode> network, List<ServersideQoLZDO> containers, float markerRange2, ContainerRegistryProcessor registry)
	{
		// The world index decodes chest contents from saved ZDO data, so pending moves must be written first.
		FlushInventorySaves();
		_freshlyReadWarehouses.Clear();
		if (RefreshPersistedWorldStockIndex(markerRange2))
		{
			SaveStockCache();
		}
	}

	private bool RefreshPersistedWorldStockIndex(float markerRange2)
	{
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Expected O, but got Unknown
		Stopwatch stopwatch = Stopwatch.StartNew();
		long num = 0L;
		long num2 = 0L;
		long num3 = 0L;
		try
		{
			if (!(typeof(ZDOMan).GetField("m_objectsByID", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(ZDOMan.instance) is IDictionary dictionary))
			{
				return false;
			}
			List<ZDO> list = new List<ZDO>(dictionary.Count);
			List<PersistedSign> list2 = new List<PersistedSign>();
			foreach (DictionaryEntry item in dictionary)
			{
				object value = item.Value;
				ZDO val = (ZDO)((value is ZDO) ? value : null);
				// Local patch 7: classify by cached prefab first. Only chests feed the spatial index and
				// only signs have their text read, instead of every object in the world.
				if (val == null)
				{
					continue;
				}
				if (IsContainerPrefab(val.GetPrefab()))
				{
					list.Add(val);
				}
				else if (IsSignObject(val))
				{
					string value2 = val.GetString(ZDOVars.s_text, string.Empty);
					if (!string.IsNullOrWhiteSpace(value2))
					{
						list2.Add(new PersistedSign(val, PlainText(value2)));
					}
				}
			}
			num = stopwatch.ElapsedMilliseconds;
			PersistedContainerSpatialLookup persistedContainerSpatialLookup = new PersistedContainerSpatialLookup(list);
			List<PersistedWarehouse> list3 = new List<PersistedWarehouse>();
			foreach (PersistedSign item2 in list2)
			{
				Match match = Marker.Match(item2.Text);
				if (match.Success && match.Groups["role"].Value.Equals("IN", StringComparison.OrdinalIgnoreCase))
				{
					ZDO val2 = persistedContainerSpatialLookup.Nearest(item2.Zdo, markerRange2);
					if (val2 != null)
					{
						list3.Add(new PersistedWarehouse(match.Groups["id"].Value.Trim(), item2.Zdo, val2, ParseInputRadius(match.Groups["payload"].Value)));
					}
				}
			}
			if (list3.Count == 0)
			{
				return false;
			}
			HashSet<ZDO> endpoints = new HashSet<ZDO>();
			HashSet<ZDO> ignored = new HashSet<ZDO>();
			foreach (PersistedSign item3 in list2)
			{
				Match match2 = Marker.Match(item3.Text);
				bool flag = match2.Success && (match2.Groups["role"].Value.Equals("IN", StringComparison.OrdinalIgnoreCase) || match2.Groups["role"].Value.Equals("OUT", StringComparison.OrdinalIgnoreCase));
				bool flag2 = IgnoreMarker.IsMatch(item3.Text);
				if (!flag && !flag2)
				{
					continue;
				}
				ZDO val3 = persistedContainerSpatialLookup.Nearest(item3.Zdo, markerRange2);
				if (val3 != null)
				{
					if (flag)
					{
						endpoints.Add(val3);
					}
					if (flag2)
					{
						ignored.Add(val3);
					}
				}
			}
			float range = ConfigBase<WarehouseConfig>.Instance.LabelDistance.Value * ConfigBase<WarehouseConfig>.Instance.LabelDistance.Value;
			HashSet<ZDO> hashSet = new HashSet<ZDO>();
			foreach (PersistedSign item4 in list2)
			{
				string input = item4.Text ?? string.Empty;
				if (!Marker.IsMatch(input) && !IgnoreMarker.IsMatch(input) && !DeleteMarker.IsMatch(input) && !CleanMarker.IsMatch(input) && !item4.Zdo.GetBool("ModerWarehouse.HelpChild", false) && string.IsNullOrWhiteSpace(item4.Zdo.GetString("ModerWarehouse.RequestWarehouse", string.Empty)) && string.IsNullOrWhiteSpace(item4.Zdo.GetString("ModerWarehouse.InfoWarehouse", string.Empty)) && string.IsNullOrWhiteSpace(item4.Zdo.GetString("ModerWarehouse.BoardWarehouse", string.Empty)) && string.IsNullOrWhiteSpace(item4.Zdo.GetString("ModerWarehouse.StatsWarehouse", string.Empty)))
				{
					ZDO val4 = persistedContainerSpatialLookup.Nearest(item4.Zdo, range);
					if (val4 != null)
					{
						hashSet.Add(val4);
					}
				}
			}
			num2 = stopwatch.ElapsedMilliseconds - num;
			string value4;
			List<PersistedWarehouse> validNodes = (from @group in list3.Where((PersistedWarehouse candidate) => !_globalInputErrors.TryGetValue(candidate.Sign.m_uid.ToString(), out value4) || string.IsNullOrWhiteSpace(value4)).GroupBy<PersistedWarehouse, string>((PersistedWarehouse candidate) => candidate.Id, StringComparer.OrdinalIgnoreCase)
				select @group.First()).ToList();
			HashSet<ZDO> eligibleStorage = new HashSet<ZDO>(hashSet.Where((ZDO chest) => !endpoints.Contains(chest) && !ignored.Contains(chest) && validNodes.Any(delegate(PersistedWarehouse persistedWarehouse)
			{
				//IL_0006: Unknown result type (might be due to invalid IL or missing references)
				//IL_0011: Unknown result type (might be due to invalid IL or missing references)
				//IL_0016: Unknown result type (might be due to invalid IL or missing references)
				//IL_001b: Unknown result type (might be due to invalid IL or missing references)
				Vector3 val5 = chest.GetPosition() - persistedWarehouse.Input.GetPosition();
				return val5.sqrMagnitude <= persistedWarehouse.Radius * persistedWarehouse.Radius;
			})));
			List<PersistedContainer> list4 = new List<PersistedContainer>(eligibleStorage.Count);
			foreach (ZDO item5 in eligibleStorage)
			{
				byte[] byteArray = item5.GetByteArray(ZDOVars.s_items, (byte[])null);
				if (byteArray != null && byteArray.Length != 0)
				{
					PersistedContainer persistedContainer = ReadPersistedContainer(item5, byteArray);
					if (persistedContainer != null)
					{
						list4.Add(persistedContainer);
					}
				}
			}
			num3 = stopwatch.ElapsedMilliseconds - num - num2;
			bool result = false;
			DateTime utcNow = WarehouseClock.UtcNow;
			foreach (PersistedWarehouse node in validNodes)
			{
				float radius2 = node.Radius * node.Radius;
				Dictionary<string, int> dictionary2 = (from item in list4.Where(delegate(PersistedContainer container)
					{
						//IL_001e: Unknown result type (might be due to invalid IL or missing references)
						//IL_002e: Unknown result type (might be due to invalid IL or missing references)
						//IL_0033: Unknown result type (might be due to invalid IL or missing references)
						//IL_0038: Unknown result type (might be due to invalid IL or missing references)
						if (eligibleStorage.Contains(container.Zdo))
						{
							Vector3 val5 = container.Zdo.GetPosition() - node.Input.GetPosition();
							return val5.sqrMagnitude <= radius2;
						}
						return false;
					}).SelectMany((PersistedContainer container) => container.Items)
					where item != null && item.m_stack > 0 && item.m_shared != null && IsWarehouseItem(item)
					group item by item.m_shared.m_name).ToDictionary<IGrouping<string, ItemData>, string, int>((IGrouping<string, ItemData> group) => group.Key, (IGrouping<string, ItemData> group) => group.Sum((ItemData item) => item.m_stack), StringComparer.Ordinal);
				if (!_warehouseStockSnapshots.TryGetValue(node.Id, out var value3) || !StockEqual(value3, dictionary2))
				{
					_warehouseStockSnapshots[node.Id] = dictionary2;
					result = true;
				}
				_warehouseStockTimes[node.Id] = utcNow;
				_freshlyReadWarehouses.Add(node.Id);
				result = true;
			}
			stopwatch.Stop();
			if (stopwatch.ElapsedMilliseconds >= 100)
			{
				this.Logger.LogInfo((object)($"Global read-only stock query: {list3.Count} warehouse(s), " + $"{list4.Count}/{eligibleStorage.Count} eligible stocked container(s), " + $"{list.Count} world objects, {list2.Count} signs; " + $"{stopwatch.ElapsedMilliseconds} ms (scan {num}, " + $"signs {num2}, inventory decode {num3})."));
			}
			return result;
		}
		catch (Exception ex)
		{
			this.Logger.LogWarning((object)("Global read-only stock query failed; using indexed/last-known data. " + ex.Message));
			return false;
		}
	}

	private static PersistedContainer ReadPersistedContainer(ZDO zdo, byte[] data)
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Expected O, but got Unknown
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Expected O, but got Unknown
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Expected O, but got Unknown
		ZNetScene instance = ZNetScene.instance;
		GameObject val = (((UnityEngine.Object)(object)instance != (UnityEngine.Object)null) ? instance.GetPrefab(zdo.GetPrefab()) : null);
		Container val2 = (((UnityEngine.Object)(object)val != (UnityEngine.Object)null) ? val.GetComponent<Container>() : null);
		if ((UnityEngine.Object)val2 == (UnityEngine.Object)null)
		{
			return null;
		}
		string text = ((UnityEngine.Object)val).name.ToLowerInvariant();
		if (text.Contains("chest_private") || text.Contains("personal"))
		{
			return null;
		}
		Inventory val3 = new Inventory(val2.m_name, val2.m_bkg, val2.m_width, val2.m_height);
		val3.Load(new ZPackage(data));
		return new PersistedContainer(zdo, val3.GetAllItems());
	}

	private string StockStatus(string warehouseId, string language)
	{
		if (_freshlyReadWarehouses.Contains(warehouseId))
		{
			return WarehouseLocalization.Text(language, "request.stock_live");
		}
		if (!_warehouseStockTimes.TryGetValue(warehouseId, out var value))
		{
			return WarehouseLocalization.Text(language, "request.stock_unknown");
		}
		string text = ((value.ToLocalTime().Date == DateTime.Now.Date) ? value.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture) : value.ToLocalTime().ToString("dd.MM HH:mm", CultureInfo.CurrentCulture));
		return WarehouseLocalization.Format(language, "request.stock_seen", text);
	}

	private static int SuggestionScore(string query, string token, string display)
	{
		string[] source = new string[4]
		{
			Normalize(token),
			Normalize(display),
			Normalize(LocalizedTokenName(token, "de")),
			Normalize(LocalizedTokenName(token, "en"))
		};
		int num = int.MaxValue;
		foreach (string item in source.Where((string value) => !string.IsNullOrWhiteSpace(value)).Distinct())
		{
			if (item == query)
			{
				return 0;
			}
			if (item.Contains(query) || query.Contains(item))
			{
				num = Math.Min(num, 1);
			}
			int num2 = BestSubstringDistance(query, item);
			if (num2 <= 1)
			{
				num = Math.Min(num, 2 + num2);
			}
		}
		if (num != int.MaxValue)
		{
			return num;
		}
		return -1;
	}

	internal static int BestSubstringDistance(string query, string name)
	{
		if (name.Length <= query.Length)
		{
			return DamerauDistance(query, name);
		}
		int num = int.MaxValue;
		int num2 = Math.Max(1, query.Length - 1);
		int num3 = Math.Min(name.Length, query.Length + 1);
		for (int i = num2; i <= num3; i++)
		{
			for (int j = 0; j + i <= name.Length; j++)
			{
				num = Math.Min(num, DamerauDistance(query, name.Substring(j, i)));
			}
		}
		return num;
	}

	internal static int DamerauDistance(string left, string right)
	{
		if (string.IsNullOrEmpty(left))
		{
			return right?.Length ?? 0;
		}
		if (string.IsNullOrEmpty(right))
		{
			return left.Length;
		}
		int[,] array = new int[left.Length + 1, right.Length + 1];
		for (int i = 0; i <= left.Length; i++)
		{
			array[i, 0] = i;
		}
		for (int j = 0; j <= right.Length; j++)
		{
			array[0, j] = j;
		}
		for (int k = 1; k <= left.Length; k++)
		{
			for (int l = 1; l <= right.Length; l++)
			{
				int num = ((left[k - 1] != right[l - 1]) ? 1 : 0);
				array[k, l] = Math.Min(Math.Min(array[k - 1, l] + 1, array[k, l - 1] + 1), array[k - 1, l - 1] + num);
				if (k > 1 && l > 1 && left[k - 1] == right[l - 2] && left[k - 2] == right[l - 1])
				{
					array[k, l] = Math.Min(array[k, l], array[k - 2, l - 2] + 1);
				}
			}
		}
		return array[left.Length, right.Length];
	}

	internal static int LevenshteinDistance(string left, string right)
	{
		if (string.IsNullOrEmpty(left))
		{
			return right?.Length ?? 0;
		}
		if (string.IsNullOrEmpty(right))
		{
			return left.Length;
		}
		int[] array = Enumerable.Range(0, right.Length + 1).ToArray();
		int[] array2 = new int[right.Length + 1];
		for (int i = 1; i <= left.Length; i++)
		{
			array2[0] = i;
			for (int j = 1; j <= right.Length; j++)
			{
				array2[j] = Math.Min(Math.Min(array2[j - 1] + 1, array[j] + 1), array[j - 1] + ((left[i - 1] != right[j - 1]) ? 1 : 0));
			}
			int[] array3 = array;
			array = array2;
			array2 = array3;
		}
		return array[right.Length];
	}

	internal static bool TryParseRequest(string payload, out string query, out int amount)
	{
		query = string.Empty;
		amount = 0;
		Match match = Regex.Match(payload.Replace("\r", string.Empty).Split(new char[1] { '\n' })[0].Trim(), "^(?<item>.+?)\\s+(?:x)?(?<amount>\\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		if (!match.Success || !int.TryParse(match.Groups["amount"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out amount) || amount <= 0 || amount > 10000)
		{
			return false;
		}
		query = match.Groups["item"].Value.Trim();
		return query.Length > 0;
	}

	// Local patch 4: "craft <item> [lvl N] [xN]" pulls the recipe's materials into OUT,
	// counting what OUT already holds so only the missing amount is withdrawn.
	internal static bool TryParseCraftRequest(string payload, out string query, out int count, out int level)
	{
		query = string.Empty;
		count = 1;
		level = 1;
		Match match = Regex.Match(payload.Replace("\r", string.Empty).Split(new char[1] { '\n' })[0].Trim(), "^(?:craft|herstellen)\\s+(?<item>.+?)(?:\\s+(?:lvl|level|stufe|q)\\s*(?<level>\\d+))?(?:\\s+x?(?<count>\\d+))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		if (!match.Success)
		{
			return false;
		}
		if (match.Groups["count"].Success && (!int.TryParse(match.Groups["count"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out count) || count <= 0 || count > 1000))
		{
			return false;
		}
		if (match.Groups["level"].Success && (!int.TryParse(match.Groups["level"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out level) || level <= 0 || level > 10))
		{
			return false;
		}
		query = match.Groups["item"].Value.Trim();
		return query.Length > 0;
	}

	private static readonly Dictionary<string, string> PieceEnglishNames = new Dictionary<string, string>();

	private static readonly Dictionary<string, string> PieceGermanNames = new Dictionary<string, string>();

	private sealed class CraftTarget
	{
		public string Name;

		public string Token;

		public ItemData Item;

		public Recipe Recipe;

		public Piece.Requirement[] Resources;

		public int MaxLevel;

		public int Priority;

		public HashSet<string> Aliases;

		public string DisplayName(string language)
		{
			if (Item != null)
			{
				return LocalizedItemName(Item, language);
			}
			Dictionary<string, string> dictionary = ((WarehouseLocalization.Resolve(language) == "de") ? PieceGermanNames : PieceEnglishNames);
			if (!dictionary.TryGetValue(Token ?? string.Empty, out var value) || string.IsNullOrWhiteSpace(value))
			{
				return Name;
			}
			return value;
		}
	}

	private static IEnumerable<Piece> BuildPieces()
	{
		return (from item in ObjectDB.instance?.m_items ?? new List<GameObject>()
			select ((UnityEngine.Object)(object)item != (UnityEngine.Object)null) ? item.GetComponent<ItemDrop>() : null into drop
			select drop?.m_itemData?.m_shared?.m_buildPieces into table
			where (UnityEngine.Object)(object)table != (UnityEngine.Object)null
			from go in table.m_pieces
			where (UnityEngine.Object)(object)go != (UnityEngine.Object)null
			select go.GetComponent<Piece>() into piece
			where (UnityEngine.Object)(object)piece != (UnityEngine.Object)null && piece.m_enabled && piece.m_resources != null && piece.m_resources.Any((Piece.Requirement x) => (UnityEngine.Object)(object)x?.m_resItem != (UnityEngine.Object)null)
			select piece).Distinct();
	}

	private static void CapturePieceNames(Localization localization, Dictionary<string, string> displayNames)
	{
		foreach (Piece item in BuildPieces())
		{
			if (!string.IsNullOrEmpty(item.m_name))
			{
				displayNames[item.m_name] = localization.Localize(item.m_name);
			}
		}
	}

	private static List<CraftTarget> CraftTargets()
	{
		List<CraftTarget> list = CraftableRecipes().Select((Recipe r) => new CraftTarget
		{
			Name = ((UnityEngine.Object)r).name,
			Token = r.m_item.m_itemData.m_shared.m_name,
			Item = r.m_item.m_itemData,
			Recipe = r,
			Resources = r.m_resources,
			MaxLevel = Math.Max(1, r.m_item.m_itemData.m_shared.m_maxQuality),
			Priority = ((r.m_amount != 1) ? 1 : 0),
			Aliases = new HashSet<string>(RecipeAliases(r).Where((string x) => x.Length > 0))
		}).ToList();
		foreach (Piece item in BuildPieces())
		{
			HashSet<string> hashSet = new HashSet<string>
			{
				Normalize(((UnityEngine.Object)item).name),
				Normalize(item.m_name?.Replace("$piece_", string.Empty))
			};
			if (PieceEnglishNames.TryGetValue(item.m_name ?? string.Empty, out var value))
			{
				hashSet.Add(Normalize(value));
			}
			if (PieceGermanNames.TryGetValue(item.m_name ?? string.Empty, out value))
			{
				hashSet.Add(Normalize(value));
			}
			hashSet.Remove(string.Empty);
			list.Add(new CraftTarget
			{
				Name = ((UnityEngine.Object)item).name,
				Token = item.m_name,
				Resources = item.m_resources,
				MaxLevel = 1,
				Priority = 2,
				Aliases = hashSet
			});
		}
		return list;
	}

	private static IEnumerable<string> RecipeAliases(Recipe recipe)
	{
		return ItemAliases(recipe.m_item.m_itemData).Concat(new string[1] { Normalize(((UnityEngine.Object)recipe.m_item).name) });
	}

	private static List<Recipe> CraftableRecipes()
	{
		return (ObjectDB.instance?.m_recipes ?? new List<Recipe>()).Where((Recipe r) => (UnityEngine.Object)(object)r != (UnityEngine.Object)null && r.m_enabled && (UnityEngine.Object)(object)r.m_item != (UnityEngine.Object)null && r.m_item.m_itemData?.m_shared != null && r.m_resources != null && r.m_resources.Any((Piece.Requirement x) => (UnityEngine.Object)(object)x?.m_resItem != (UnityEngine.Object)null)).ToList();
	}

	private string ProcessCraftRequest(string id, string language, string query, int count, int level, IInventory output, List<ServersideQoLZDO> stores, ContainerRegistryProcessor registry, WarehouseStatsHistory stats, ref int moves, Action readWorld, List<WarehouseNode> network, List<ServersideQoLZDO> containers, float markerRange2, Dictionary<string, int> reserved)
	{
		string normalized = Normalize(query);
		LoadGermanNames();
		// Crafting recipes and hammer/hoe/cultivator build pieces share one name space.
		List<CraftTarget> targets = CraftTargets();
		CraftTarget craftTarget;
		if (query.IndexOf('*') >= 0)
		{
			// Local patch 10: "craft *frost resistance" matches recipe names like plain wildcard requests;
			// it crafts when the pattern names one item, and lists the candidates otherwise.
			Regex pattern = WildcardPattern(query);
			List<CraftTarget> matches = (from t in targets
				where t.Aliases.Any((string alias) => pattern.IsMatch(alias))
				group t by t.Token into g
				select g.OrderBy((CraftTarget t) => t.Priority).First()).ToList();
			if (matches.Count != 1)
			{
				string unknown = WarehouseLocalization.Format(language, "request.craft_unknown", query);
				List<string> names = matches.Select((CraftTarget t) => t.DisplayName(language)).Distinct<string>(StringComparer.OrdinalIgnoreCase).OrderBy<string, string>((string x) => x, StringComparer.OrdinalIgnoreCase).Take(4).ToList();
				return (names.Count > 0) ? (unknown + "\n" + string.Join(" | ", names)) : unknown;
			}
			craftTarget = matches[0];
		}
		else
		{
			craftTarget = targets.Where((CraftTarget t) => t.Aliases.Contains(normalized)).OrderBy((CraftTarget t) => t.Priority).FirstOrDefault();
		}
		if (craftTarget == null && normalized.Length >= 5 && query.IndexOf('*') < 0)
		{
			// Tolerate small spelling differences ("Armour Stand" -> "Armor stand") when one target is clearly closest.
			var source = (from t in targets
				select new
				{
					Target = t,
					Distance = t.Aliases.Min((string alias) => LevenshteinDistance(alias, normalized))
				} into x
				where x.Distance <= 2
				orderby x.Distance, x.Target.Priority
				select x).ToList();
			if (source.Count > 0 && (source.Count == 1 || source[1].Distance > source[0].Distance))
			{
				craftTarget = source[0].Target;
			}
		}
		if (craftTarget == null)
		{
			string text = WarehouseLocalization.Format(language, "request.craft_unknown", query);
			List<string> list = (from t in targets
				where normalized.Length >= 3 && t.Aliases.Any((string alias) => alias.Contains(normalized))
				select t.DisplayName(language)).Distinct<string>(StringComparer.OrdinalIgnoreCase).OrderBy<string, string>((string x) => x, StringComparer.OrdinalIgnoreCase).Take(3).ToList();
			return (list.Count > 0) ? (text + "\n" + string.Join(" | ", list)) : text;
		}
		string text2 = craftTarget.DisplayName(language);
		int num = craftTarget.MaxLevel;
		if (level > num)
		{
			return WarehouseLocalization.Format(language, "request.craft_max_level", text2, num);
		}
		Recipe recipe = craftTarget.Recipe;
		// Upgrader resources are only consumed at a Refinement Forge, never at a normal crafting station.
		List<Piece.Requirement> list2 = craftTarget.Resources.Where((Piece.Requirement x) => (UnityEngine.Object)(object)x?.m_resItem != (UnityEngine.Object)null && !x.m_upgraderResource && x.GetAmount(level) > 0).ToList();
		if ((UnityEngine.Object)(object)recipe != (UnityEngine.Object)null && recipe.m_requireOnlyOneIngredient && list2.Count > 1)
		{
			// Any one ingredient is enough: take the first one the warehouse can cover.
			Piece.Requirement requirement = list2.FirstOrDefault((Piece.Requirement x) => StockFor(stores, registry, output, ((UnityEngine.Object)x.m_resItem).name) >= x.GetAmount(level) * count) ?? list2[0];
			list2 = new List<Piece.Requirement> { requirement };
		}
		List<string> list3 = new List<string> { (level > 1) ? WarehouseLocalization.Format(language, "request.craft_header_level", text2, level, count) : WarehouseLocalization.Format(language, "request.craft_header", text2, count) };
		foreach (Piece.Requirement item in list2)
		{
			string name = ((UnityEngine.Object)item.m_resItem).name;
			int num2 = (int)Math.Min(10000L, (long)item.GetAmount(level) * (long)count);
			// OUT stock already counted by an earlier craft entry on this sign is not reused.
			reserved.TryGetValue(name, out var value);
			int num3 = Math.Max(0, output.Items.Where((ItemData x) => x != null && x.m_stack > 0 && ItemMatchesQuery(x, name)).Sum((ItemData x) => x.m_stack) - value);
			reserved[name] = value + num2;
			int num4 = num2 - num3;
			if (num4 <= 0)
			{
				list3.Add(WarehouseLocalization.Format(language, "request.craft_in_output", LocalizedItemName(item.m_resItem.m_itemData, language), num2));
				continue;
			}
			int locallyAvailable;
			string text3 = ProcessSingleRequest(id, language, name, num4, output, stores, registry, stats, ref moves, out locallyAvailable);
			if (locallyAvailable < num4)
			{
				readWorld();
				List<string> list4 = FindRemoteStock(id, name, network, containers, markerRange2, registry);
				if (list4.Count > 0)
				{
					text3 = text3 + " (" + string.Join(" | ", list4) + ")";
				}
			}
			list3.Add(text3);
		}
		this.Logger.LogInfo((object)string.Format("Warehouse '{0}': craft request '{1}' (level {2}, x{3}) resolved to '{4}' with {5} ingredient(s).", id, query, level, count, craftTarget.Name, list2.Count));
		return string.Join("\n", list3);
	}

	private int StockFor(List<ServersideQoLZDO> stores, ContainerRegistryProcessor registry, IInventory output, string query)
	{
		return stores.Select((ServersideQoLZDO store) => InventoryFor(registry, store)).Concat(new IInventory[1] { output }).Where((IInventory inv) => inv != null).SelectMany((IInventory inv) => inv.Items).Where((ItemData x) => x != null && x.m_stack > 0 && ItemMatchesQuery(x, query)).Sum((ItemData x) => x.m_stack);
	}

	private string ProcessWildcardRequest(string id, string language, string payload, IInventory output, List<ServersideQoLZDO> stores, ContainerRegistryProcessor registry, WarehouseStatsHistory stats, ref int moves)
	{
		if (!TryParseWildcardRequest(payload, out var pattern, out var display, out var amount))
		{
			return WarehouseLocalization.Text(language, "request.error_example");
		}
		List<string> list = (from item in stores.SelectMany(delegate(ServersideQoLZDO store)
			{
				IInventory obj = InventoryFor(registry, store);
				IEnumerable<ItemData> enumerable = ((obj != null) ? obj.Items : null);
				return enumerable ?? Enumerable.Empty<ItemData>();
			})
			where item != null && item.m_stack > 0 && ItemAliases(item).Any((string alias) => pattern.IsMatch(Normalize(alias)))
			select ItemName(item) into name
			where !string.IsNullOrWhiteSpace(name)
			select name).Distinct<string>(StringComparer.OrdinalIgnoreCase).OrderBy<string, string>((string name) => name, StringComparer.OrdinalIgnoreCase).ToList();
		if (list.Count == 0)
		{
			return WarehouseLocalization.Format(language, "request.not_found", display);
		}
		if (list.Count > 10)
		{
			return WarehouseLocalization.Format(language, "request.wildcard_too_many", display, list.Count, 10);
		}
		List<string> list2 = new List<string>();
		foreach (string item in list)
		{
			list2.Add(ProcessSingleRequest(id, language, item, amount, output, stores, registry, stats, ref moves, out var _));
		}
		this.Logger.LogInfo((object)string.Format("Warehouse '{0}': wildcard request '{1}' matched {2} item(s): {3}.", id, display, list.Count, string.Join(", ", list)));
		return string.Join("\n", list2);
	}

	internal static bool TryParseWildcardRequest(string payload, out Regex pattern, out string display, out int amount)
	{
		pattern = null;
		display = string.Empty;
		amount = 1;
		Match match = Regex.Match(payload.Replace("\r", string.Empty).Split(new char[1] { '\n' })[0].Trim(), "^(?<item>.*?\\*.*?)(?:\\s+(?:x)?(?<amount>\\d+))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		if (!match.Success)
		{
			return false;
		}
		if (match.Groups["amount"].Success && (!int.TryParse(match.Groups["amount"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out amount) || amount <= 0 || amount > 10000))
		{
			return false;
		}
		display = match.Groups["item"].Value.Trim();
		pattern = WildcardPattern(display);
		return display.Length > 0;
	}

	// "*frost resistance" -> ^.*frostresistance$ over normalized names (punctuation such as ':' ignored).
	internal static Regex WildcardPattern(string text)
	{
		return new Regex("^" + string.Join(".*", from part in (text ?? string.Empty).Split(new char[1] { '*' })
			select Regex.Escape(Normalize(part))) + "$", RegexOptions.CultureInvariant);
	}

	internal static bool IsStatus(string payload)
	{
		return WarehouseLocalization.StartsWithAnyTranslation(payload, "request.done", "request.partial", "request.output_full", "request.not_found", "request.wildcard_too_many", "request.craft_header", "request.craft_header_level", "request.craft_unknown", "request.craft_max_level", "request.error", "request.error_example", "request.suggestions", "request.shown_on_board");
	}

	internal static bool IsRequestPrompt(string payload)
	{
		string text = (payload ?? string.Empty).Trim();
		if (!WarehouseLocalization.StartsWithAnyTranslation(text, "request.idle", "request.title") && !text.Equals("REQ", StringComparison.OrdinalIgnoreCase))
		{
			return text.StartsWith("REQ\n", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static void WriteRequestPrompt(ServersideQoLZDO sign, string id, string language)
	{
		string value = sign.ZDO.GetString("ModerWarehouse.RequestTargets", string.Empty);
		string text = WarehouseLocalization.Text(language, "request.example_item");
		string text2 = (string.IsNullOrWhiteSpace(value) ? ("<color=#80ffff>" + WarehouseLocalization.Text(language, "request.idle") + "\n<color=white>" + text + " 100\n" + WarehouseLocalization.Text(language, "request.target") + " " + text + " 1000") : ("<color=#80ffff>" + WarehouseLocalization.Text(language, "request.title") + "\n<color=white>" + WarehouseLocalization.Text(language, "request.updating")));
		WriteRequestText(sign, text2);
	}

	private static void WriteRequestText(ServersideQoLZDO sign, string text)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		string text2 = PlainText(text).Trim();
		sign.ZDO.Set("ModerWarehouse.RequestRenderedText", text2);
		ServersideQoLZDO.ZDOVars vars = sign.Vars;
		if (!string.Equals(vars.GetText(""), text, StringComparison.Ordinal))
		{
			if (!sign.IsOwnerOrUnassigned())
			{
				sign.ClaimOwnership();
			}
			vars = sign.Vars;
			vars.SetText(text);
		}
	}

	private static void WriteStatus(string id, ServersideQoLZDO sign, string status)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		if (!sign.IsOwnerOrUnassigned())
		{
			sign.ClaimOwnership();
		}
		string text = "<color=white>" + status;
		sign.ZDO.Set("ModerWarehouse.RequestRenderedText", PlainText(text).Trim());
		ServersideQoLZDO.ZDOVars vars = sign.Vars;
		vars.SetText(text);
	}

	internal static bool TryParseTargets(string payload, out Dictionary<string, int> targets)
	{
		targets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		foreach (string item in (from x in (payload ?? string.Empty).Split(new char[4] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
			select x.Trim() into x
			where x.Length > 0
			select x).Take(20))
		{
			if (!TryParseRequest(item, out var query, out var amount))
			{
				return false;
			}
			targets[query] = amount;
		}
		return targets.Count > 0;
	}

	private static string SerializeTargets(Dictionary<string, int> targets)
	{
		return string.Join("\n", targets.Select((KeyValuePair<string, int> x) => x.Key + "\t" + x.Value.ToString(CultureInfo.InvariantCulture)));
	}

	internal static Dictionary<string, int> DeserializeTargets(string value)
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		string[] array = (value ?? string.Empty).Split(new char[1] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split(new char[1] { '\t' });
			if (array2.Length == 2 && int.TryParse(array2[1], NumberStyles.None, CultureInfo.InvariantCulture, out var result) && result > 0)
			{
				dictionary[array2[0]] = result;
			}
		}
		return dictionary;
	}

	private void EnsureHelpSign(ServersideQoLZDO inputSign, string id, string language)
	{
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0279: Unknown result type (might be due to invalid IL or missing references)
		//IL_027e: Unknown result type (might be due to invalid IL or missing references)
		inputSign.ZDO.Set("ModerWarehouse.HelpWarehouse", id);
		string group = inputSign.ZDO.GetString("ModerWarehouse.HelpGroup", string.Empty);
		if (string.IsNullOrWhiteSpace(group))
		{
			// A recreated IN sign lost its group id: re-adopt the help sign already standing next to it.
			ZDO zDO = NearbyHelpSigns(inputSign).FirstOrDefault();
			group = ((zDO != null) ? zDO.GetString("ModerWarehouse.HelpGroup", string.Empty) : string.Empty);
			if (string.IsNullOrWhiteSpace(group))
			{
				group = Guid.NewGuid().ToString("N");
			}
			inputSign.ZDO.Set("ModerWarehouse.HelpGroup", group);
		}
		ServersideQoLZDO val = ((IEnumerable<ServersideQoLZDO>)_signs).FirstOrDefault((Func<ServersideQoLZDO, bool>)((ServersideQoLZDO sign) => sign.ZDO.GetBool("ModerWarehouse.HelpChild", false) && sign.ZDO.GetString("ModerWarehouse.HelpGroup", string.Empty) == group));
		string text = "<size=70%><color=#80ffff>" + WarehouseLocalization.Text(language, "help.title") + "\n<color=white>" + WarehouseLocalization.Text(language, "help.in") + "\n" + WarehouseLocalization.Text(language, "help.req") + "\n" + WarehouseLocalization.Text(language, "help.out") + "\n" + WarehouseLocalization.Text(language, "help.board");
		ServersideQoLZDO.ZDOVars vars;
		DateTime value;
		if (val != null)
		{
			// Local patch 5: remove duplicate help signs left behind by earlier restarts (each one
			// got its own group id, so match them by position next to this IN sign).
			float helpRange2 = HelpSignRange * HelpSignRange;
			foreach (ServersideQoLZDO item in _signs.Where((ServersideQoLZDO sign) => sign != val && sign.IsModCreator() && sign.ZDO.GetBool("ModerWarehouse.HelpChild", false) && Distance2(sign, inputSign) <= helpRange2).ToList())
			{
				_signs.Remove(item);
				item.Destroy();
				this.Logger.LogInfo((object)("Warehouse '" + id + "': removed duplicate automatic help sign."));
			}
			val.ZDO.Set("ModerWarehouse.HelpWarehouse", id);
			_helpPendingAt.Remove(inputSign);
			vars = val.Vars;
			if (!string.Equals(vars.GetText(""), text, StringComparison.Ordinal))
			{
				if (!val.IsOwnerOrUnassigned())
				{
					val.ClaimOwnership();
				}
				vars = val.Vars;
				vars.SetText(text);
			}
		}
		else if (!_helpPendingAt.TryGetValue(inputSign, out value))
		{
			_helpPendingAt[inputSign] = WarehouseClock.UtcNow;
		}
		else
		{
			if ((WarehouseClock.UtcNow - value).TotalSeconds < 5.0)
			{
				return;
			}
			// After a restart the existing help sign may simply not be processed yet; only the
			// world itself can say whether it is really missing.
			ZDO zDO2 = NearbyHelpSigns(inputSign).FirstOrDefault();
			if (zDO2 != null)
			{
				inputSign.ZDO.Set("ModerWarehouse.HelpGroup", zDO2.GetString("ModerWarehouse.HelpGroup", string.Empty));
				_helpPendingAt.Remove(inputSign);
				return;
			}
			Quaternion rotation = inputSign.ZDO.GetRotation();
			Vector3 val2 = inputSign.ZDO.GetPosition() + rotation * new Vector3(1.2f, 0f, 0f);
			ServersideQoLZDO val3 = this.PlacePiece(val2, Prefabs.Sign, rotation, (CreatorMarkers)2);
			if (val3 == null)
			{
				return;
			}
			val3.Fields<Piece>().Set((Func<Expression<Func<Piece, bool>>>)(() => (Piece x) => x.m_canBeRemoved), true);
			val3.ZDO.Set("ModerWarehouse.HelpGroup", group);
			val3.ZDO.Set("ModerWarehouse.HelpChild", true);
			val3.ZDO.Set("ModerWarehouse.HelpWarehouse", id);
			vars = val3.Vars;
			vars.SetText(text);
			if (_signs.Add(val3))
			{
				val3.Destroyed += delegate(ServersideQoLZDO destroyed)
				{
					_signs.Remove(destroyed);
				};
			}
			_helpPendingAt.Remove(inputSign);
			this.Logger.LogInfo((object)("Warehouse '" + id + "': created automatic help sign left of IN marker."));
		}
	}

	private const float HelpSignRange = 2.5f;

	private static List<ZDO> NearbyHelpSigns(ServersideQoLZDO inputSign)
	{
		Vector3 position = inputSign.ZDO.GetPosition();
		return (from x in WorldObjectsNear(position)
			where x.GetBool("ModerWarehouse.HelpChild", false) && !string.IsNullOrWhiteSpace(x.GetString("ModerWarehouse.HelpGroup", string.Empty))
			let d = (x.GetPosition() - position).sqrMagnitude
			where d <= HelpSignRange * HelpSignRange
			orderby d
			select x).ToList();
	}

	// Local patch 5: ServersideQoL ContainerSigns replaces every chest sign with a fresh one on
	// each server start and only carries the text over. Links stored on the sign ZDO (REQ/INFO
	// warehouse, target stock) are therefore mirrored onto the chest, which does persist.
	private const string ChestRequestKey = "ModerWarehouse.ChestRequestWarehouse";

	private const string ChestInfoKey = "ModerWarehouse.ChestInfoWarehouse";

	private const string ChestTargetsKey = "ModerWarehouse.ChestRequestTargets";

	private const float ChestSignRange = 1.6f;

	private static readonly List<ZDO> WorldLookupBuffer = new List<ZDO>();

	private static readonly Dictionary<int, bool> ContainerPrefabs = new Dictionary<int, bool>();

	private readonly HashSet<ServersideQoLZDO> _chestLinkChecked = new HashSet<ServersideQoLZDO>();

	private static List<ZDO> WorldObjectsNear(Vector3 position)
	{
		WorldLookupBuffer.Clear();
		ZDOMan.instance.FindSectorObjects(ZoneSystem.GetZone(position), new SimulationDistance(1, 1, classic: true), WorldLookupBuffer);
		List<ZDO> result = WorldLookupBuffer.Where((ZDO x) => x != null && x.IsValid()).ToList();
		WorldLookupBuffer.Clear();
		return result;
	}

	private static bool IsContainerPrefab(int prefab)
	{
		if (ContainerPrefabs.TryGetValue(prefab, out var value))
		{
			return value;
		}
		GameObject gameObject = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(prefab) : null);
		if ((UnityEngine.Object)(object)gameObject == (UnityEngine.Object)null)
		{
			return false;
		}
		value = (UnityEngine.Object)(object)gameObject.GetComponent<Container>() != (UnityEngine.Object)null;
		ContainerPrefabs[prefab] = value;
		return value;
	}

	private static ZDO ChestForSign(ServersideQoLZDO sign)
	{
		Vector3 position = sign.ZDO.GetPosition();
		return (from x in WorldObjectsNear(position)
			where IsContainerPrefab(x.GetPrefab())
			let d = (x.GetPosition() - position).sqrMagnitude
			where d <= ChestSignRange * ChestSignRange
			orderby d
			select x).FirstOrDefault();
	}

	private static void SetIfChanged(ZDO zdo, string key, string value)
	{
		if (!string.Equals(zdo.GetString(key, string.Empty), value ?? string.Empty, StringComparison.Ordinal))
		{
			zdo.Set(key, value ?? string.Empty);
		}
	}

	private void SaveChestSignLinks(ServersideQoLZDO sign)
	{
		ZDO zDO = ChestForSign(sign);
		if (zDO != null)
		{
			SetIfChanged(zDO, ChestRequestKey, sign.ZDO.GetString("ModerWarehouse.RequestWarehouse", string.Empty));
			SetIfChanged(zDO, ChestInfoKey, sign.ZDO.GetString("ModerWarehouse.InfoWarehouse", string.Empty));
			SetIfChanged(zDO, ChestTargetsKey, sign.ZDO.GetString("ModerWarehouse.RequestTargets", string.Empty));
		}
	}

	private void RestoreChestSignLinks(ServersideQoLZDO sign, string plainText)
	{
		// Once per sign and session: the lookup scans nearby world objects.
		if (!_chestLinkChecked.Add(sign))
		{
			return;
		}
		sign.Destroyed += delegate(ServersideQoLZDO destroyed)
		{
			_chestLinkChecked.Remove(destroyed);
		};
		bool flag = !string.IsNullOrWhiteSpace(sign.ZDO.GetString("ModerWarehouse.RequestWarehouse", string.Empty)) || !string.IsNullOrWhiteSpace(sign.ZDO.GetString("ModerWarehouse.InfoWarehouse", string.Empty));
		if (flag)
		{
			// Already linked (e.g. set up before this patch): back the link up on the chest.
			SaveChestSignLinks(sign);
			return;
		}
		if (sign.ZDO.GetBool("ModerWarehouse.HelpChild", false) || sign.ZDO.GetBool("ModerWarehouse.AutoLabelManaged", false) || !string.IsNullOrWhiteSpace(sign.ZDO.GetString("ModerWarehouse.AutoLabelItem", string.Empty)) || !string.IsNullOrWhiteSpace(sign.ZDO.GetString("ModerWarehouse.BoardGroup", string.Empty)) || !string.IsNullOrWhiteSpace(sign.ZDO.GetString("ModerWarehouse.StatsGroup", string.Empty)))
		{
			return;
		}
		ZDO zDO = ChestForSign(sign);
		// ContainerSigns copies the chest's text onto the new sign; anything else is a different sign.
		if (zDO == null || !string.Equals(PlainText(zDO.GetString(ZDOVars.s_text, string.Empty)).Trim(), (plainText ?? string.Empty).Trim(), StringComparison.Ordinal))
		{
			return;
		}
		string text = zDO.GetString(ChestRequestKey, string.Empty);
		string text2 = zDO.GetString(ChestInfoKey, string.Empty);
		if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(text2))
		{
			return;
		}
		if (!string.IsNullOrWhiteSpace(text))
		{
			sign.ZDO.Set("ModerWarehouse.RequestWarehouse", text);
			string text3 = zDO.GetString(ChestTargetsKey, string.Empty);
			if (!string.IsNullOrWhiteSpace(text3))
			{
				sign.ZDO.Set("ModerWarehouse.RequestTargets", text3);
			}
			// The carried-over text is our own last output, not a new request.
			sign.ZDO.Set("ModerWarehouse.RequestRenderedText", (plainText ?? string.Empty).Trim());
			this.Logger.LogInfo((object)("Warehouse '" + text + "': restored REQ sign link from its chest after the sign was recreated."));
		}
		if (!string.IsNullOrWhiteSpace(text2))
		{
			sign.ZDO.Set("ModerWarehouse.InfoWarehouse", text2);
			this.Logger.LogInfo((object)("Warehouse '" + text2 + "': restored INFO sign link from its chest after the sign was recreated."));
		}
	}

	private void EnsureBoard(ServersideQoLZDO anchor, string id)
	{
		//IL_0277: Unknown result type (might be due to invalid IL or missing references)
		//IL_027c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0283: Unknown result type (might be due to invalid IL or missing references)
		//IL_0288: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_0371: Unknown result type (might be due to invalid IL or missing references)
		//IL_0376: Unknown result type (might be due to invalid IL or missing references)
		string group = anchor.ZDO.GetString("ModerWarehouse.BoardGroup", string.Empty);
		if (string.IsNullOrWhiteSpace(group))
		{
			return;
		}
		if (!_boardReadyGroups.Contains(group))
		{
			if (!_boardEnsurePendingAt.TryGetValue(group, out var value))
			{
				_boardEnsurePendingAt[group] = WarehouseClock.UtcNow;
				return;
			}
			if ((WarehouseClock.UtcNow - value).TotalSeconds < 5.0)
			{
				return;
			}
			_boardEnsurePendingAt.Remove(group);
			_boardReadyGroups.Add(group);
		}
		List<ServersideQoLZDO> source = _signs.Where((ServersideQoLZDO x) => x.ZDO.GetString("ModerWarehouse.BoardGroup", string.Empty) == group).ToList();
		int num = 0;
		foreach (IGrouping<int, ServersideQoLZDO> item in from x in source
			group x by x.ZDO.GetInt("ModerWarehouse.BoardCell", -1))
		{
			ServersideQoLZDO keep = ((item.Key == 12 && item.Contains(anchor)) ? anchor : item.First());
			foreach (ServersideQoLZDO item2 in item.Where((ServersideQoLZDO x) => x != keep).ToList())
			{
				_signs.Remove(item2);
				item2.Destroy();
				num++;
			}
		}
		if (num > 0)
		{
			this.Logger.LogInfo((object)$"Warehouse '{id}': removed {num} duplicate stock-board sign layer(s) from '{group}'.");
		}
		Dictionary<int, ServersideQoLZDO> dictionary = (from x in _signs
			where x.ZDO.GetString("ModerWarehouse.BoardGroup", string.Empty) == @group
			group x by x.ZDO.GetInt("ModerWarehouse.BoardCell", -1)).ToDictionary((IGrouping<int, ServersideQoLZDO> x) => x.Key, (IGrouping<int, ServersideQoLZDO> x) => x.First());
		Quaternion rotation = anchor.ZDO.GetRotation();
		Vector3 position = anchor.ZDO.GetPosition();
		int num2 = 0;
		for (int num3 = 0; num3 < 16; num3++)
		{
			if (num3 == 12 || dictionary.ContainsKey(num3))
			{
				continue;
			}
			int num4 = num3 % 4;
			int num5 = num3 / 4;
			Vector3 val = position + rotation * new Vector3((float)num4 * -1.12f, (float)(3 - num5) * 0.5f, 0f);
			ServersideQoLZDO val2 = this.PlacePiece(val, Prefabs.Sign, rotation, (CreatorMarkers)2);
			if (val2 == null)
			{
				continue;
			}
			val2.Fields<Piece>().Set((Func<Expression<Func<Piece, bool>>>)(() => (Piece x) => x.m_canBeRemoved), true);
			val2.ZDO.Set("ModerWarehouse.BoardWarehouse", id);
			val2.ZDO.Set("ModerWarehouse.BoardGroup", group);
			val2.ZDO.Set("ModerWarehouse.BoardCell", num3);
			ServersideQoLZDO.ZDOVars vars = val2.Vars;
			vars.SetText("<color=white>...");
			if (_signs.Add(val2))
			{
				val2.Destroyed += delegate(ServersideQoLZDO destroyed)
				{
					_signs.Remove(destroyed);
				};
			}
			num2++;
		}
		if (num2 > 0)
		{
			this.Logger.LogInfo((object)$"Warehouse '{id}': created {num2} sign(s) for 4x4 stock board '{group}'.");
		}
	}

	private void EnsureStatsBoard(ServersideQoLZDO anchor, string id)
	{
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0237: Unknown result type (might be due to invalid IL or missing references)
		//IL_023c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0390: Unknown result type (might be due to invalid IL or missing references)
		//IL_0395: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03bb: Unknown result type (might be due to invalid IL or missing references)
		string group = anchor.ZDO.GetString("ModerWarehouse.StatsGroup", string.Empty);
		if (string.IsNullOrWhiteSpace(group))
		{
			return;
		}
		if (!_statsReadyGroups.Contains(group))
		{
			if (!_statsEnsurePendingAt.TryGetValue(group, out var value))
			{
				_statsEnsurePendingAt[group] = WarehouseClock.UtcNow;
				return;
			}
			if ((WarehouseClock.UtcNow - value).TotalSeconds < 5.0)
			{
				return;
			}
			_statsEnsurePendingAt.Remove(group);
			_statsReadyGroups.Add(group);
		}
		Dictionary<int, ServersideQoLZDO> dictionary = (from x in _signs
			where x.ZDO.GetString("ModerWarehouse.StatsGroup", string.Empty) == @group
			group x by x.ZDO.GetInt("ModerWarehouse.StatsCell", -1)).ToDictionary((IGrouping<int, ServersideQoLZDO> x) => x.Key, (IGrouping<int, ServersideQoLZDO> x) => x.First());
		Quaternion rotation = anchor.ZDO.GetRotation();
		Vector3 position = anchor.ZDO.GetPosition();
		int num = 0;
		ServersideQoLZDO.ZDOVars vars;
		for (int num2 = 0; num2 < 16; num2++)
		{
			if (num2 == 12 || dictionary.ContainsKey(num2))
			{
				continue;
			}
			int num3 = num2 % 4;
			int num4 = num2 / 4;
			Vector3 val = position + rotation * new Vector3((float)num3 * -1.02f, (float)(3 - num4) * 0.5f, 0f);
			ServersideQoLZDO val2 = this.PlacePiece(val, Prefabs.Sign, rotation, (CreatorMarkers)2);
			if (val2 == null)
			{
				continue;
			}
			val2.Fields<Piece>().Set((Func<Expression<Func<Piece, bool>>>)(() => (Piece x) => x.m_canBeRemoved), true);
			val2.ZDO.Set("ModerWarehouse.StatsWarehouse", id);
			val2.ZDO.Set("ModerWarehouse.StatsGroup", group);
			val2.ZDO.Set("ModerWarehouse.StatsCell", num2);
			vars = val2.Vars;
			vars.SetText("<color=white>...");
			if (_signs.Add(val2))
			{
				val2.Destroyed += delegate(ServersideQoLZDO destroyed)
				{
					_signs.Remove(destroyed);
				};
			}
			num++;
		}
		if (!dictionary.Values.Any((ServersideQoLZDO x) => x.ZDO.GetBool("ModerWarehouse.StatsControl", false)))
		{
			Vector3 val3 = position + rotation * new Vector3(-1.53f, -0.55f, 0f);
			ServersideQoLZDO val4 = this.PlacePiece(val3, Prefabs.Sign, rotation, (CreatorMarkers)2);
			if (val4 != null)
			{
				val4.Fields<Piece>().Set((Func<Expression<Func<Piece, bool>>>)(() => (Piece x) => x.m_canBeRemoved), true);
				val4.ZDO.Set("ModerWarehouse.StatsWarehouse", id);
				val4.ZDO.Set("ModerWarehouse.StatsGroup", group);
				val4.ZDO.Set("ModerWarehouse.StatsCell", -2);
				val4.ZDO.Set("ModerWarehouse.StatsControl", true);
				vars = val4.Vars;
				vars.SetText("<color=#80ffff>CONTROL\n<color=white>E: 1-90 AUTO");
				ZDO zDO = val4.ZDO;
				vars = val4.Vars;
				zDO.Set("ModerWarehouse.BoardRenderedText", PlainText(vars.GetText("")).Trim());
				if (_signs.Add(val4))
				{
					val4.Destroyed += delegate(ServersideQoLZDO destroyed)
					{
						_signs.Remove(destroyed);
					};
				}
				num++;
			}
		}
		if (num > 0)
		{
			this.Logger.LogInfo((object)$"Warehouse '{id}': created {num} sign/control(s) for 4x4 statistics board '{group}'.");
		}
	}

	private void CaptureBoardControlInput(ServersideQoLZDO sign, string group, int cell)
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		if (string.IsNullOrWhiteSpace(group) || (cell != 1 && cell != 2))
		{
			return;
		}
		string text = sign.ZDO.GetString("ModerWarehouse.BoardRenderedText", string.Empty);
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		ServersideQoLZDO.ZDOVars vars = sign.Vars;
		string text2 = PlainText(vars.GetText("")).Trim();
		if (string.Equals(text2, text, StringComparison.Ordinal))
		{
			return;
		}
		string text3 = text2;
		int num = text3.LastIndexOf('\n');
		if (num >= 0)
		{
			text3 = text3.Substring(num + 1);
		}
		text3 = text3.Trim();
		if (string.IsNullOrWhiteSpace(text3) || text3.Equals("AUTO", StringComparison.OrdinalIgnoreCase))
		{
			_boardControls.Remove(group);
			this.Logger.LogInfo((object)("Stock board '" + group + "': returned to automatic rotation."));
			return;
		}
		if (!_boardControls.TryGetValue(group, out var value))
		{
			value = new BoardControl();
		}
		if (cell == 1)
		{
			value.Prefix = text3;
			value.Page = null;
		}
		else
		{
			Match match = Regex.Match(text3, "\\d+");
			if (!match.Success || !int.TryParse(match.Value, out var result))
			{
				return;
			}
			value.Page = Math.Max(1, result);
		}
		value.ChangedAt = WarehouseClock.UtcNow;
		_boardControls[group] = value;
		this.Logger.LogInfo((object)("Stock board '" + group + "': filter='" + (value.Prefix ?? "ALLE") + "', page=" + (value.Page.HasValue ? value.Page.Value.ToString() : "1") + " for 60 seconds."));
	}

	private void WriteShoppingLists(string id, string language, IEnumerable<ServersideQoLZDO> requestSigns, List<ServersideQoLZDO> stores, ContainerRegistryProcessor registry)
	{
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		List<ItemData> items = (from item in stores.SelectMany(delegate(ServersideQoLZDO store)
			{
				IInventory val = InventoryFor(registry, store);
				IEnumerable<ItemData> enumerable = ((val != null) ? val.Items : null);
				return enumerable ?? Enumerable.Empty<ItemData>();
			})
			where item != null && item.m_stack > 0 && item.m_shared != null
			select item).ToList();
		foreach (ServersideQoLZDO item in requestSigns.Distinct())
		{
			Dictionary<string, int> dictionary = DeserializeTargets(item.ZDO.GetString("ModerWarehouse.RequestTargets", string.Empty));
			if (dictionary.Count == 0)
			{
				continue;
			}
			ServersideQoLZDO.ZDOVars vars = item.Vars;
			string text = PlainText(vars.GetText("")).Trim();
			string b = item.ZDO.GetString("ModerWarehouse.RequestRenderedText", string.Empty);
			bool flag = text.StartsWith("SOLL ", StringComparison.OrdinalIgnoreCase) || text.StartsWith("TARGET ", StringComparison.OrdinalIgnoreCase);
			if ((!string.Equals(text, b, StringComparison.Ordinal) && !flag) || IsStatus(text))
			{
				continue;
			}
			List<TargetStatus> list = (from x in dictionary.Select(delegate(KeyValuePair<string, int> target)
				{
					int current3 = items.Where((ItemData item) => ItemMatchesQuery(item, target.Key)).Sum((ItemData item) => item.m_stack);
					return new TargetStatus(target.Key, current3, target.Value);
				})
				where x.Current < x.Target
				orderby (x.Target <= 0) ? 1.0 : ((double)x.Current / (double)x.Target)
				select x).ThenBy<TargetStatus, string>((TargetStatus x) => x.Name, StringComparer.CurrentCultureIgnoreCase).Take(3).ToList();
			if (list.Count == 0)
			{
				string text2 = "<color=#80ffff>" + WarehouseLocalization.Text(language, "request.idle") + "\n<color=#80ff80>" + WarehouseLocalization.Text(language, "shopping.fulfilled") + "\n<color=white>" + WarehouseLocalization.Text(language, "request.example_item") + " 100";
				WriteRequestText(item, text2);
				continue;
			}
			StringBuilder stringBuilder = new StringBuilder("<color=#80ffff>").Append(WarehouseLocalization.Text(language, "shopping.title"));
			foreach (TargetStatus item2 in list)
			{
				string value = ((item2.Current <= 0) ? "#ff6666" : ((item2.Current < item2.Target) ? "#ffb347" : "#80ff80"));
				stringBuilder.Append("\n<color=").Append(value).Append('>')
					.Append(item2.Name)
					.Append(' ')
					.Append(FormatAmount(item2.Current))
					.Append('/')
					.Append(FormatAmount(item2.Target));
			}
			WriteRequestText(item, stringBuilder.ToString());
		}
	}

	private void WriteWarehouseBoard(string id, string language, IEnumerable<ServersideQoLZDO> boardSigns, List<ServersideQoLZDO> stores, ContainerRegistryProcessor registry, IReadOnlyList<Peer> peers, List<WarehouseNode> network, List<ServersideQoLZDO> containers, float markerRange2)
	{
		List<StockEntry> list = (from item in stores.SelectMany(delegate(ServersideQoLZDO store)
			{
				IInventory val = InventoryFor(registry, store);
				IEnumerable<ItemData> enumerable = ((val != null) ? val.Items : null);
				return enumerable ?? Enumerable.Empty<ItemData>();
			})
			where item != null && item.m_stack > 0 && item.m_shared != null
			group item by item.m_shared.m_name into @group
			select new StockEntry(@group.Key, LocalizedItemName(@group.First(), language), @group.Sum((ItemData item) => item.m_stack))).OrderBy<StockEntry, string>((StockEntry entry) => entry.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
		int num = stores.Count((ServersideQoLZDO store) => registry.GetState(store) != null && !InventoryFor(registry, store).Items.Any((ItemData item) => item != null && item.m_stack > 0));
		int num2 = Math.Max(0, stores.Count - num);
		double num3 = ((stores.Count == 0) ? 0.0 : ((double)num2 / (double)stores.Count));
		double num4 = (double)Mathf.Clamp(ConfigBase<WarehouseConfig>.Instance.CapacityWarningPercent.Value, 0, 100) / 100.0;
		double num5 = (double)Mathf.Clamp(ConfigBase<WarehouseConfig>.Instance.CapacityCriticalPercent.Value, ConfigBase<WarehouseConfig>.Instance.CapacityWarningPercent.Value, 100) / 100.0;
		string text = ((num3 >= num5) ? "#ff6666" : ((num3 >= num4) ? "#ffb347" : "#80ff80"));
		string text2 = ((num3 >= num5) ? WarehouseLocalization.Text(language, "board.expand") : (num2 + " / " + stores.Count));
		Dictionary<string, StockEntry> dictionary = list.ToDictionary<StockEntry, string, StockEntry>((StockEntry entry) => entry.Token, (StockEntry entry) => entry, StringComparer.Ordinal);
		foreach (IGrouping<string, ServersideQoLZDO> item in from sign in boardSigns
			where !string.IsNullOrWhiteSpace(sign.ZDO.GetString("ModerWarehouse.BoardGroup", string.Empty))
			group sign by sign.ZDO.GetString("ModerWarehouse.BoardGroup", string.Empty))
		{
			Dictionary<int, ServersideQoLZDO> dictionary2 = (from sign in item
				group sign by sign.ZDO.GetInt("ModerWarehouse.BoardCell", -1)).ToDictionary((IGrouping<int, ServersideQoLZDO> x) => x.Key, (IGrouping<int, ServersideQoLZDO> x) => x.First());
			if (!dictionary2.TryGetValue(12, out var _))
			{
				item.First();
			}
			float distance = Mathf.Max(1f, ConfigBase<WarehouseConfig>.Instance.BoardWakeDistance.Value);
			bool flag = IsPanelAwake(peers, dictionary2.Values, distance);
			if (!_boardVisits.TryGetValue(item.Key, out var value2))
			{
				value2 = new BoardVisitState
				{
					Awake = flag
				};
				value2.Baseline = CloneSnapshot(dictionary);
				_boardVisits[item.Key] = value2;
			}
			else if (value2.Awake && !flag)
			{
				value2.Awake = false;
				value2.Changes.Clear();
			}
			else if (!value2.Awake && flag)
			{
				value2.Awake = true;
				value2.Changes = CalculateChanges(value2.Baseline, dictionary);
				value2.Baseline = CloneSnapshot(dictionary);
				value2.SummaryUntil = WarehouseClock.UtcNow.AddSeconds(Math.Max(2f, ConfigBase<WarehouseConfig>.Instance.VisitSummarySeconds.Value));
			}
			if (!flag)
			{
				_requestSearchNotices.Remove(id);
				WriteBoardCell(dictionary2, 0, "<color=#80ffff>" + WarehouseLocalization.Text(language, "board.warehouse") + "\n" + id.ToUpperInvariant());
				WriteBoardCell(dictionary2, 1, "<color=#8090a0>" + WarehouseLocalization.Text(language, "board.display_sleeping"));
				WriteBoardCell(dictionary2, 2, "<color=white>" + WarehouseLocalization.Format(language, "board.wake", distance.ToString("0.#", CultureInfo.InvariantCulture)));
				WriteBoardCell(dictionary2, 3, "<color=" + text + ">" + WarehouseLocalization.Text(language, "board.chests") + "\n" + text2);
				SleepDisplayPolicy.Layout layout = SleepDisplayPolicy.Create(WarehouseLocalization.Text(language, "board.sleep_word"), 12);
				for (int num6 = 0; num6 < 12; num6++)
				{
					string arg = ((num6 < layout.LabelLength) ? "#80ffff" : "#8090a0");
					WriteBoardCell(dictionary2, num6 + 4, $"<color={arg}><size=200%>{layout.Text[num6]}");
				}
				continue;
			}
			if (_requestSearchNotices.TryGetValue(id, out var value3))
			{
				if (WarehouseClock.UtcNow < value3.Until)
				{
					WriteRequestSearchBoard(dictionary2, id, language, value3, text, text2);
					continue;
				}
				_requestSearchNotices.Remove(id);
			}
			if (_deliveryNotices.TryGetValue(id, out var value4))
			{
				if (WarehouseClock.UtcNow < value4.Until)
				{
					WriteBoardMessage(dictionary2, id, language, WarehouseLocalization.Text(language, "board.deposited"), value4.Items.Select((KeyValuePair<string, int> x) => new BoardMessage(x.Key, "+" + FormatAmount(x.Value), "#80ff80")).ToList(), text, text2);
					continue;
				}
				_deliveryNotices.Remove(id);
			}
			if (WarehouseClock.UtcNow < value2.SummaryUntil && value2.Changes.Count > 0)
			{
				WriteBoardMessage(dictionary2, id, language, WarehouseLocalization.Text(language, "board.since_visit"), value2.Changes.Select((StockChange x) => new BoardMessage(x.Name, ((x.Amount > 0) ? "+" : string.Empty) + FormatSignedAmount(x.Amount), (x.Amount > 0) ? "#80ff80" : "#ffb347")).ToList(), text, text2);
				continue;
			}
			BoardControl boardControl = null;
			if (_boardControls.TryGetValue(item.Key, out var value5))
			{
				if ((WarehouseClock.UtcNow - value5.ChangedAt).TotalSeconds >= 60.0)
				{
					_boardControls.Remove(item.Key);
				}
				else
				{
					boardControl = value5;
				}
			}
			List<StockEntry> list2 = list;
			if (boardControl != null && !string.IsNullOrWhiteSpace(boardControl.Prefix))
			{
				string prefix = Normalize(boardControl.Prefix);
				list2 = list.Where((StockEntry entry) => Normalize(entry.Name).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
			}
			bool flag2 = boardControl == null && network.Count((WarehouseNode x) => x.Valid) > 1;
			int num7 = Math.Max(1, (list2.Count + 12 - 1) / 12) + (flag2 ? 1 : 0);
			double num8 = Math.Max(2f, ConfigBase<WarehouseConfig>.Instance.BoardPageSeconds.Value);
			int num9 = ((boardControl == null) ? ((int)(WarehouseClock.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds / num8) % num7) : Mathf.Clamp((boardControl.Page ?? 1) - 1, 0, num7 - 1));
			if (flag2 && num9 == 0)
			{
				NetworkOverview networkOverview = GetNetworkOverview(language, network, containers, markerRange2, registry);
				WriteBoardCell(dictionary2, 0, "<color=#80ffff>" + WarehouseLocalization.Text(language, "network.active"));
				WriteBoardCell(dictionary2, 1, "<color=white>" + WarehouseLocalization.Text(language, "network.local") + "\n" + id.ToUpperInvariant());
				WriteBoardCell(dictionary2, 2, string.Format("<color=white>{0}\n1 / {1}", WarehouseLocalization.Text(language, "board.page"), num7));
				WriteBoardCell(dictionary2, 3, "<color=" + text + ">" + WarehouseLocalization.Text(language, "board.chests") + "\n" + text2);
				for (int num10 = 0; num10 < 12; num10++)
				{
					string text3 = ((num10 < networkOverview.Rows.Count) ? ("<color=white>" + networkOverview.Rows[num10].Name + "\n<color=" + networkOverview.Rows[num10].Color + ">" + networkOverview.Rows[num10].Value) : "<color=white>-");
					WriteBoardCell(dictionary2, num10 + 4, text3);
				}
				continue;
			}
			int num11 = num9 - (flag2 ? 1 : 0);
			List<StockEntry> list3 = list2.Skip(num11 * 12).Take(12).ToList();
			string text4 = ((list3.Count == 0) ? WarehouseLocalization.Text(language, "board.no_matches") : (Initial(list3.First().Name) + " - " + Initial(list3.Last().Name)));
			WriteBoardCell(dictionary2, 0, "<color=#80ffff>" + WarehouseLocalization.Text(language, "board.warehouse") + "\n" + id.ToUpperInvariant());
			string text5 = ((boardControl != null && !string.IsNullOrWhiteSpace(boardControl.Prefix)) ? boardControl.Prefix.ToUpperInvariant() : text4);
			string text6 = ((boardControl == null) ? "white" : "#ffb347");
			WriteBoardCell(dictionary2, 1, "<color=" + text6 + ">" + WarehouseLocalization.Text(language, "board.stock") + "\n" + text5);
			WriteBoardCell(dictionary2, 2, string.Format("<color={0}>{1}\n{2} / {3}", text6, WarehouseLocalization.Text(language, "board.page"), num9 + 1, num7));
			WriteBoardCell(dictionary2, 3, "<color=" + text + ">" + WarehouseLocalization.Text(language, "board.chests") + "\n" + text2);
			for (int num12 = 0; num12 < 12; num12++)
			{
				string text7 = ((num12 < list3.Count) ? ("<color=white>" + list3[num12].Name + "\n<color=#80ffff>" + FormatAmount(list3[num12].Amount)) : "<color=white>-");
				WriteBoardCell(dictionary2, num12 + 4, text7);
			}
		}
	}

	private NetworkOverview GetNetworkOverview(string language, List<WarehouseNode> network, List<ServersideQoLZDO> containers, float markerRange2, ContainerRegistryProcessor registry)
	{
		string text = WarehouseLocalization.Resolve(language) + "|" + string.Join("|", from x in network.Where((WarehouseNode x) => x.Valid).OrderBy<WarehouseNode, string>((WarehouseNode x) => x.Id, StringComparer.OrdinalIgnoreCase)
			select x.Id.ToUpperInvariant() + ":" + x.Radius.ToString("0.###", CultureInfo.InvariantCulture) + ":" + ((object)x.Input.ZDO.GetPosition()).ToString());
		if (_networkOverview != null && !_networkOverviewDirty && _networkOverview.Signature == text && (WarehouseClock.UtcNow - _networkOverviewAt).TotalSeconds < (double)Math.Max(3f, ConfigBase<WarehouseConfig>.Instance.ScanInterval.Value))
		{
			return _networkOverview;
		}
		List<BoardMessage> list = new List<BoardMessage>();
		int num = 0;
		int num2 = 0;
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		foreach (WarehouseNode item in network.Where((WarehouseNode x) => x.Valid).OrderBy<WarehouseNode, string>((WarehouseNode x) => x.Id, StringComparer.OrdinalIgnoreCase))
		{
			List<ServersideQoLZDO> list2 = StoresForNode(item, containers, markerRange2, registry);
			List<ItemData> list3 = (from x in list2.SelectMany(delegate(ServersideQoLZDO x)
				{
					IInventory val = InventoryFor(registry, x);
					IEnumerable<ItemData> enumerable = ((val != null) ? val.Items : null);
					return enumerable ?? Enumerable.Empty<ItemData>();
				})
				where x != null && x.m_stack > 0 && x.m_shared != null
				select x).ToList();
			int num3 = list3.Sum((ItemData x) => x.m_stack);
			num += list2.Count;
			num2 += num3;
			foreach (ItemData item2 in list3)
			{
				hashSet.Add(item2.m_shared.m_name);
			}
			list.Add(new BoardMessage(item.Id.ToUpperInvariant(), $"{FormatAmount(num3)} / {list2.Count}K", "#80ffff"));
		}
		list.Add(new BoardMessage(WarehouseLocalization.Text(language, "network.warehouses"), network.Count((WarehouseNode x) => x.Valid).ToString(CultureInfo.InvariantCulture), "#80ff80"));
		list.Add(new BoardMessage(WarehouseLocalization.Text(language, "network.chests_world"), num.ToString(CultureInfo.InvariantCulture), "#80ff80"));
		list.Add(new BoardMessage(WarehouseLocalization.Text(language, "network.stock_world"), FormatAmount(num2), "#80ff80"));
		list.Add(new BoardMessage(WarehouseLocalization.Text(language, "network.types_world"), hashSet.Count.ToString(CultureInfo.InvariantCulture), "#80ff80"));
		_networkOverview = new NetworkOverview(text, list);
		_networkOverviewAt = WarehouseClock.UtcNow;
		_networkOverviewDirty = false;
		return _networkOverview;
	}

	private void WriteStatisticsBoard(string id, string language, IEnumerable<ServersideQoLZDO> statsSigns, WarehouseStatsHistory history, List<ServersideQoLZDO> stores, ContainerRegistryProcessor registry, IReadOnlyList<Peer> peers, List<WarehouseNode> network, List<ServersideQoLZDO> containers, float markerRange2)
	{
		foreach (IGrouping<string, ServersideQoLZDO> item in from x in statsSigns
			where !string.IsNullOrWhiteSpace(x.ZDO.GetString("ModerWarehouse.StatsGroup", string.Empty))
			group x by x.ZDO.GetString("ModerWarehouse.StatsGroup", string.Empty))
		{
			Dictionary<int, ServersideQoLZDO> dictionary = (from x in item
				group x by x.ZDO.GetInt("ModerWarehouse.StatsCell", -1)).ToDictionary((IGrouping<int, ServersideQoLZDO> x) => x.Key, (IGrouping<int, ServersideQoLZDO> x) => x.First());
			if (!dictionary.TryGetValue(12, out var value))
			{
				continue;
			}
			CaptureStatsControls(item.Key, dictionary);
			StatsControl statsControl = null;
			if (_statsControls.TryGetValue(item.Key, out var value2))
			{
				if ((WarehouseClock.UtcNow - value2.ChangedAt).TotalSeconds >= 60.0)
				{
					_statsControls.Remove(item.Key);
				}
				else
				{
					statsControl = value2;
				}
			}
			float distance = Mathf.Max(1f, ConfigBase<WarehouseConfig>.Instance.BoardWakeDistance.Value);
			bool num = IsPanelAwake(peers, dictionary.Values, distance);
			WriteBoardCell(dictionary, 0, "<color=#80ffff>" + WarehouseLocalization.Text(language, "stats.title") + "\n" + id.ToUpperInvariant());
			if (!num)
			{
				WriteBoardCell(dictionary, 1, "<color=#8090a0>" + WarehouseLocalization.Text(language, "board.display_sleeping"));
				WriteBoardCell(dictionary, 2, "<color=white>" + WarehouseLocalization.Format(language, "board.wake", distance.ToString("0.#", CultureInfo.InvariantCulture)));
				WriteBoardCell(dictionary, 3, "<color=#8090a0>90 " + WarehouseLocalization.Text(language, "stats.days_short"));
				SleepDisplayPolicy.Layout layout = SleepDisplayPolicy.Create(WarehouseLocalization.Text(language, "stats.sleep_word"), 12);
				for (int num2 = 0; num2 < 12; num2++)
				{
					string arg = ((num2 < layout.LabelLength) ? "#80ffff" : "#8090a0");
					WriteBoardCell(dictionary, num2 + 4, $"<color={arg}><size=180%>{layout.Text[num2]}");
				}
				continue;
			}
			double num3 = Math.Max(3f, ConfigBase<WarehouseConfig>.Instance.BoardPageSeconds.Value);
			int num4 = ((network.Count((WarehouseNode x) => x.Valid) > 1) ? 5 : 4);
			int num5 = ((statsControl != null && statsControl.Page.HasValue) ? (statsControl.Page.Value - 1) : ((int)(WarehouseClock.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds / num3) % num4));
			int num6 = statsControl?.Days ?? (num5 switch
			{
				3 => 90, 
				0 => 1, 
				_ => 30, 
			});
			int selectedDays = num6;
			Dictionary<int, ServersideQoLZDO> cells = dictionary;
			string text = WarehouseLocalization.Text(language, "stats.period");
			string text2 = ((statsControl == null || !statsControl.Days.HasValue) ? (num5 switch
			{
				1 => "7 / 30 / 90", 
				0 => WarehouseLocalization.Text(language, "stats.today"), 
				_ => WarehouseLocalization.Format(language, "stats.days", selectedDays), 
			}) : WarehouseLocalization.Format(language, "stats.days", selectedDays));
			WriteBoardCell(cells, 1, "<color=white>" + text + "\n" + text2);
			WriteBoardCell(dictionary, 2, string.Format("<color=white>{0}\n{1} / {2}", WarehouseLocalization.Text(language, "board.page"), num5 + 1, num4));
			WriteBoardCell(dictionary, 3, "<color=white>" + WarehouseLocalization.Text(language, "stats.tracked_since") + "\n" + DayLabel(history.FirstDayKey));
			List<WarehouseStatsHistory.Day> source = history.Window(selectedDays);
			Dictionary<string, int> incoming = history.SumItems(selectedDays, input: true);
			Dictionary<string, int> outgoing = history.SumItems(selectedDays, input: false);
			List<BoardMessage> list = new List<BoardMessage>();
			switch (num5)
			{
			case 0:
			{
				int num7 = source.Sum((WarehouseStatsHistory.Day x) => x.Input);
				int num8 = source.Sum((WarehouseStatsHistory.Day x) => x.Output);
				int amount = history.CurrentStock.Values.Sum();
				int num9 = stores.Count(delegate(ServersideQoLZDO x)
				{
					IInventory val = InventoryFor(registry, x);
					return val != null && val.Items.Any((ItemData i) => i != null && i.m_stack > 0);
				});
				double num10 = ((stores.Count == 0) ? 0.0 : (100.0 * (double)num9 / (double)stores.Count));
				list.Add(Metric(language, "stats.input", FormatAmount(num7), "#80ff80"));
				list.Add(Metric(language, "stats.output", FormatAmount(num8), "#ffb347"));
				list.Add(SignedMetric(language, "stats.balance", num7 - num8));
				list.Add(Metric(language, "stats.per_day", FormatAmount(num7 + num8), "#80ffff"));
				list.Add(ItemMessage(language, "stats.top_input", incoming, "#80ff80"));
				list.Add(ItemMessage(language, "stats.top_output", outgoing, "#ffb347"));
				list.Add(Metric(language, "stats.current", FormatAmount(amount), "#80ffff"));
				list.Add(Metric(language, "stats.item_types", history.CurrentStock.Count.ToString(), "white"));
				list.Add(Metric(language, "stats.chests_now", num9 + " / " + stores.Count, "white"));
				list.Add(Metric(language, "stats.avg_use", num10.ToString("0", CultureInfo.InvariantCulture) + "%", "#80ffff"));
				string value3 = ((num8 > 0) ? ((double)num7 / (double)num8).ToString("0.00", CultureInfo.InvariantCulture) : ((num7 > 0) ? "∞" : "-"));
				list.Add(Metric(language, "stats.ratio", value3, "#80ffff"));
				list.Add(Metric(language, "stats.days_tracked", history.TrackedDays.ToString(), "white"));
				break;
			}
			case 1:
			{
				int[] array = ((statsControl != null && statsControl.Days.HasValue) ? new int[3]
				{
					selectedDays,
					-selectedDays,
					90
				} : new int[3] { 7, 30, 90 });
				foreach (int num11 in array)
				{
					List<WarehouseStatsHistory.Day> source2 = ((num11 < 0) ? history.PreviousWindow(-num11) : history.Window(num11));
					int num12 = source2.Sum((WarehouseStatsHistory.Day x) => x.Input);
					int num13 = source2.Sum((WarehouseStatsHistory.Day x) => x.Output);
					string value4 = ((num11 < 0) ? WarehouseLocalization.Text(language, "stats.previous") : WarehouseLocalization.Format(language, "stats.days", num11));
					list.Add(Metric(language, "stats.period", value4, "white"));
					list.Add(Metric(language, "stats.input", FormatAmount(num12), "#80ff80"));
					list.Add(Metric(language, "stats.output", FormatAmount(num13), "#ffb347"));
					list.Add(SignedMetric(language, "stats.balance", num12 - num13));
				}
				break;
			}
			case 2:
			{
				int value6;
				int value7;
				Dictionary<string, int> dictionary2 = incoming.Keys.Union(outgoing.Keys).ToDictionary((string k) => k, (string k) => (incoming.TryGetValue(k, out value6) ? value6 : 0) - (outgoing.TryGetValue(k, out value7) ? value7 : 0));
				List<KeyValuePair<string, int>> values = dictionary2.Where(delegate(KeyValuePair<string, int> x)
				{
					KeyValuePair<string, int> keyValuePair3 = x;
					return keyValuePair3.Value > 0;
				}).OrderByDescending(delegate(KeyValuePair<string, int> x)
				{
					KeyValuePair<string, int> keyValuePair3 = x;
					return keyValuePair3.Value;
				}).Take(2)
					.ToList();
				List<KeyValuePair<string, int>> values2 = dictionary2.Where(delegate(KeyValuePair<string, int> x)
				{
					KeyValuePair<string, int> keyValuePair3 = x;
					return keyValuePair3.Value < 0;
				}).OrderBy(delegate(KeyValuePair<string, int> x)
				{
					KeyValuePair<string, int> keyValuePair3 = x;
					return keyValuePair3.Value;
				}).Take(2)
					.ToList();
				bool flag = history.TrackedDays >= Math.Min(7, selectedDays);
				var anon = (from x in (flag ? dictionary2 : new Dictionary<string, int>()).Where(delegate(KeyValuePair<string, int> x)
					{
						KeyValuePair<string, int> keyValuePair3 = x;
						if (keyValuePair3.Value < 0)
						{
							Dictionary<string, int> currentStock = history.CurrentStock;
							keyValuePair3 = x;
							return currentStock.ContainsKey(keyValuePair3.Key);
						}
						return false;
					}).Select(delegate(KeyValuePair<string, int> x)
					{
						KeyValuePair<string, int> keyValuePair3 = x;
						string key = keyValuePair3.Key;
						Dictionary<string, int> currentStock = history.CurrentStock;
						keyValuePair3 = x;
						double num16 = (double)currentStock[keyValuePair3.Key] * (double)selectedDays;
						keyValuePair3 = x;
						return new
						{
							Key = key,
							Days = num16 / (double)(-keyValuePair3.Value)
						};
					})
					orderby x.Days
					select x).FirstOrDefault();
				list.Add(TokenAt(language, "stats.rising", values, 0, "#80ff80"));
				list.Add(TokenAt(language, "stats.rising", values, 1, "#80ff80"));
				list.Add(TokenAt(language, "stats.falling", values2, 0, "#ff6666"));
				list.Add(TokenAt(language, "stats.falling", values2, 1, "#ff6666"));
				list.Add(Metric(language, "stats.empty_first", (anon == null) ? "-" : LocalizedTokenName(anon.Key, language), "#ffb347"));
				list.Add(Metric(language, "stats.eta", (anon == null) ? "-" : (anon.Days.ToString("0.#", CultureInfo.InvariantCulture) + " d"), "#ffb347"));
				List<WarehouseStatsHistory.Day> source3 = history.Window(selectedDays);
				List<WarehouseStatsHistory.Day> source4 = history.PreviousWindow(selectedDays);
				list.Add(Metric(language, "stats.input_change", PercentChange(language, source3.Sum((WarehouseStatsHistory.Day x) => x.Input), source4.Sum((WarehouseStatsHistory.Day x) => x.Input)), "#80ffff"));
				list.Add(Metric(language, "stats.output_change", PercentChange(language, source3.Sum((WarehouseStatsHistory.Day x) => x.Output), source4.Sum((WarehouseStatsHistory.Day x) => x.Output)), "#80ffff"));
				list.Add(ItemMessage(language, "stats.top_input", incoming, "#80ff80"));
				list.Add(ItemMessage(language, "stats.top_output", outgoing, "#ffb347"));
				string text3 = Math.Min(history.TrackedDays, selectedDays) + " / " + selectedDays + " d";
				list.Add(Metric(language, "stats.confidence", flag ? text3 : (WarehouseLocalization.Text(language, "stats.not_enough") + " " + text3), flag ? "#80ff80" : "#ffb347"));
				list.Add(Metric(language, "stats.net_day", FormatSignedAmount((source.Sum((WarehouseStatsHistory.Day x) => x.Input) - source.Sum((WarehouseStatsHistory.Day x) => x.Output)) / Math.Max(1, selectedDays)), "white"));
				break;
			}
			case 3:
			{
				WarehouseStatsHistory.Day day = source.OrderByDescending((WarehouseStatsHistory.Day x) => x.Input).FirstOrDefault();
				WarehouseStatsHistory.Day day2 = source.OrderByDescending((WarehouseStatsHistory.Day x) => x.Output).FirstOrDefault();
				WarehouseStatsHistory.Day day3 = source.OrderByDescending((WarehouseStatsHistory.Day x) => x.Stock).FirstOrDefault();
				WarehouseStatsHistory.Day day4 = source.OrderBy((WarehouseStatsHistory.Day x) => x.Stock).FirstOrDefault();
				KeyValuePair<string, int> keyValuePair = incoming.OrderByDescending((KeyValuePair<string, int> x) => x.Value).FirstOrDefault();
				KeyValuePair<string, int> keyValuePair2 = outgoing.OrderByDescending((KeyValuePair<string, int> x) => x.Value).FirstOrDefault();
				list.Add(Metric(language, "stats.busy_in", (day == null) ? "-" : FormatAmount(day.Input), "#80ff80"));
				list.Add(Metric(language, "stats.date", (day == null) ? "-" : DayLabel(day.Key), "white"));
				list.Add(Metric(language, "stats.busy_out", (day2 == null) ? "-" : FormatAmount(day2.Output), "#ffb347"));
				list.Add(Metric(language, "stats.date", (day2 == null) ? "-" : DayLabel(day2.Key), "white"));
				list.Add(Metric(language, "stats.peak", (day3 == null) ? "-" : FormatAmount(day3.Stock), "#80ff80"));
				list.Add(Metric(language, "stats.date", (day3 == null) ? "-" : DayLabel(day3.Key), "white"));
				list.Add(Metric(language, "stats.low", (day4 == null) ? "-" : FormatAmount(day4.Stock), "#ffb347"));
				list.Add(Metric(language, "stats.date", (day4 == null) ? "-" : DayLabel(day4.Key), "white"));
				list.Add(Metric(language, "stats.top_input", string.IsNullOrEmpty(keyValuePair.Key) ? "-" : LocalizedTokenName(keyValuePair.Key, language), "#80ff80"));
				list.Add(Metric(language, "stats.amount", string.IsNullOrEmpty(keyValuePair.Key) ? "-" : FormatAmount(keyValuePair.Value), "white"));
				list.Add(Metric(language, "stats.top_output", string.IsNullOrEmpty(keyValuePair2.Key) ? "-" : LocalizedTokenName(keyValuePair2.Key, language), "#ffb347"));
				list.Add(Metric(language, "stats.amount", string.IsNullOrEmpty(keyValuePair2.Key) ? "-" : FormatAmount(keyValuePair2.Value), "white"));
				break;
			}
			default:
			{
				NetworkOverview networkOverview = GetNetworkOverview(language, network, containers, markerRange2, registry);
				list.AddRange(networkOverview.Rows);
				while (list.Count < 12)
				{
					list.Add(new BoardMessage("-", "-", "white"));
				}
				WriteBoardCell(dictionary, 1, "<color=#80ffff>" + WarehouseLocalization.Text(language, "network.statistics"));
				break;
			}
			}
			for (int num14 = 0; num14 < 12; num14++)
			{
				BoardMessage boardMessage = list[num14];
				WriteBoardCell(dictionary, num14 + 4, "<color=white>" + boardMessage.Name + "\n<color=" + boardMessage.Color + ">" + boardMessage.Value);
			}
			if (dictionary.TryGetValue(-2, out value))
			{
				string text4;
				if (statsControl == null)
				{
					text4 = "<color=#80ffff>" + WarehouseLocalization.Text(language, "stats.control") + "\n<color=white>E: 1-90 AUTO";
				}
				else
				{
					int num15 = Math.Max(0, 60 - (int)(WarehouseClock.UtcNow - statsControl.ChangedAt).TotalSeconds);
					string text5 = (statsControl.Days.HasValue ? WarehouseLocalization.Format(language, "stats.days", statsControl.Days.Value) : WarehouseLocalization.Text(language, "common.auto"));
					string text6 = (statsControl.Page.HasValue ? WarehouseLocalization.Text(language, "stats.page_" + statsControl.Page.Value) : WarehouseLocalization.Text(language, "common.auto"));
					text4 = "<color=#ffb347>" + WarehouseLocalization.Text(language, "stats.manual") + "\n<color=white>" + text5 + " · " + text6 + "\n" + WarehouseLocalization.Format(language, "stats.auto_in", num15);
				}
				WriteBoardCell(dictionary, -2, text4);
			}
		}
	}

	private void CaptureStatsControls(string group, Dictionary<int, ServersideQoLZDO> cells)
	{
		CaptureStatsControl(group, cells, 1, allowDays: true, allowPage: false);
		CaptureStatsControl(group, cells, 2, allowDays: false, allowPage: true);
		CaptureStatsControl(group, cells, -2, allowDays: true, allowPage: true);
	}

	private void CaptureStatsControl(string group, Dictionary<int, ServersideQoLZDO> cells, int cell, bool allowDays, bool allowPage)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		if (!cells.TryGetValue(cell, out var value))
		{
			return;
		}
		string text = value.ZDO.GetString("ModerWarehouse.BoardRenderedText", string.Empty);
		ServersideQoLZDO.ZDOVars vars = value.Vars;
		string text2 = PlainText(vars.GetText("")).Trim();
		if (string.IsNullOrEmpty(text) || string.Equals(text2, text, StringComparison.Ordinal))
		{
			return;
		}
		string text3 = Normalize(text2);
		if (string.IsNullOrWhiteSpace(text3) || text3.Contains("auto"))
		{
			_statsControls.Remove(group);
			this.Logger.LogInfo((object)("Statistics board '" + group + "': returned to automatic rotation."));
			return;
		}
		if (!_statsControls.TryGetValue(group, out var value2))
		{
			value2 = new StatsControl();
		}
		bool flag = false;
		int result;
		List<int> list = (from Match x in Regex.Matches(text2, "\\d+")
			select int.TryParse(x.Value, out result) ? result : (-1) into x
			where x >= 0
			select x).ToList();
		if (allowDays)
		{
			int num = list.FirstOrDefault((int x) => x >= 1 && x <= 90);
			if (num > 0)
			{
				value2.Days = num;
				flag = true;
			}
		}
		if (allowPage)
		{
			int num2 = ParseStatsPage(text3, list, allowDays);
			if (num2 > 0)
			{
				value2.Page = num2;
				flag = true;
			}
		}
		if (flag)
		{
			value2.ChangedAt = WarehouseClock.UtcNow;
			_statsControls[group] = value2;
			this.Logger.LogInfo((object)("Statistics board '" + group + "': manual period=" + (value2.Days?.ToString() ?? "AUTO") + ", page=" + (value2.Page?.ToString() ?? "AUTO") + " for 60 seconds."));
		}
	}

	internal static int ParseStatsPage(string normalized, List<int> numbers, bool combined)
	{
		if (normalized.Contains("ubersicht") || normalized.Contains("übersicht") || normalized.Contains("overview") || normalized.Contains("heute") || normalized.Contains("today"))
		{
			return 1;
		}
		if (normalized.Contains("vergleich") || normalized.Contains("compare"))
		{
			return 2;
		}
		if (normalized.Contains("trend") || normalized.Contains("prognose") || normalized.Contains("forecast"))
		{
			return 3;
		}
		if (normalized.Contains("rekord") || normalized.Contains("record"))
		{
			return 4;
		}
		object obj = ((combined && numbers.Count > 1) ? numbers.Skip(1) : numbers);
		return ((IEnumerable<int>)obj).FirstOrDefault((int x) => x >= 1 && x <= 4);
	}

	private static BoardMessage Metric(string language, string key, string value, string color)
	{
		return new BoardMessage(WarehouseLocalization.Text(language, key), value, color);
	}

	private static BoardMessage SignedMetric(string language, string key, int value)
	{
		return Metric(language, key, ((value > 0) ? "+" : string.Empty) + FormatSignedAmount(value), (value >= 0) ? "#80ff80" : "#ff6666");
	}

	private static BoardMessage TokenAt(string language, string key, List<KeyValuePair<string, int>> values, int index, string color)
	{
		if (index >= values.Count)
		{
			return Metric(language, key, "-", color);
		}
		return TokenMessage(language, key, values[index].Key, values[index].Value, color);
	}

	internal static string PercentChange(string language, int current, int previous)
	{
		if (previous <= 0)
		{
			if (current <= 0)
			{
				return "-";
			}
			return WarehouseLocalization.Text(language, "stats.new");
		}
		double num = 100.0 * (double)(current - previous) / (double)previous;
		return ((num > 0.0) ? "+" : string.Empty) + num.ToString("0", CultureInfo.InvariantCulture) + "%";
	}

	internal static string DayLabel(int key)
	{
		return (key % 100).ToString("00", CultureInfo.InvariantCulture) + "." + (key / 100 % 100).ToString("00", CultureInfo.InvariantCulture) + ".";
	}

	private static BoardMessage ItemMessage(string language, string key, Dictionary<string, int> values, string color)
	{
		KeyValuePair<string, int> keyValuePair = values.OrderByDescending((KeyValuePair<string, int> x) => x.Value).FirstOrDefault();
		return TokenMessage(language, key, keyValuePair.Key, keyValuePair.Value, color);
	}

	private static BoardMessage TokenMessage(string language, string key, string token, int value, string color)
	{
		return new BoardMessage(WarehouseLocalization.Text(language, key), string.IsNullOrWhiteSpace(token) ? "-" : (LocalizedTokenName(token, language) + " " + ((value > 0) ? "+" : string.Empty) + FormatSignedAmount(value)), color);
	}

	private static Dictionary<string, StockEntry> CloneSnapshot(Dictionary<string, StockEntry> source)
	{
		return source.ToDictionary<KeyValuePair<string, StockEntry>, string, StockEntry>((KeyValuePair<string, StockEntry> x) => x.Key, (KeyValuePair<string, StockEntry> x) => new StockEntry(x.Value.Token, x.Value.Name, x.Value.Amount), StringComparer.Ordinal);
	}

	private static List<StockChange> CalculateChanges(Dictionary<string, StockEntry> before, Dictionary<string, StockEntry> after)
	{
		before = before ?? new Dictionary<string, StockEntry>(StringComparer.OrdinalIgnoreCase);
		after = after ?? new Dictionary<string, StockEntry>(StringComparer.OrdinalIgnoreCase);
		return (from x in before.Keys.Union<string>(after.Keys, StringComparer.OrdinalIgnoreCase).Select(delegate(string key)
			{
				StockEntry value;
				StockEntry stockEntry = (before.TryGetValue(key, out value) ? value : null);
				StockEntry value2;
				StockEntry stockEntry2 = (after.TryGetValue(key, out value2) ? value2 : null);
				return new StockChange(stockEntry2?.Name ?? stockEntry?.Name ?? key, (stockEntry2?.Amount ?? 0) - (stockEntry?.Amount ?? 0));
			})
			where x.Amount != 0
			orderby Math.Abs(x.Amount) descending
			select x).ThenBy<StockChange, string>((StockChange x) => x.Name, StringComparer.CurrentCultureIgnoreCase).Take(12).ToList();
	}

	private static void WriteBoardMessage(Dictionary<int, ServersideQoLZDO> cells, string id, string language, string heading, List<BoardMessage> messages, string capacityColor, string capacityLabel)
	{
		WriteBoardCell(cells, 0, "<color=#80ffff>" + WarehouseLocalization.Text(language, "board.warehouse") + "\n" + id.ToUpperInvariant());
		WriteBoardCell(cells, 1, "<color=#ffb347>" + heading);
		WriteBoardCell(cells, 2, string.Format("<color=white>{0}\n{1}", WarehouseLocalization.Text(language, "board.positions"), messages.Count));
		WriteBoardCell(cells, 3, "<color=" + capacityColor + ">" + WarehouseLocalization.Text(language, "board.chests") + "\n" + capacityLabel);
		for (int i = 0; i < 12; i++)
		{
			string text = ((i < messages.Count) ? ("<color=white>" + messages[i].Name + "\n<color=" + messages[i].Color + ">" + messages[i].Value) : "<color=white>-");
			WriteBoardCell(cells, i + 4, text);
		}
	}

	private static void WriteRequestSearchBoard(Dictionary<int, ServersideQoLZDO> cells, string id, string language, RequestSearchNotice search, string capacityColor, string capacityLabel)
	{
		int num = Math.Max(1, (search.Matches.Count + 12 - 1) / 12);
		double num2 = ((num == 1) ? 10.0 : 8.0);
		int num3 = Math.Min(num - 1, (int)((WarehouseClock.UtcNow - search.StartedAt).TotalSeconds / num2) % num);
		List<RequestSuggestion> list = search.Matches.Skip(num3 * 12).Take(12).ToList();
		WriteBoardCell(cells, 0, "<color=#80ffff>" + WarehouseLocalization.Text(language, "board.warehouse") + "\n" + id.ToUpperInvariant());
		WriteBoardCell(cells, 1, "<color=#ffb347>" + WarehouseLocalization.Text(language, "request.suggestions") + "\n" + search.Query.ToUpperInvariant());
		WriteBoardCell(cells, 2, string.Format("<color=white>{0}\n{1} / {2}", WarehouseLocalization.Text(language, "board.page"), num3 + 1, num));
		WriteBoardCell(cells, 3, "<color=" + capacityColor + ">" + WarehouseLocalization.Text(language, "board.chests") + "\n" + capacityLabel);
		for (int i = 0; i < 12; i++)
		{
			string text = "<color=white>-";
			if (i < list.Count)
			{
				RequestSuggestion match = list[i];
				string value;
				string text2 = string.Join(" | ", match.Amounts.OrderBy((KeyValuePair<string, int> pair) => (!pair.Key.Equals(id, StringComparison.OrdinalIgnoreCase)) ? 1 : 0).ThenBy<KeyValuePair<string, int>, string>((KeyValuePair<string, int> pair) => pair.Key, StringComparer.OrdinalIgnoreCase).Select(delegate(KeyValuePair<string, int> pair)
				{
					string[] array = new string[5];
					KeyValuePair<string, int> keyValuePair = pair;
					array[0] = keyValuePair.Key.ToUpperInvariant();
					array[1] = " ";
					keyValuePair = pair;
					array[2] = FormatAmount(keyValuePair.Value);
					array[3] = " ";
					Dictionary<string, string> statuses = match.Statuses;
					keyValuePair = pair;
					array[4] = (statuses.TryGetValue(keyValuePair.Key, out value) ? value : string.Empty);
					return string.Concat(array);
				}));
				text = "<color=#80ff80>" + match.Name + "\n<color=#80ffff>" + text2;
			}
			WriteBoardCell(cells, i + 4, text);
		}
	}

	private static void WriteBoardCell(Dictionary<int, ServersideQoLZDO> cells, int cell, string text)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		if (!cells.TryGetValue(cell, out var value))
		{
			return;
		}
		ServersideQoLZDO.ZDOVars vars = value.Vars;
		if (!string.Equals(vars.GetText(""), text, StringComparison.Ordinal))
		{
			if (!value.IsOwnerOrUnassigned())
			{
				value.ClaimOwnership();
			}
			vars = value.Vars;
			vars.SetText(text);
			value.ZDO.Set("ModerWarehouse.BoardRenderedText", PlainText(text).Trim());
		}
	}

	internal static string Initial(string name)
	{
		if (!string.IsNullOrWhiteSpace(name))
		{
			return char.ToUpperInvariant(name.Trim()[0]).ToString();
		}
		return "-";
	}

	internal static string FormatAmount(int amount)
	{
		if (amount < 1000)
		{
			return amount.ToString(CultureInfo.InvariantCulture);
		}
		if (amount < 1000000)
		{
			return ((double)amount / 1000.0).ToString("0.#", CultureInfo.InvariantCulture) + "k";
		}
		return ((double)amount / 1000000.0).ToString("0.#", CultureInfo.InvariantCulture) + "M";
	}

	internal static string FormatSignedAmount(int amount)
	{
		if (amount >= 0)
		{
			return FormatAmount(amount);
		}
		return "-" + FormatAmount(Math.Abs(amount));
	}

	private void WriteWarehouseInfo(string id, string language, IEnumerable<ServersideQoLZDO> infoSigns, List<ServersideQoLZDO> stores, Dictionary<ServersideQoLZDO, string> labels, ContainerRegistryProcessor registry)
	{
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0184: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		int num = 0;
		int num2 = 0;
		foreach (ServersideQoLZDO store in stores)
		{
			if (registry.GetState(store) != null && !InventoryFor(registry, store).Items.Any((ItemData item) => item != null && item.m_stack > 0))
			{
				if (labels.ContainsKey(store))
				{
					num2++;
				}
				else
				{
					num++;
				}
			}
		}
		int num3 = Math.Max(0, stores.Count - num - num2);
		string text = "<color=white>" + WarehouseLocalization.Text(language, "board.warehouse") + " " + id.ToUpperInvariant() + "\n" + string.Format("{0} {1}\n", WarehouseLocalization.Text(language, "info.free"), num) + string.Format("{0} {1}\n", WarehouseLocalization.Text(language, "info.empty_reserved"), num2) + string.Format("{0} {1} / {2}", WarehouseLocalization.Text(language, "info.occupied"), num3, stores.Count);
		foreach (ServersideQoLZDO item in infoSigns.Distinct())
		{
			ServersideQoLZDO.ZDOVars vars = item.Vars;
			if (!string.Equals(vars.GetText(""), text, StringComparison.Ordinal))
			{
				if (!item.IsOwnerOrUnassigned())
				{
					item.ClaimOwnership();
				}
				vars = item.Vars;
				vars.SetText(text);
				this.Logger.LogInfo((object)($"Warehouse '{id}': capacity sign updated: {num} free, " + $"{num2} reserved empty, {num3}/{stores.Count} occupied."));
			}
		}
	}

	private static bool IsRememberedRequest(ServersideQoLZDO sign)
	{
		return !string.IsNullOrWhiteSpace(sign.ZDO.GetString("ModerWarehouse.RequestWarehouse", string.Empty));
	}

	private static bool IsRememberedInfo(ServersideQoLZDO sign)
	{
		return !string.IsNullOrWhiteSpace(sign.ZDO.GetString("ModerWarehouse.InfoWarehouse", string.Empty));
	}

	private static bool IsRememberedBoard(ServersideQoLZDO sign)
	{
		return !string.IsNullOrWhiteSpace(sign.ZDO.GetString("ModerWarehouse.BoardWarehouse", string.Empty));
	}

	private static bool IsRememberedStats(ServersideQoLZDO sign)
	{
		return !string.IsNullOrWhiteSpace(sign.ZDO.GetString("ModerWarehouse.StatsWarehouse", string.Empty));
	}

	private ServersideQoLZDO LinkedOrNearestContainer(ServersideQoLZDO sign, List<ServersideQoLZDO> containers, float range2)
	{
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		float num = sign.ZDO.GetFloat("ModerWarehouse.AutoChestX", float.NaN);
		float num2 = sign.ZDO.GetFloat("ModerWarehouse.AutoChestY", float.NaN);
		float num3 = sign.ZDO.GetFloat("ModerWarehouse.AutoChestZ", float.NaN);
		if (!float.IsNaN(num) && !float.IsNaN(num2) && !float.IsNaN(num3))
		{
			Vector3 val = default(Vector3);
			val = new Vector3(num, num2, num3);
			ServersideQoLZDO val2 = Lookup(containers).Nearest(num, num2, num3, 0.01f);
			if (val2 != null)
			{
				Vector3 val3 = val2.ZDO.GetPosition() - val;
				if (val3.sqrMagnitude < 0.01f)
				{
					return val2;
				}
			}
			return null;
		}
		return NearestContainer(sign, containers, range2);
	}

	private bool LabelMatches(ServersideQoLZDO chest, string label, ItemData item, ContainerRegistryProcessor registry)
	{
		string b = item?.m_shared?.m_name ?? string.Empty;
		if (_labelItemTokens.TryGetValue(chest, out var value))
		{
			return string.Equals(value, b, StringComparison.Ordinal);
		}
		string normalized = Normalize(label);
		if (!ItemAliases(item).Any((string alias) => alias == normalized))
		{
			return false;
		}
		if (AliasOwners.TryGetValue(normalized, out var value2) && value2.Count > 1)
		{
			return HasItem(InventoryFor(registry, chest), item);
		}
		return true;
	}

	private static bool ItemMatchesQuery(ItemData item, string query)
	{
		string normalized = Normalize(query);
		return ItemAliases(item).Any((string alias) => alias == normalized);
	}

	private static IEnumerable<string> ItemAliases(ItemData item)
	{
		string prefab = Normalize(ItemName(item));
		string token = Normalize(item?.m_shared?.m_name?.Replace("$item_", string.Empty));
		if (prefab.Length > 0)
		{
			yield return prefab;
		}
		if (token.Length > 0 && token != prefab)
		{
			yield return token;
		}
		if (item?.m_shared != null && EnglishNames.TryGetValue(item.m_shared.m_name, out var value) && value.Length > 0)
		{
			yield return value;
		}
		if (item?.m_shared != null && GermanNames.TryGetValue(item.m_shared.m_name, out value) && value.Length > 0)
		{
			yield return value;
		}
		foreach (string item2 in GermanAliases(prefab).Concat(GermanAliases(token)).Distinct())
		{
			yield return item2;
		}
	}

	private static void LoadGermanNames()
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Expected O, but got Unknown
		if (_germanNamesLoaded || (UnityEngine.Object)ObjectDB.instance == (UnityEngine.Object)null)
		{
			return;
		}
		_germanNamesLoaded = true;
		Localization instance = Localization.instance;
		string selectedLanguage = instance.GetSelectedLanguage();
		try
		{
			instance.SetLanguage("English");
			CaptureLocalizedNames(instance, EnglishNames, EnglishDisplayNames);
			CapturePieceNames(instance, PieceEnglishNames);
			instance.SetLanguage("German");
			CaptureLocalizedNames(instance, GermanNames, GermanDisplayNames);
			CapturePieceNames(instance, PieceGermanNames);
			BuildAliasOwners();
			File.WriteAllLines(Path.Combine(Paths.ConfigPath, "ModerWarehouse-items-en-de.tsv"), new string[1] { "Prefab\tToken\tEnglish\tDeutsch" }.Concat(from token in GermanNames.Keys
				orderby token
				select Value(PrefabNames, token) + "\t" + token + "\t" + Value(EnglishNames, token) + "\t" + Value(GermanNames, token)));
		}
		finally
		{
			instance.SetLanguage(selectedLanguage);
		}
	}

	private static void CaptureLocalizedNames(Localization localization, Dictionary<string, string> destination, Dictionary<string, string> displayNames)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Expected O, but got Unknown
		foreach (GameObject item in ObjectDB.instance.m_items)
		{
			string text = (((UnityEngine.Object)item != (UnityEngine.Object)null) ? item.GetComponent<ItemDrop>() : null)?.m_itemData?.m_shared?.m_name;
			if (!string.IsNullOrEmpty(text))
			{
				string value = localization.Localize(text);
				destination[text] = Normalize(value);
				if (displayNames != null)
				{
					displayNames[text] = value;
				}
				PrefabNames[text] = ((UnityEngine.Object)item).name;
			}
		}
	}

	private static string Value(Dictionary<string, string> values, string key)
	{
		if (!values.TryGetValue(key, out var value))
		{
			return string.Empty;
		}
		return value;
	}

	private static void BuildAliasOwners()
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Expected O, but got Unknown
		AliasOwners.Clear();
		foreach (GameObject item in ObjectDB.instance.m_items)
		{
			ItemData val = (((UnityEngine.Object)item != (UnityEngine.Object)null) ? item.GetComponent<ItemDrop>() : null)?.m_itemData;
			string text = val?.m_shared?.m_name;
			if (string.IsNullOrWhiteSpace(text))
			{
				continue;
			}
			foreach (string item2 in (from x in ItemAliases(val)
				where x.Length > 0
				select x).Distinct())
			{
				if (!AliasOwners.TryGetValue(item2, out var value))
				{
					HashSet<string> hashSet = (AliasOwners[item2] = new HashSet<string>(StringComparer.Ordinal));
					value = hashSet;
				}
				value.Add(text);
			}
		}
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
		{
			char c3 = name[0];
			if (c3 != 'b')
			{
				if (c3 != 'c')
				{
					if (c3 != 's' || !(name == "silver"))
					{
						yield break;
					}
					goto IL_0545;
				}
				if (!(name == "copper"))
				{
					yield break;
				}
				goto IL_059b;
			}
			if (name == "bronze")
			{
				yield return "bronze";
				yield return "bronzebarren";
			}
			yield break;
		}
		case 9:
		{
			char c = name[1];
			if (c != 'c')
			{
				if (c != 'i')
				{
					if (c != 'o' || !(name == "copperore"))
					{
						yield break;
					}
					goto IL_059b;
				}
				if (!(name == "silverore"))
				{
					yield break;
				}
				goto IL_0545;
			}
			if (!(name == "scrapiron"))
			{
				yield break;
			}
			goto IL_04ef;
		}
		case 4:
			switch (name[0])
			{
			default:
				yield break;
			case 'w':
				if (name == "wood")
				{
					yield return "holz";
				}
				yield break;
			case 'c':
				if (name == "coal")
				{
					yield return "kohle";
				}
				yield break;
			case 'i':
				break;
			}
			if (!(name == "iron"))
			{
				yield break;
			}
			goto IL_04ef;
		case 7:
		{
			char c2 = name[0];
			if (c2 != 'f')
			{
				switch (c2)
				{
				default:
					yield break;
				case 't':
					if (name == "thistle")
					{
						yield return "distel";
					}
					yield break;
				case 'i':
					break;
				}
				if (!(name == "ironore"))
				{
					yield break;
				}
				goto IL_04ef;
			}
			if (!(name == "feather"))
			{
				yield break;
			}
			break;
		}
		case 8:
		{
			char c4 = name[2];
			if ((uint)c4 <= 110u)
			{
				switch (c4)
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
			switch (c4)
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
			if (!(name == "blackmetal"))
			{
				yield break;
			}
			goto IL_04b5;
		case 15:
			if (!(name == "blackmetalscrap"))
			{
				yield break;
			}
			goto IL_04b5;
		case 11:
			{
				if (name == "ancientbark")
				{
					yield return "uralteborke";
				}
				yield break;
			}
			IL_04b5:
			yield return "schwarzmetall";
			yield return "schwarzmetallbarren";
			yield break;
			IL_04ef:
			yield return "eisen";
			yield return "eisenbarren";
			yield return "altmetall";
			yield break;
			IL_0545:
			yield return "silber";
			yield return "silbererz";
			yield return "silberbarren";
			yield break;
			IL_059b:
			yield return "kupfer";
			yield return "kupfererz";
			yield return "kupferbarren";
			yield break;
		}
		yield return "feder";
		yield return "federn";
	}

	private static string ItemName(ItemData item)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Expected O, but got Unknown
		object obj;
		if (!((UnityEngine.Object)(item?.m_dropPrefab) != (UnityEngine.Object)null))
		{
			obj = item?.m_shared?.m_name;
			if (obj == null)
			{
				return string.Empty;
			}
		}
		else
		{
			obj = ((UnityEngine.Object)item.m_dropPrefab).name;
		}
		return (string)obj;
	}

	internal static string Normalize(string value)
	{
		StringBuilder stringBuilder = new StringBuilder();
		string text = Regex.Replace(value ?? string.Empty, "<.*?>", string.Empty).ToLowerInvariant();
		foreach (char c in text)
		{
			if (char.IsLetterOrDigit(c))
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString();
	}

	private static bool IsSignObject(ZDO zdo)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Expected O, but got Unknown
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Expected O, but got Unknown
		int prefab = zdo.GetPrefab();
		if (SignPrefabs.TryGetValue(prefab, out var value))
		{
			return value;
		}
		GameObject val = (((UnityEngine.Object)(object)ZNetScene.instance != (UnityEngine.Object)null) ? ZNetScene.instance.GetPrefab(prefab) : null);
		if ((UnityEngine.Object)val == (UnityEngine.Object)null)
		{
			return true;
		}
		value = (UnityEngine.Object)val.GetComponent<Sign>() != (UnityEngine.Object)null;
		SignPrefabs[prefab] = value;
		return value;
	}

	internal static string PlainText(string value)
	{
		return Regex.Replace(value ?? string.Empty, "<.*?>", string.Empty);
	}

	// Local patch 9: ServersideQoL ContainerSigns writes a chest's contents between these markers.
	internal static bool IsContainerSignsList(string raw)
	{
		return (raw ?? string.Empty).IndexOf("<i ls>", StringComparison.Ordinal) >= 0;
	}

	private const string ContainerSignsGuid = "ArgusMagnus.ServersideQoL.ContainerSigns";

	// A sign's text names what its chest holds: not empty or punctuation only (a "..." or "•" sign
	// waiting to be adopted), not a ContainerSigns contents list, not a warehouse marker.
	internal static bool IsLabelText(string raw)
	{
		string plain = PlainText(raw);
		return !IsContainerSignsList(raw) && !IsBlankLabel(plain) && !Marker.IsMatch(plain) && !IgnoreMarker.IsMatch(plain) && !DeleteMarker.IsMatch(plain) && !CleanMarker.IsMatch(plain);
	}

	// The label sign of each managed chest, from signs whose text is a label (IsLabelText): one per
	// chest, the warehouse's own automatic label before a player's, else the first. Signs of no chest
	// or of a chest outside managed are left out.
	internal static Dictionary<TChest, TSign> ChooseChestLabels<TChest, TSign>(IEnumerable<(TSign Sign, TChest Chest, bool AutoManaged)> labels, ISet<TChest> managed) where TChest : class
	{
		Dictionary<TChest, (TSign Sign, bool AutoManaged)> chosen = new Dictionary<TChest, (TSign, bool)>();
		foreach ((TSign sign, TChest chest, bool autoManaged) in labels)
		{
			if (chest != null && managed.Contains(chest) && (!chosen.TryGetValue(chest, out var current) || (!current.AutoManaged && autoManaged)))
			{
				chosen[chest] = (sign, autoManaged);
			}
		}
		return chosen.ToDictionary((KeyValuePair<TChest, (TSign Sign, bool AutoManaged)> x) => x.Key, (KeyValuePair<TChest, (TSign Sign, bool AutoManaged)> x) => x.Value.Sign);
	}

	// ContainerSigns is optional: nothing here references it, it is only looked for by id.
	private static bool ContainerSignsLoaded => BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(ContainerSignsGuid);

	// Inside a warehouse the warehouse's label is the chest's text. ContainerSigns keeps the chest's
	// text (the chest ZDO's s_text) and copies it onto every sign it handles for that chest, its own
	// and the ones players connect, each time the chest's contents change, so a label written on the
	// sign alone was put back to the old text by the next deposit. Writing the label to the chest too
	// makes all of that chest's ContainerSigns signs show the label. Chests outside warehouses are
	// never in managed and keep ContainerSigns' own behaviour.
	private void AdoptContainerSigns(List<ServersideQoLZDO> managed, List<ServersideQoLZDO> containers, float range2)
	{
		if (!ContainerSignsLoaded || managed.Count == 0)
		{
			return;
		}
		IEnumerable<(ServersideQoLZDO Sign, ServersideQoLZDO Chest, bool AutoManaged)> labels = from sign in StorageSigns
			where !IsRememberedRequest(sign) && !IsRememberedInfo(sign) && !IsRememberedBoard(sign) && !IsRememberedStats(sign) && !sign.ZDO.GetBool("ModerWarehouse.HelpChild", false)
			where IsLabelText(sign.Vars.GetText("") ?? string.Empty)
			select (sign, LinkedOrNearestContainer(sign, containers, range2), sign.ZDO.GetBool("ModerWarehouse.AutoLabelManaged", false));
		foreach (KeyValuePair<ServersideQoLZDO, ServersideQoLZDO> pair in ChooseChestLabels(labels, new HashSet<ServersideQoLZDO>(managed)))
		{
			string label = pair.Value.Vars.GetText("") ?? string.Empty;
			if (string.Equals(pair.Key.Vars.GetText("") ?? string.Empty, label, StringComparison.Ordinal))
			{
				continue;
			}
			if (!pair.Key.IsOwnerOrUnassigned())
			{
				pair.Key.ClaimOwnership();
			}
			if (pair.Key.IsOwnerOrUnassigned())
			{
				pair.Key.Vars.SetText(label);
			}
		}
	}

	// Local patch 9: "Wood 521" (an item name and a count, ContainerSigns' "{0} {1}") -> "Wood", when
	// the name is a known item and the whole text is not.
	internal static bool TryStripContentCount(string plain, out string name)
	{
		name = null;
		Match match = Regex.Match((plain ?? string.Empty).Trim(), "^(?<name>.*\\D)\\s+\\d+$", RegexOptions.CultureInvariant);
		if (!match.Success)
		{
			return false;
		}
		string candidate = match.Groups["name"].Value.Trim();
		if (AliasOwners.ContainsKey(Normalize(plain)) || !AliasOwners.ContainsKey(Normalize(candidate)))
		{
			return false;
		}
		name = candidate;
		return true;
	}

	internal static bool IsBlankLabel(string value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			return !value.Any(char.IsLetterOrDigit);
		}
		return true;
	}

	private static bool Own(ContainerState state)
	{
		if (state.ZDO.IsOwnerOrUnassigned())
		{
			return true;
		}
		state.ZDO.ClaimOwnership();
		return state.ZDO.IsOwnerOrUnassigned();
	}

	private static int Move(IInventory source, IInventory destination, ItemData item, int wanted)
	{
		int max = Math.Max(1, item.m_shared.m_maxStackSize);
		int matchingStackSpace = destination.Items.Where((ItemData x) => CanStack(x, item)).Sum((ItemData x) => Math.Max(0, max - x.m_stack));
		int emptySlots = destination.Inventory.GetWidth() * destination.Inventory.GetHeight() - destination.Items.Count;
		int num = WarehouseContainerPolicy.TransferAmount(item.m_stack, wanted, max, matchingStackSpace, emptySlots);
		if (num <= 0)
		{
			return 0;
		}
		ItemData val = item.Clone();
		val.m_stack = num;
		if (!destination.Inventory.AddItem(val))
		{
			return 0;
		}
		if (!source.Inventory.RemoveItem(item, num))
		{
			destination.Inventory.RemoveItem(val, num);
			return 0;
		}
		// Local patch 8: each Save() re-serializes the whole chest and force-sends it to clients,
		// so saves are deferred and done once per chest when the warehouse cycle ends.
		PendingInventorySaves.Add(destination);
		PendingInventorySaves.Add(source);
		return num;
	}

	private static bool HasItem(IInventory inventory, ItemData item)
	{
		if (inventory != null)
		{
			return inventory.Items.Any((ItemData x) => CanStack(x, item));
		}
		return false;
	}

	private static bool CanStack(ItemData a, ItemData b)
	{
		if (a != null && b != null && a.m_shared.m_name == b.m_shared.m_name && a.m_quality == b.m_quality && a.m_worldLevel == b.m_worldLevel && a.m_variant == b.m_variant)
		{
			return Equal(a.m_customData, b.m_customData);
		}
		return false;
	}

	private IInventory InventoryFor(ContainerRegistryProcessor registry, ServersideQoLZDO zdo)
	{
		if (zdo == null)
		{
			return null;
		}
		if (_cycleInventories != null && _cycleInventories.TryGetValue(zdo, out var value))
		{
			return value;
		}
		if (!_inventoryIndex.TryGetValue(zdo, out var value2))
		{
			ContainerState state = registry.GetState(zdo);
			value2 = ((state != null) ? state.GetInventory() : null);
			if (value2 != null)
			{
				_inventoryIndex[zdo] = value2;
			}
			_cycleInventoryReads++;
		}
		if (_cycleInventories != null)
		{
			_cycleInventories[zdo] = value2;
		}
		return value2;
	}

	private PerfWindow PerformanceWindow(string id)
	{
		if (_performanceWindows.TryGetValue(id, out var value))
		{
			return value;
		}
		value = new PerfWindow
		{
			Since = WarehouseClock.UtcNow
		};
		_performanceWindows[id] = value;
		return value;
	}

	private void RecordPerformance(string id, bool awake, long elapsedMs)
	{
		PerfWindow perfWindow = PerformanceWindow(id);
		perfWindow.Cycles++;
		if (awake)
		{
			perfWindow.AwakeCycles++;
		}
		else
		{
			perfWindow.SleepingCycles++;
		}
		perfWindow.TotalMs += elapsedMs;
		perfWindow.MaxMs = Math.Max(perfWindow.MaxMs, elapsedMs);
		perfWindow.TopologyMs = Math.Max(perfWindow.TopologyMs, _cycleTopologyMs);
		perfWindow.ClassifyMs = Math.Max(perfWindow.ClassifyMs, _cycleClassifyMs);
		perfWindow.DisplayMs = Math.Max(perfWindow.DisplayMs, _cycleDisplayMs);
		perfWindow.TransferMs = Math.Max(perfWindow.TransferMs, _cycleTransferMs);
		perfWindow.Containers = _cycleContainers;
		perfWindow.Signs = _cycleSigns;
		perfWindow.Stores = _cycleStores;
		perfWindow.InventoryReads = Math.Max(perfWindow.InventoryReads, _cycleInventoryReads);
		perfWindow.Peers = Math.Max(perfWindow.Peers, _cyclePeers);
		perfWindow.PingAverage = Math.Max(perfWindow.PingAverage, _cyclePingAverage);
		perfWindow.PingMaximum = Math.Max(perfWindow.PingMaximum, _cyclePingMaximum);
		perfWindow.LocalQualityMinimum = Math.Min(perfWindow.LocalQualityMinimum, _cycleLocalQualityMinimum);
		perfWindow.RemoteQualityMinimum = Math.Min(perfWindow.RemoteQualityMinimum, _cycleRemoteQualityMinimum);
		perfWindow.OutBytesPerSecond = Math.Max(perfWindow.OutBytesPerSecond, _cycleOutBytesPerSecond);
		perfWindow.InBytesPerSecond = Math.Max(perfWindow.InBytesPerSecond, _cycleInBytesPerSecond);
		if (elapsedMs >= 50 && (!_slowSampleAt.TryGetValue(id, out var value) || (WarehouseClock.UtcNow - value).TotalSeconds >= 10.0))
		{
			_slowSampleAt[id] = WarehouseClock.UtcNow;
			AppendPerformance("SLOW", id, awake ? "awake" : "sleeping", 1, perfWindow.Skipped, elapsedMs, elapsedMs, _cycleTopologyMs, _cycleClassifyMs, _cycleDisplayMs, _cycleTransferMs, _cycleContainers, _cycleSigns, _cycleStores, _cycleInventoryReads, _cyclePeers, _cyclePingAverage, _cyclePingMaximum, _cycleLocalQualityMinimum, _cycleRemoteQualityMinimum, _cycleOutBytesPerSecond, _cycleInBytesPerSecond);
		}
		if (!((WarehouseClock.UtcNow - perfWindow.Since).TotalSeconds < 30.0))
		{
			AppendPerformance("SUMMARY", id, $"awake={perfWindow.AwakeCycles};sleeping={perfWindow.SleepingCycles}", perfWindow.Cycles, perfWindow.Skipped, (perfWindow.Cycles == 0) ? 0.0 : ((double)perfWindow.TotalMs / (double)perfWindow.Cycles), perfWindow.MaxMs, perfWindow.TopologyMs, perfWindow.ClassifyMs, perfWindow.DisplayMs, perfWindow.TransferMs, perfWindow.Containers, perfWindow.Signs, perfWindow.Stores, perfWindow.InventoryReads, perfWindow.Peers, perfWindow.PingAverage, perfWindow.PingMaximum, perfWindow.LocalQualityMinimum, perfWindow.RemoteQualityMinimum, perfWindow.OutBytesPerSecond, perfWindow.InBytesPerSecond);
			_performanceWindows[id] = new PerfWindow
			{
				Since = WarehouseClock.UtcNow
			};
		}
	}

	private bool ShouldLogTransferDiagnostic(string id)
	{
		DateTime utcNow = WarehouseClock.UtcNow;
		if (_transferDiagnosticAt.TryGetValue(id, out var value) && (utcNow - value).TotalSeconds < 30.0)
		{
			return false;
		}
		_transferDiagnosticAt[id] = utcNow;
		return true;
	}

	private void AppendPerformance(string kind, string id, string state, int cycles, int skipped, double averageMs, long maximumMs, long topologyMs, long classifyMs, long displayMs, long transferMs, int containers, int signs, int stores, int inventoryReads, int peers, int pingAverage, int pingMaximum, float localQualityMinimum, float remoteQualityMinimum, float outBytesPerSecond, float inBytesPerSecond)
	{
		if (_performanceLogDisabled || string.IsNullOrWhiteSpace(_performanceLogPath))
		{
			return;
		}
		try
		{
			string text = string.Join(" | ", WarehouseClock.UtcNow.ToString("O", CultureInfo.InvariantCulture), kind, id, state, cycles.ToString(), skipped.ToString(), averageMs.ToString("0.0", CultureInfo.InvariantCulture), maximumMs.ToString(), topologyMs.ToString(), classifyMs.ToString(), displayMs.ToString(), transferMs.ToString(), containers.ToString(), signs.ToString(), stores.ToString(), inventoryReads.ToString(), peers.ToString(), pingAverage.ToString(), pingMaximum.ToString(), localQualityMinimum.ToString("0.000", CultureInfo.InvariantCulture), remoteQualityMinimum.ToString("0.000", CultureInfo.InvariantCulture), outBytesPerSecond.ToString("0", CultureInfo.InvariantCulture), inBytesPerSecond.ToString("0", CultureInfo.InvariantCulture));
			File.AppendAllText(_performanceLogPath, text + Environment.NewLine, Encoding.UTF8);
		}
		catch (Exception ex)
		{
			_performanceLogDisabled = true;
			this.Logger.LogWarning((object)("Warehouse performance log disabled after write failure: " + ex.Message));
		}
	}

	private void CapturePeerMetrics(IReadOnlyList<Peer> peers)
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Expected O, but got Unknown
		_cyclePeers = 0;
		_cyclePingAverage = (_cyclePingMaximum = 0);
		_cycleLocalQualityMinimum = (_cycleRemoteQualityMinimum = 1f);
		_cycleOutBytesPerSecond = (_cycleInBytesPerSecond = 0f);
		if ((UnityEngine.Object)ZNet.instance == (UnityEngine.Object)null)
		{
			return;
		}
		try
		{
			float cycleLocalQualityMinimum = 0f;
			float cycleRemoteQualityMinimum = 0f;
			int val = 0;
			float val2 = 0f;
			float val3 = 0f;
			ZNet.instance.GetNetStats(out cycleLocalQualityMinimum, out cycleRemoteQualityMinimum, out val, out val2, out val3);
			_cyclePeers = Math.Max(0, ZNet.instance.GetNrOfPlayers());
			_cyclePingAverage = (_cyclePingMaximum = Math.Max(0, val));
			_cycleLocalQualityMinimum = cycleLocalQualityMinimum;
			_cycleRemoteQualityMinimum = cycleRemoteQualityMinimum;
			_cycleOutBytesPerSecond = Math.Max(0f, val2);
			_cycleInBytesPerSecond = Math.Max(0f, val3);
		}
		catch
		{
		}
	}

	private void DeleteLegacyLog(string path, string kind)
	{
		if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
		{
			return;
		}
		try
		{
			File.Delete(path);
			this.Logger.LogInfo((object)("Removed legacy warehouse " + kind + " log: " + path));
		}
		catch (Exception ex)
		{
			this.Logger.LogWarning((object)("Could not remove legacy warehouse " + kind + " log '" + path + "': " + ex.Message));
		}
	}

	private static bool Equal(Dictionary<string, string> a, Dictionary<string, string> b)
	{
		if (a == b)
		{
			return true;
		}
		if (a == null || b == null || a.Count != b.Count)
		{
			return false;
		}
		foreach (KeyValuePair<string, string> item in a)
		{
			if (!b.TryGetValue(item.Key, out var value) || value != item.Value)
			{
				return false;
			}
		}
		return true;
	}

	private List<ServersideQoLZDO> AllContainers()
	{
		if (_containers != null)
		{
			return _containers.Values.SelectMany((HashSet<ServersideQoLZDO> x) => x).Distinct().ToList();
		}
		return new List<ServersideQoLZDO>();
	}

	private static float Distance2(ServersideQoLZDO a, ServersideQoLZDO b)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = a.ZDO.GetPosition() - b.ZDO.GetPosition();
		return val.sqrMagnitude;
	}

	private static bool IsPanelAwake(IReadOnlyList<Peer> peers, IEnumerable<ServersideQoLZDO> cells, float distance)
	{
		if (peers == null)
		{
			return false;
		}
		float distance2 = distance * distance;
		List<Vector3> positions = (from cell in cells
			where cell != null
			select cell.ZDO.GetPosition()).ToList();
		return peers.Any((Peer peer) => peer != null && positions.Any(delegate(Vector3 position)
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			Vector3 val = peer.RefPos - position;
			return val.sqrMagnitude <= distance2;
		}));
	}
}
