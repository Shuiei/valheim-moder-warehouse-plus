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
	private sealed class FillOrder
	{
		public ServersideQoLZDO Sign { get; }

		public ServersideQoLZDO Chest { get; }

		public string Payload { get; }

		public FillOrder(ServersideQoLZDO sign, ServersideQoLZDO chest, string payload)
		{
			Sign = sign;
			Chest = chest;
			Payload = payload;
		}
	}

	private sealed class StatsCacheEntry
	{
		public string Raw;

		public WarehouseStatsHistory History;

		public DateTime WrittenAt;
	}

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
			Vector3 position = sign.GetPosition();
			return lookup.Nearest(position.x, position.y, position.z, range2, IsContainer, preferLastOnTie: true);
		}

		private bool IsContainer(ZDO candidate)
		{
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

		public bool Canonical { get; }

		public PersistedInputCandidate(string id, ZDO sign, ZDO input, bool canonical)
		{
			Id = id;
			Sign = sign;
			Input = input;
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

	private static readonly Regex MarkerRegex = new Regex("^\\s*\\[WH:(?<id>[A-Za-z0-9_-]+):(?<role>IN|OUT|REQ|INFO|BOARD|STATS|FILL)\\](?<payload>[\\s\\S]*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

	internal static readonly WarehouseText.MatchCache Marker = new WarehouseText.MatchCache(MarkerRegex);

	internal static readonly Regex IgnoreMarker = new Regex("^\\s*\\[WH:IGNORE\\]\\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

	internal static readonly Regex DeleteMarker = new Regex("^\\s*\\[WH:DELETE\\]\\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

	internal static readonly Regex CleanMarker = new Regex("^\\s*\\[WH:CLEAN\\]\\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly int RequestWarehouseKey = KeyHash("ModerWarehouse.RequestWarehouse");

	private static readonly int InfoWarehouseKey = KeyHash("ModerWarehouse.InfoWarehouse");

	private static readonly int BoardWarehouseKey = KeyHash("ModerWarehouse.BoardWarehouse");

	private static readonly int BoardGroupKey = KeyHash("ModerWarehouse.BoardGroup");

	private static readonly int BoardCellKey = KeyHash("ModerWarehouse.BoardCell");

	private static readonly int BoardRenderedTextKey = KeyHash("ModerWarehouse.BoardRenderedText");

	private static readonly int StatsWarehouseKey = KeyHash("ModerWarehouse.StatsWarehouse");

	private static readonly int StatsGroupKey = KeyHash("ModerWarehouse.StatsGroup");

	private static readonly int StatsCellKey = KeyHash("ModerWarehouse.StatsCell");

	private static readonly int StatsHistoryKey = KeyHash("ModerWarehouse.StatsHistory");

	private static readonly int StatsControlKey = KeyHash("ModerWarehouse.StatsControl");

	private static readonly int RequestRenderedTextKey = KeyHash("ModerWarehouse.RequestRenderedText");

	private static readonly int RequestTargetsKey = KeyHash("ModerWarehouse.RequestTargets");

	private static readonly int HelpGroupKey = KeyHash("ModerWarehouse.HelpGroup");

	private static readonly int HelpChildKey = KeyHash("ModerWarehouse.HelpChild");

	private static readonly int HelpWarehouseKey = KeyHash("ModerWarehouse.HelpWarehouse");

	private const int BoardColumns = 4;

	private const int BoardRows = 4;

	private const int BoardCellCount = 16;

	private const int BoardPageSize = 12;

	private const int BoardAnchorCell = 12;

	private const int StatsColumns = 4;

	private const int StatsRows = 4;

	private const int StatsCellCount = 16;

	private const int StatsAnchorCell = 12;

	private static readonly int AutoChestXKey = KeyHash("ModerWarehouse.AutoChestX");

	private static readonly int AutoChestYKey = KeyHash("ModerWarehouse.AutoChestY");

	private static readonly int AutoChestZKey = KeyHash("ModerWarehouse.AutoChestZ");

	private static readonly int AutoLabelItemKey = KeyHash("ModerWarehouse.AutoLabelItem");

	private static readonly int AutoLabelManagedKey = KeyHash("ModerWarehouse.AutoLabelManaged");

	private static readonly int AutoLabelWarehouseKey = KeyHash("ModerWarehouse.AutoLabelWarehouse");

	private static readonly int AutoLabelPriorityKey = KeyHash("ModerWarehouse.AutoLabelPriority");

	private static readonly int InputRadiusKey = KeyHash("ModerWarehouse.InputRadius");

	private static readonly int InputWarehouseKey = KeyHash("ModerWarehouse.InputWarehouse");

	private static readonly int InputCanonicalKey = KeyHash("ModerWarehouse.InputCanonical");

	private readonly HashSet<ServersideQoLZDO> _signs = new HashSet<ServersideQoLZDO>();

	private static readonly Dictionary<string, string> GermanNames = new Dictionary<string, string>();

	private static readonly Dictionary<string, string> GermanDisplayNames = new Dictionary<string, string>();

	private static readonly Dictionary<string, string> EnglishNames = new Dictionary<string, string>();

	private static readonly Dictionary<string, string> EnglishDisplayNames = new Dictionary<string, string>();

	private static readonly Dictionary<string, string> PrefabNames = new Dictionary<string, string>();

	internal static readonly Dictionary<string, HashSet<string>> AliasOwners = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

	private static bool _germanNamesLoaded;

	private SectorDictionary<HashSet<ServersideQoLZDO>> _containers;

	private readonly Dictionary<ServersideQoLZDO, string> _labelItemTokens = new Dictionary<ServersideQoLZDO, string>();

	private readonly Dictionary<ServersideQoLZDO, string> _labelWarehouseIds = new Dictionary<ServersideQoLZDO, string>();

	private readonly Dictionary<ServersideQoLZDO, int> _labelPriorities = new Dictionary<ServersideQoLZDO, int>();

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

	private List<string> _acceptedWarehouseIds = new List<string>();

	private readonly Dictionary<string, Vector3[]> _panelPositions = new Dictionary<string, Vector3[]>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, Dictionary<string, int>> _warehouseStockSnapshots = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, DateTime> _warehouseStockTimes = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

	private readonly HashSet<string> _freshlyReadWarehouses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, string> _globalInputErrors = new Dictionary<string, string>(StringComparer.Ordinal);

	private DateTime _globalInputValidationAt;

	private DateTime _worldStockRefreshAt;

	private readonly Dictionary<string, string> _validatedTopologyParts = new Dictionary<string, string>(StringComparer.Ordinal);

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

	private readonly Dictionary<string, ServersideQoLZDO> _boardAnchors = new Dictionary<string, ServersideQoLZDO>(StringComparer.Ordinal);

	private readonly StringBuilder _cycleDetail = new StringBuilder();

	private Stopwatch _markWatch;

	private double _markLast;

	private readonly Dictionary<string, DateTime> _compactAt = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, Dictionary<string, int>> _noSpace = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, DeliveryNotice> _noSpaceNotices = new Dictionary<string, DeliveryNotice>(StringComparer.OrdinalIgnoreCase);

	private const double StatsWriteSeconds = 60.0;

	private readonly Dictionary<ServersideQoLZDO, StatsCacheEntry> _statsCache = new Dictionary<ServersideQoLZDO, StatsCacheEntry>();

	private readonly Dictionary<string, string> _skipReported = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<List<ServersideQoLZDO>, Dictionary<(ServersideQoLZDO, float), ServersideQoLZDO>> _nearestMemo = new Dictionary<List<ServersideQoLZDO>, Dictionary<(ServersideQoLZDO, float), ServersideQoLZDO>>();

	private readonly Dictionary<string, DateTime> _miscJanitorAt = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, string> _quarantineReported = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<ZDOID, KeyValuePair<uint, PersistedContainer>> _decodedContainers = new Dictionary<ZDOID, KeyValuePair<uint, PersistedContainer>>();

	private readonly Queue<ServersideQoLZDO> _warmQueue = new Queue<ServersideQoLZDO>();

	private readonly HashSet<ServersideQoLZDO> _warmQueued = new HashSet<ServersideQoLZDO>();

	private readonly Dictionary<string, DateTime> _warmDeadline = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, DateTime> _warmArmedAt = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

	private int _warmFrame = -1;

	private double _warmSpentMs;

	private Type _warmStateType;

	private FieldInfo _warmInventoryField;

	private bool _warmReflectionFailed;

	private const int WarmUpThreshold = 48;

	private const double WarmUpBudgetMs = 4.0;

	private const double WarmUpMaxSeconds = 3.0;

	private const double WarmUpRearmSeconds = 60.0;

	private const string LabelSeparator = "\u001f";

	private static readonly Dictionary<string, string[]> AliasCache = new Dictionary<string, string[]>(StringComparer.Ordinal);

	private IEnumerable<ServersideQoLZDO> StorageSigns
	{
		get
		{
			IEnumerable<ServersideQoLZDO> storageSigns = _storageSigns;
			return storageSigns ?? _signs;
		}
	}

	private static int KeyHash(string key)
	{
		return StringExtensionMethods.GetStableHashCode(key);
	}

	protected override void Initialize()
	{
		_signs.Clear();
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
		_validatedTopologyParts.Clear();
		_lastAwake.Clear();
		_lastCompleted.Clear();
		_spatialLookups.Clear();
		_storageSigns = null;
		_localStorage = null;
		_slowCycleLoggedAt.Clear();
		_slowSampleAt.Clear();
		_performanceWindows.Clear();
		_panelPositions.Clear();
		_acceptedWarehouseIds = new List<string>();
		_boardAnchors.Clear();
		_quarantineReported.Clear();
		_noSpace.Clear();
		_noSpaceNotices.Clear();
		_skipReported.Clear();
		_statsCache.Clear();
		_warehouseStockSnapshots.Clear();
		_warehouseStockTimes.Clear();
		_freshlyReadWarehouses.Clear();
		_globalInputErrors.Clear();
		_globalInputValidationAt = DateTime.MinValue;
		_worldStockRefreshAt = DateTime.MinValue;
		_requestSearchNotices.Clear();
		_inventoryIndex.Clear();
		_warmQueue.Clear();
		_warmQueued.Clear();
		_warmDeadline.Clear();
		_warmArmedAt.Clear();
		_decodedContainers.Clear();
		InitializePerformanceLog();
		InitializeStockCache();
		EnsureWorldIndex();
		ContainerRegistryProcessor val = Processor.Instance<ContainerRegistryProcessor>();
		_containers = val.GetContainers(100f);
		val.ContainerChanged -= OnContainerChanged;
		val.ContainerChanged += OnContainerChanged;
		ZNet.WorldSaveStarted = (Action)Delegate.Remove(ZNet.WorldSaveStarted, new Action(OnWorldSaveStarted));
		ZNet.WorldSaveStarted = (Action)Delegate.Combine(ZNet.WorldSaveStarted, new Action(OnWorldSaveStarted));
		WarehouseLocalization.Initialize(delegate(string message)
		{
			this.Logger.LogInfo((object)message);
		});
		LoadGermanNames();
		this.Logger.LogInfo((object)(WarehousePlugin.Name + " " + WarehousePlugin.Version + ": localization, configurable capacity alerts, player-safe input ownership, solo-friendly visit deltas, target stock shopping list, delivery confirmation, automatic IN help sign, interactive rotating 4x4 stock board, rolling 4x4 statistics board, persistent linked automatic signs, safe transfers and multi-item withdrawal initialized; " + $"{GermanNames.Count} EN/DE item names loaded; based on Moder Warehouse 2.0.3, build " + WarehousePlugin.BuildId + "."));
		this.Logger.LogInfo((object)("Performance revision " + WarehousePlugin.BuildId + ": memoized sign/label text, per-type item aliases, warehouse-scoped change events, cached panel positions, throttled statistics writes, world index for read-only stock queries, inventory warm-up."));
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
		if (_knownNodes.Count > 0)
		{
			WarehouseNode[] array = _knownNodes.Values.ToArray();
			foreach (WarehouseNode warehouseNode in array)
			{
				if ((changed == null || !(Distance2(changed, warehouseNode.Input) > warehouseNode.Radius * warehouseNode.Radius)) && _warehousesDirty.Add(warehouseNode.Id))
				{
					this.ScheduleReprocessing(warehouseNode.Sign, 0.5f);
				}
			}
			return;
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (ServersideQoLZDO sign in _signs)
		{
			string text = WarehouseId(sign);
			if (!string.IsNullOrWhiteSpace(text) && hashSet.Add(text) && _warehousesDirty.Add(text))
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
			File.WriteAllText(_performanceLogPath, WarehousePlugin.Name + " " + WarehousePlugin.Version + " performance log\r\n" + $"Started UTC: {WarehouseClock.UtcNow:O}\r\n" + "Revision: " + WarehousePlugin.BuildId + "; SUMMARY phase times are independent maxima; SLOW sampled at most once per 10s; skipped means throttled callbacks, not failed work.\r\nUTC | kind | warehouse | state | cycles | skipped | avg_ms | max_ms | topology_ms | classify_ms | display_ms | transfer_ms | containers | signs | stores | inventory_reads | peers | ping_avg_ms | ping_max_ms | local_quality_min | remote_quality_min | out_Bps | in_Bps\r\n", Encoding.UTF8);
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
		int[] array = new int[4] { RequestWarehouseKey, InfoWarehouseKey, BoardWarehouseKey, StatsWarehouseKey };
		foreach (int num in array)
		{
			string text = sign.ZDO.GetString(num, string.Empty);
			if (!string.IsNullOrWhiteSpace(text))
			{
				return text.Trim();
			}
		}
		WarehouseText.MatchCache marker = Marker;
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
		zdo.Fields<Piece>().Set((Func<Expression<Func<Piece, bool>>>)(() => (Piece x) => x.m_canBeRemoved), true);
		ServersideQoLZDO.ZDOVars vars = zdo.Vars;
		string text = PlainText(vars.GetText(""));
		if (CleanMarker.IsMatch(text))
		{
			List<ServersideQoLZDO> list = _signs.Where((ServersideQoLZDO other) => other != zdo && PlacedByWarehouse(other) && Distance2(other, zdo) < 0.0025f).ToList();
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
			string boardGroup = zdo.ZDO.GetString(BoardGroupKey, string.Empty);
			if (!string.IsNullOrWhiteSpace(boardGroup))
			{
				foreach (ServersideQoLZDO item2 in _signs.Where((ServersideQoLZDO x) => x.ZDO.GetString(BoardGroupKey, string.Empty) == boardGroup).ToList())
				{
					_signs.Remove(item2);
					item2.Destroy();
				}
				this.Logger.LogInfo((object)("Deleted warehouse board group '" + boardGroup + "'."));
				return (ProcessResult)4;
			}
			string statsGroup = zdo.ZDO.GetString(StatsGroupKey, string.Empty);
			if (!string.IsNullOrWhiteSpace(statsGroup))
			{
				foreach (ServersideQoLZDO item3 in _signs.Where((ServersideQoLZDO x) => x.ZDO.GetString(StatsGroupKey, string.Empty) == statsGroup).ToList())
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
		string text2 = zdo.ZDO.GetString(RequestWarehouseKey, string.Empty);
		string text3 = zdo.ZDO.GetString(InfoWarehouseKey, string.Empty);
		string text4 = zdo.ZDO.GetString(BoardWarehouseKey, string.Empty);
		string text5 = zdo.ZDO.GetString(StatsWarehouseKey, string.Empty);
		if (match.Success && match.Groups["role"].Value.Equals("REQ", StringComparison.OrdinalIgnoreCase))
		{
			text2 = match.Groups["id"].Value;
			zdo.ZDO.Set(RequestWarehouseKey, text2);
			SaveChestSignLinks(zdo);
		}
		if (match.Success && match.Groups["role"].Value.Equals("INFO", StringComparison.OrdinalIgnoreCase))
		{
			text3 = match.Groups["id"].Value;
			zdo.ZDO.Set(InfoWarehouseKey, text3);
			SaveChestSignLinks(zdo);
		}
		if (match.Success && match.Groups["role"].Value.Equals("BOARD", StringComparison.OrdinalIgnoreCase))
		{
			text4 = match.Groups["id"].Value;
			zdo.ZDO.Set(BoardWarehouseKey, text4);
			if (string.IsNullOrWhiteSpace(zdo.ZDO.GetString(BoardGroupKey, string.Empty)))
			{
				zdo.ZDO.Set(BoardGroupKey, Guid.NewGuid().ToString("N"));
			}
			zdo.ZDO.Set(BoardCellKey, 12, false);
			EnsureBoard(zdo, text4);
		}
		if (match.Success && match.Groups["role"].Value.Equals("STATS", StringComparison.OrdinalIgnoreCase))
		{
			text5 = match.Groups["id"].Value;
			zdo.ZDO.Set(StatsWarehouseKey, text5);
			if (string.IsNullOrWhiteSpace(zdo.ZDO.GetString(StatsGroupKey, string.Empty)))
			{
				zdo.ZDO.Set(StatsGroupKey, Guid.NewGuid().ToString("N"));
			}
			zdo.ZDO.Set(StatsCellKey, 12, false);
			EnsureStatsBoard(zdo, text5);
		}
		if (match.Success && match.Groups["role"].Value.Equals("IN", StringComparison.OrdinalIgnoreCase))
		{
			string text6 = match.Groups["id"].Value.Trim();
			float radius = ParseInputRadius(match.Groups["payload"].Value);
			WarehouseRenamePolicy.TryResolvePreviousId(zdo.ZDO.GetString(InputWarehouseKey, string.Empty), text6, out var previousId);
			bool flag = HasOtherInputWithId(zdo, text6);
			if (WarehouseRenamePolicy.CanApplyRename(previousId, text6, flag))
			{
				MigrateWarehouseSigns(zdo, previousId, text6, radius);
				zdo.ZDO.Set(InputWarehouseKey, text6);
			}
			else if (string.IsNullOrWhiteSpace(previousId) && !flag)
			{
				zdo.ZDO.Set(InputWarehouseKey, text6);
			}
			else if (WarehouseRenamePolicy.ShouldLogRenameConflict(previousId, flag))
			{
				this.Logger.LogWarning((object)("Warehouse rename to '" + text6 + "' was not applied: another IN marker already uses that name."));
			}
			zdo.ZDO.Set(InputRadiusKey, ParseInputRadius(match.Groups["payload"].Value));
			EnsureHelpSign(zdo, text6, ResolveLanguage(match.Groups["payload"].Value));
		}
		if (!string.IsNullOrWhiteSpace(text2))
		{
			string text7 = (match.Success ? match.Groups["payload"].Value.Trim() : text.Trim());
			string text8 = zdo.ZDO.GetString(RequestRenderedTextKey, string.Empty);
			if (string.IsNullOrWhiteSpace(text7))
			{
				_requestClearAt.Remove(zdo);
				WriteRequestPrompt(zdo, LanguageForWarehouse(text2));
				return this.ScheduleReprocessing(Mathf.Max(1f, ConfigBase<WarehouseConfig>.Instance.ScanInterval.Value));
			}
			if (RequestPolicy.TryStripKeyword(text7, WarehouseLocalization.TargetKeywords(), out var rest))
			{
				if (TryParseTargets(rest, out var targets))
				{
					zdo.ZDO.Set(RequestTargetsKey, SerializeTargets(targets));
					SaveChestSignLinks(zdo);
					this.Logger.LogInfo((object)$"Warehouse '{text2}': saved {targets.Count} target stock value(s).");
					ProcessWarehouse(text2, peers, force: true);
				}
				else
				{
					string language = LanguageForWarehouse(text2);
					WriteRequestText(zdo, "<color=#ff6666>" + WarehouseLocalization.Text(language, "request.error_example") + "\n<color=white>" + WarehouseLocalization.Text(language, "request.target") + " " + WarehouseLocalization.Text(language, "request.example_item") + " 1000");
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
					WriteRequestPrompt(zdo, LanguageForWarehouse(text2));
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
					WriteRequestPrompt(zdo, LanguageForWarehouse(text2));
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
			string group = zdo.ZDO.GetString(BoardGroupKey, string.Empty);
			int num2 = zdo.ZDO.GetInt(BoardCellKey, -1);
			CaptureBoardControlInput(zdo, group, num2);
			if (!string.IsNullOrWhiteSpace(group))
			{
				DateTime value3;
				if (HasBoardAnchor(group))
				{
					_orphanBoardSeenAt.Remove(group);
				}
				else if (!_orphanBoardSeenAt.TryGetValue(group, out value3))
				{
					_orphanBoardSeenAt[group] = WarehouseClock.UtcNow;
				}
				else if ((WarehouseClock.UtcNow - value3).TotalSeconds >= 10.0)
				{
					List<ServersideQoLZDO> list2 = _signs.Where((ServersideQoLZDO sign) => sign.ZDO.GetString(BoardGroupKey, string.Empty) == group).ToList();
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
			int num3 = zdo.ZDO.GetInt(StatsCellKey, -1);
			if (num3 == 12)
			{
				EnsureStatsBoard(zdo, text5);
			}
			if (num3 == 12 || zdo.ZDO.GetBool(StatsControlKey, false))
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

	private bool HasBoardAnchor(string group)
	{
		if (_boardAnchors.TryGetValue(group, out var value) && _signs.Contains(value) && value.ZDO.GetString(BoardGroupKey, string.Empty) == group && value.ZDO.GetInt(BoardCellKey, -1) == 12)
		{
			return true;
		}
		ServersideQoLZDO val = ((IEnumerable<ServersideQoLZDO>)_signs).FirstOrDefault((Func<ServersideQoLZDO, bool>)((ServersideQoLZDO sign) => sign.ZDO.GetString(BoardGroupKey, string.Empty) == group && sign.ZDO.GetInt(BoardCellKey, -1) == 12));
		if (val != null)
		{
			_boardAnchors[group] = val;
		}
		else
		{
			_boardAnchors.Remove(group);
		}
		return val != null;
	}

	private string LanguageForWarehouse(string id)
	{
		foreach (ServersideQoLZDO sign in _signs)
		{
			WarehouseText.MatchCache marker = Marker;
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
		WarmInventories();
		string text = CanonicalWarehouseId(id.Trim());
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
		CapturePeerMetrics();
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
			_nearestMemo.Clear();
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
		if (_panelPositions.TryGetValue(id, out var value))
		{
			foreach (Peer peer in peers)
			{
				if (peer == null)
				{
					continue;
				}
				Vector3[] array = value;
				foreach (Vector3 val in array)
				{
					Vector3 val2 = peer.RefPos - val;
					if (val2.sqrMagnitude <= distance2)
					{
						return true;
					}
				}
			}
			return false;
		}
		return _signs.Any(delegate(ServersideQoLZDO sign)
		{
			string text = sign.ZDO.GetString(BoardWarehouseKey, string.Empty);
			string text2 = sign.ZDO.GetString(StatsWarehouseKey, string.Empty);
			return (text.Equals(id, StringComparison.OrdinalIgnoreCase) || text2.Equals(id, StringComparison.OrdinalIgnoreCase)) && peers.Any(delegate(Peer peer)
			{
				if (peer != null)
				{
					Vector3 val3 = peer.RefPos - sign.ZDO.GetPosition();
					return val3.sqrMagnitude <= distance2;
				}
				return false;
			});
		});
	}

	private void MarkStart()
	{
		_cycleDetail.Clear();
		_markWatch = Stopwatch.StartNew();
		_markLast = 0.0;
	}

	private void Mark(string name)
	{
		if (_markWatch != null)
		{
			double totalMilliseconds = _markWatch.Elapsed.TotalMilliseconds;
			_cycleDetail.Append(name).Append('=').Append((totalMilliseconds - _markLast).ToString("0.0", CultureInfo.InvariantCulture))
				.Append(' ');
			_markLast = totalMilliseconds;
		}
	}

	private void ProcessWarehouseCore(string id, IReadOnlyList<Peer> peers)
	{
		_cycleDetail.Clear();
		Stopwatch stopwatch = Stopwatch.StartNew();
		if (!_germanNamesLoaded)
		{
			LoadGermanNames();
		}
		string language = LanguageForWarehouse(id);
		List<ServersideQoLZDO> list = AllContainers();
		_cycleContainers = list.Count;
		_cycleSigns = _signs.Count;
		float num = ConfigBase<WarehouseConfig>.Instance.MarkerDistance.Value * ConfigBase<WarehouseConfig>.Instance.MarkerDistance.Value;
		MarkStart();
		List<WarehouseNode> list2 = DiscoverWarehouseNodes(list, num);
		Mark("nodes");
		ValidateWarehouseNodes(list2, num);
		Mark("validate");
		_knownNodes.Clear();
		foreach (WarehouseNode item2 in list2.Where((WarehouseNode x) => x.Valid))
		{
			_knownNodes[item2.Id] = item2;
		}
		UpdateInputHelpSigns(list2);
		UpdateUnpairedInputSigns(list2);
		_cycleTopologyMs = stopwatch.ElapsedMilliseconds;
		Mark("helpSigns");
		WarehouseNode localNode = list2.FirstOrDefault((WarehouseNode x) => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
		if (localNode == null || !localNode.Valid)
		{
			ReportSkippedCycle(id, (localNode == null) ? ("no IN marker with a reachable input chest (network: " + string.Join(", ", list2.Select((WarehouseNode n) => n.Id + (n.Valid ? "" : "!"))) + ")") : ("warehouse locked (" + localNode.Error + ")"), (localNode == null) ? "board.no_input" : "board.locked", language);
			return;
		}
		_localStorage = new HashSet<ServersideQoLZDO>(list.Where((ServersideQoLZDO x) => Distance2(x, localNode.Input) <= localNode.Radius * localNode.Radius));
		float signRadius = localNode.Radius + Math.Max(ConfigBase<WarehouseConfig>.Instance.LabelDistance.Value, ConfigBase<WarehouseConfig>.Instance.MarkerDistance.Value) + 2f;
		_storageSigns = _signs.Where((ServersideQoLZDO sign) => Distance2(sign, localNode.Input) <= signRadius * signRadius || LinkedInside(sign, localNode)).ToList();
		Mark("localSets");
		HashSet<ServersideQoLZDO> endpoints = new HashSet<ServersideQoLZDO>();
		ServersideQoLZDO input = localNode.Input;
		ServersideQoLZDO outputZdo = null;
		List<Request> list3 = new List<Request>();
		List<ServersideQoLZDO> list4 = new List<ServersideQoLZDO>();
		List<ServersideQoLZDO> list5 = new List<ServersideQoLZDO>();
		List<ServersideQoLZDO> list6 = new List<ServersideQoLZDO>();
		List<FillOrder> list7 = new List<FillOrder>();
		float num2 = ConfigBase<WarehouseConfig>.Instance.LabelDistance.Value * ConfigBase<WarehouseConfig>.Instance.LabelDistance.Value;
		ServersideQoLZDO.ZDOVars vars;
		foreach (ServersideQoLZDO sign in _signs)
		{
			WarehouseText.MatchCache marker = Marker;
			vars = sign.Vars;
			Match match = marker.Match(PlainText(vars.GetText("")));
			string text = sign.ZDO.GetString(RequestWarehouseKey, string.Empty);
			string text2 = sign.ZDO.GetString(InfoWarehouseKey, string.Empty);
			string text3 = sign.ZDO.GetString(BoardWarehouseKey, string.Empty);
			string text4 = sign.ZDO.GetString(StatsWarehouseKey, string.Empty);
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
			ServersideQoLZDO val = NearestContainer(sign, list, num);
			if (val != null)
			{
				endpoints.Add(val);
				if (text5 == "IN" && val == localNode.Input)
				{
					input = val;
				}
				if (text5 == "OUT")
				{
					outputZdo = val;
				}
				if (text5 == "FILL")
				{
					list7.Add(new FillOrder(sign, val, match.Groups["payload"].Value));
				}
			}
		}
		_panelPositions[id] = (from sign in list5.Concat(list6)
			select sign.ZDO.GetPosition()).ToArray();
		Mark("signLoop");
		if (input == null)
		{
			ReportSkippedCycle(id, "input chest not resolved", "board.no_input", language);
			return;
		}
		ContainerRegistryProcessor registry = Processor.Instance<ContainerRegistryProcessor>();
		ContainerState state = registry.GetState(input);
		if (state == null)
		{
			ReportSkippedCycle(id, "input chest has no container state", "board.input_unavailable", language);
			return;
		}
		IInventory val2 = InventoryFor(registry, input);
		if (val2 == null)
		{
			ReportSkippedCycle(id, "input chest inventory unavailable", "board.input_unavailable", language);
			return;
		}
		ReportSkippedCycle(id, null, null, language);
		float radius2 = localNode.Radius * localNode.Radius;
		Mark("source");
		HashSet<ServersideQoLZDO> ignored = DiscoverIgnored(list, num);
		Mark("ignored");
		RefreshAutomaticLabels(list, num2, registry, language, id);
		Mark("refreshLabels");
		Dictionary<ServersideQoLZDO, string> labels = DiscoverLabels(list, num2, null, rememberItemTokens: true, id);
		Mark("discoverLabels");
		Dictionary<ServersideQoLZDO, ServersideQoLZDO> dictionary = DiscoverBlankSigns(list, num2, null, id);
		Mark("blankSigns");
		HashSet<ServersideQoLZDO> optedIn = new HashSet<ServersideQoLZDO>(labels.Keys);
		optedIn.UnionWith(dictionary.Keys);
		if (DeferForInventoryWarmUp(id, list.Where((ServersideQoLZDO x) => optedIn.Contains(x) && Distance2(x, input) <= radius2)))
		{
			return;
		}
		List<ServersideQoLZDO> quarantined = new List<ServersideQoLZDO>();
		List<ServersideQoLZDO> list8 = (from x in (from x in list
				where optedIn.Contains(x) && WarehouseContainerPolicy.CanManage(endpoints.Contains(x), ignored.Contains(x), IsPrivateContainer(x), inUse: false, hasStorageSign: true)
				where Distance2(x, input) <= radius2
				select x).Where(delegate(ServersideQoLZDO x)
			{
				if (ContainsOnlyWarehouseItems(InventoryFor(registry, x)))
				{
					return true;
				}
				if (InventoryFor(registry, x) != null)
				{
					quarantined.Add(x);
				}
				return false;
			})
			orderby Distance2(x, input)
			select x).ToList();
		List<ServersideQoLZDO> list9 = list8.Where(delegate(ServersideQoLZDO x)
		{
			ServersideQoLZDO.ZDOVars vars2 = x.Vars;
			return !vars2.GetInUse(false);
		}).ToList();
		ReportQuarantinedStorage(id, quarantined, registry);
		Mark("storesFilter");
		_cycleStores = list9.Count;
		int moves = Math.Max(1, ConfigBase<WarehouseConfig>.Instance.MaxMoves.Value);
		int num3 = CorrectMisfiledItems(id, list9, labels, registry, ref moves);
		if (num3 > 0)
		{
			this.Logger.LogInfo((object)$"Warehouse '{id}' janitor refiled {num3} valid misplaced item(s) in total.");
		}
		Mark("janitor");
		ServersideQoLZDO val3 = ((IEnumerable<ServersideQoLZDO>)list6).FirstOrDefault((Func<ServersideQoLZDO, bool>)((ServersideQoLZDO sign) => sign.ZDO.GetInt(StatsCellKey, -1) == 12));
		StatsCacheEntry statsCacheEntry = ((val3 == null) ? null : TakeStatsHistory(val3));
		WarehouseStatsHistory warehouseStatsHistory = statsCacheEntry?.History;
		if (warehouseStatsHistory != null)
		{
			SnapshotStatistics(warehouseStatsHistory, list8, registry);
		}
		Mark("statsLoad");
		Dictionary<string, int> dictionary2 = (from val5 in list8.SelectMany(delegate(ServersideQoLZDO store)
			{
				IInventory obj = InventoryFor(registry, store);
				IEnumerable<ItemData> enumerable = ((obj != null) ? obj.Items : null);
				return enumerable ?? Enumerable.Empty<ItemData>();
			})
			where val5 != null && val5.m_stack > 0 && val5.m_shared != null
			group val5 by val5.m_shared.m_name).ToDictionary<IGrouping<string, ItemData>, string, int>((IGrouping<string, ItemData> group) => group.Key, (IGrouping<string, ItemData> group) => group.Sum((ItemData val5) => val5.m_stack), StringComparer.Ordinal);
		if (!_warehouseStockSnapshots.TryGetValue(id, out var value) || !StockEqual(value, dictionary2))
		{
			_warehouseStockSnapshots[id] = dictionary2;
			_warehouseStockTimes[id] = WarehouseClock.UtcNow;
			SaveStockCache();
		}
		Mark("stockSnapshot");
		_cycleClassifyMs = stopwatch.ElapsedMilliseconds - _cycleTopologyMs;
		WhitenWarehouseLabels(list8.Concat(endpoints).Concat(ignored).Distinct()
			.ToList(), list, num2);
		Mark("whiten");
		LabelHomogeneousStores(id, list9, labels, dictionary, registry, language);
		AdoptContainerSigns(list9, list, num2);
		Mark("autoLabel");
		WriteWarehouseInfo(id, language, list4, list8, labels, registry);
		WriteShoppingLists(language, list3.Select((Request x) => x.Sign), list8, registry);
		Mark("infoShopping");
		WriteWarehouseBoard(id, language, list5, list8, registry, peers, list2, list, num);
		Mark("board");
		_cycleDisplayMs = stopwatch.ElapsedMilliseconds - _cycleTopologyMs - _cycleClassifyMs;
		int num4 = 0;
		HashSet<ServersideQoLZDO> hashSet = new HashSet<ServersideQoLZDO>();
		Dictionary<string, int> dictionary3 = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);
		bool flag5 = val2.Items.Any((ItemData val5) => IsWarehouseItem(val5) && val5.m_stack > 0);
		vars = input.Vars;
		bool inUse = vars.GetInUse(false);
		if (inUse && TryReleaseStaleInputLock(id, input, state, peers))
		{
			inUse = false;
		}
		else if (!inUse)
		{
			_inputInUseSince.Remove(input.ZDO.m_uid);
		}
		bool flag6 = state.ZDO.IsOwnerOrUnassigned();
		bool flag7 = flag5 && !inUse && Own(state);
		int num5 = 0;
		int num6 = 0;
		int num7 = 0;
		int num8 = 0;
		List<ServersideQoLZDO> second = list9.Where((ServersideQoLZDO x) => IsMiscChest(x, labels)).ToList();
		Dictionary<string, int> dictionary4 = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);
		bool flag8 = false;
		if (flag5 && !inUse && flag7)
		{
			flag8 = true;
			foreach (ItemData item in val2.Items.ToList())
			{
				if (moves <= 0 || item == null || item.m_stack <= 0)
				{
					break;
				}
				if (!IsWarehouseItem(item))
				{
					continue;
				}
				int num9 = item.m_stack;
				string value5;
				foreach (ServersideQoLZDO item3 in (from x in list9
					where (!labels.TryGetValue(x, out value5)) ? IsSingleItemType(InventoryFor(registry, x), item) : LabelMatches(x, value5, item, registry)
					orderby FillPriority(x, id, item.m_shared.m_name), (!HasItem(InventoryFor(registry, x), item)) ? ((labels.TryGetValue(x, out value5) && LabelMatches(x, value5, item, registry)) ? 1 : 0) : 2 descending
					select x).Concat(second))
				{
					if (moves <= 0 || num9 <= 0)
					{
						break;
					}
					num5++;
					ContainerState state2 = registry.GetState(item3);
					if (state2 == null)
					{
						num6++;
						continue;
					}
					if (!Own(state2))
					{
						num7++;
						continue;
					}
					IInventory val4 = InventoryFor(registry, item3);
					bool flag9 = !labels.ContainsKey(item3) && IsSingleItemType(val4, item);
					int num10 = Move(val2, val4, item, num9);
					if (num10 <= 0)
					{
						num8++;
					}
					if (num10 <= 0)
					{
						continue;
					}
					string key = LocalizedItemName(item, language);
					dictionary3[key] = (dictionary3.TryGetValue(key, out var value2) ? (value2 + num10) : num10);
					num4 += num10;
					warehouseStatsHistory?.AddInput(item.m_shared.m_name, num10);
					num9 -= num10;
					moves--;
					hashSet.Add(item3);
					if (flag9 && ConfigBase<WarehouseConfig>.Instance.AutoCreateLabels.Value)
					{
						string value3 = CreateAutomaticLabel(id, item3, item, dictionary, language);
						if (!string.IsNullOrEmpty(value3))
						{
							labels[item3] = value3;
						}
					}
				}
				if (num9 > 0 && moves > 0)
				{
					string key2 = LocalizedItemName(item, language);
					dictionary4[key2] = (dictionary4.TryGetValue(key2, out var value4) ? (value4 + num9) : num9);
				}
			}
		}
		if (flag8 || !flag5)
		{
			UpdateNoSpace(id, dictionary4);
		}
		if (val2.Items.Any((ItemData val5) => val5 != null && val5.m_stack > 0) && num4 == 0 && ShouldLogTransferDiagnostic(id))
		{
			string text7 = string.Join(", ", from val5 in val2.Items
				where val5 != null && val5.m_stack > 0
				select $"{ItemName(val5)} x{val5.m_stack}" + string.Format(" [shared='{0}', cheated={1}, ", val5.m_shared?.m_name ?? "<null>", val5.m_cheated) + "type='" + (val5.m_shared?.m_itemType.ToString() ?? "<null>") + "']");
			int num11 = val2.Items.Count((ItemData val5) => val5 != null && val5.m_stack > 0 && !IsWarehouseItem(val5));
			this.Logger.LogWarning((object)("Warehouse '" + id + "': transfer diagnostic; input=[" + text7 + "], " + $"warehouseItems={flag5}, rejectedItemStacks={num11}, " + $"INInUse={inUse}, sourceOwnerBefore={flag6}, " + $"sourceOwnershipGranted={flag7}, stores={list9.Count}, " + $"matchingTargets={num5}, missingTargetStates={num6}, " + $"targetOwnershipBlocked={num7}, moveReturnedZero={num8}."));
		}
		if (num4 > 0)
		{
			_deliveryNotices[id] = new DeliveryNotice(dictionary3, WarehouseClock.UtcNow.AddSeconds(Math.Max(2f, ConfigBase<WarehouseConfig>.Instance.DeliveryNoticeSeconds.Value)));
			this.Logger.LogInfo((object)($"Warehouse '{id}': stored {num4} item(s) into " + $"{hashSet.Count} chest(s); {list9.Count} candidate chest(s) scanned."));
		}
		foreach (Request item4 in list3)
		{
			ProcessRequest(id, language, item4, outputZdo, list8, registry, warehouseStatsHistory, ref moves, num);
		}
		RefillChests(id, language, list7, input, localNode.Radius, list9, registry, warehouseStatsHistory, ref moves);
		if (!val2.Items.Any((ItemData val5) => val5 != null && val5.m_stack > 0 && IsWarehouseItem(val5)))
		{
			CompactStorage(id, list9, labels, registry, list, num2, ref moves);
		}
		if (warehouseStatsHistory != null)
		{
			SnapshotStatistics(warehouseStatsHistory, list8, registry);
			StoreStatsHistory(val3, statsCacheEntry);
			WriteStatisticsBoard(id, language, list6, warehouseStatsHistory, list8, registry, peers, list2, list, num);
		}
		_cycleTransferMs = stopwatch.ElapsedMilliseconds - _cycleTopologyMs - _cycleClassifyMs - _cycleDisplayMs;
	}

	private bool TryReleaseStaleInputLock(string id, ServersideQoLZDO input, ContainerState state, IReadOnlyList<Peer> peers)
	{
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
		vars.SetInUse(false);
		_inputInUseSince.Remove(uid);
		string arg = ((val == null) ? "its owner is not connected" : (flag ? $"hard limit {num3:0} s reached" : $"its owner is more than {num:0.#} m away"));
		this.Logger.LogWarning((object)$"Warehouse '{id}': released stale IN chest lock after {totalSeconds:0} s; {arg}.");
		return true;
	}

	private void RefillChests(string id, string language, List<FillOrder> fills, ServersideQoLZDO input, float radius, List<ServersideQoLZDO> stores, ContainerRegistryProcessor registry, WarehouseStatsHistory stats, ref int moves)
	{
		foreach (FillOrder fill in fills)
		{
			if (moves <= 0)
			{
				break;
			}
			if (Distance2(fill.Chest, input) > radius * radius)
			{
				continue;
			}
			ServersideQoLZDO.ZDOVars vars = fill.Chest.Vars;
			if (vars.GetInUse(false) || !TryParseTargets(PlainText(fill.Payload), out var targets))
			{
				continue;
			}
			ContainerState state = registry.GetState(fill.Chest);
			IInventory val = InventoryFor(registry, fill.Chest);
			if (state == null || val == null)
			{
				continue;
			}
			List<ItemData> items = (from item in stores.SelectMany(delegate(ServersideQoLZDO store)
				{
					IInventory obj = InventoryFor(registry, store);
					IEnumerable<ItemData> enumerable = ((obj != null) ? obj.Items : null);
					return enumerable ?? Enumerable.Empty<ItemData>();
				})
				where item != null && item.m_stack > 0 && item.m_shared != null
				select item).ToList();
			foreach (KeyValuePair<string, int> item in targets)
			{
				if (moves <= 0)
				{
					return;
				}
				string token = ChooseRequestToken(items, item.Key) ?? ChooseRequestToken(val.Items, item.Key);
				if (token == null)
				{
					continue;
				}
				int num = val.Items.Where((ItemData item) => item != null && item.m_shared?.m_name == token).Sum((ItemData item) => item.m_stack);
				int num2 = item.Value - num;
				if (num2 <= 0)
				{
					continue;
				}
				if (!Own(state))
				{
					break;
				}
				int num3 = 0;
				foreach (ServersideQoLZDO store in stores)
				{
					if (moves <= 0 || num2 <= 0)
					{
						break;
					}
					vars = store.Vars;
					if (vars.GetInUse(false))
					{
						continue;
					}
					ContainerState state2 = registry.GetState(store);
					IInventory val2 = InventoryFor(registry, store);
					if (state2 == null || val2 == null)
					{
						continue;
					}
					List<ItemData> list = val2.Items.Where((ItemData item) => item != null && item.m_stack > 0 && item.m_shared?.m_name == token).ToList();
					if (list.Count == 0 || !Own(state2))
					{
						continue;
					}
					foreach (ItemData item2 in list)
					{
						if (moves <= 0 || num2 <= 0)
						{
							break;
						}
						int num4 = Move(val2, val, item2, num2);
						if (num4 > 0)
						{
							num3 += num4;
							num2 -= num4;
							moves--;
							stats?.AddOutput(token, num4);
						}
					}
				}
				if (num3 > 0)
				{
					this.Logger.LogInfo((object)($"Warehouse '{id}': refilled {num3} x '{LocalizedTokenName(token, language)}' " + $"into FILL chest ({item.Value} wanted)."));
				}
			}
		}
	}

	private void CompactStorage(string id, List<ServersideQoLZDO> stores, Dictionary<ServersideQoLZDO, string> labels, ContainerRegistryProcessor registry, List<ServersideQoLZDO> containers, float labelRange2, ref int moves)
	{
		DateTime utcNow = WarehouseClock.UtcNow;
		if (moves <= 0 || (_compactAt.TryGetValue(id, out var value) && utcNow < value))
		{
			return;
		}
		_compactAt[id] = utcNow.AddSeconds(60.0);
		Dictionary<string, List<(ServersideQoLZDO Chest, int Amount, ItemData Sample)>> dictionary = new Dictionary<string, List<(ServersideQoLZDO Chest, int Amount, ItemData Sample)>>(StringComparer.Ordinal);
		foreach (ServersideQoLZDO store in stores)
		{
			IInventory obj = InventoryFor(registry, store);
			IEnumerable<ItemData> enumerable = ((obj != null) ? obj.Items : null);
			List<ItemData> list = (enumerable ?? Enumerable.Empty<ItemData>()).Where((ItemData item) => item != null && item.m_stack > 0 && item.m_shared != null).ToList();
			if (list.Count == 0)
			{
				continue;
			}
			string token = list[0].m_shared.m_name;
			if (list.Any((ItemData item) => item.m_shared.m_name != token))
			{
				continue;
			}
			bool num;
			if (!_labelItemTokens.TryGetValue(store, out var value2))
			{
				if (!labels.TryGetValue(store, out var value3))
				{
					goto IL_0178;
				}
				if (TryLabelCategory(value3, out var _))
				{
					continue;
				}
				num = LabelMatches(store, value3, list[0], registry);
			}
			else
			{
				num = value2 == token;
			}
			if (!num)
			{
				continue;
			}
			goto IL_0178;
			IL_0178:
			if (!dictionary.TryGetValue(token, out var value4))
			{
				value4 = (dictionary[token] = new List<(ServersideQoLZDO Chest, int Amount, ItemData Sample)>());
			}
			value4.Add((store, list.Sum((ItemData item) => item.m_stack), list[0]));
		}
		foreach (KeyValuePair<string, List<(ServersideQoLZDO Chest, int Amount, ItemData Sample)>> pair in dictionary)
		{
			if (pair.Value.Count < 2)
			{
				continue;
			}
			(ServersideQoLZDO Chest, int Amount, ItemData Sample) source = (from x in pair.Value
				orderby (FillPriority(x.Chest, id, pair.Key) == int.MaxValue) ? 1 : FillPriority(x.Chest, id, pair.Key) descending, x.Amount
				select x).First();
			List<(ServersideQoLZDO Chest, int Amount, ItemData Sample)> list3 = (from x in pair.Value
				where x.Chest != source.Chest
				orderby FillPriority(x.Chest, id, pair.Key), x.Amount descending
				select x).ToList();
			if (list3.Sum<(ServersideQoLZDO Chest, int Amount, ItemData Sample)>(((ServersideQoLZDO Chest, int Amount, ItemData Sample) x) => FreeCapacity(InventoryFor(registry, x.Chest), source.Sample)) < source.Amount)
			{
				continue;
			}
			ContainerState state = registry.GetState(source.Chest);
			IInventory val = InventoryFor(registry, source.Chest);
			if (state == null || val == null || !Own(state))
			{
				continue;
			}
			int num2 = 0;
			foreach (var item in list3)
			{
				ContainerState state2 = registry.GetState(item.Item1);
				if (state2 == null || !Own(state2))
				{
					continue;
				}
				IInventory destination = InventoryFor(registry, item.Item1);
				foreach (ItemData item2 in val.Items.Where((ItemData x) => x != null && x.m_stack > 0).ToList())
				{
					if (moves <= 0)
					{
						break;
					}
					int num3 = Move(val, destination, item2, item2.m_stack);
					if (num3 > 0)
					{
						num2 += num3;
						moves--;
					}
				}
			}
			if (num2 != 0)
			{
				bool flag = !val.Items.Any((ItemData x) => x != null && x.m_stack > 0);
				string text = LocalizedTokenName(pair.Key, LanguageForWarehouse(id));
				this.Logger.LogInfo((object)($"Warehouse '{id}': compacted {num2} x '{text}' into {list3.Count} chest(s)" + (flag ? "; one chest is free again." : ".")));
				if (flag)
				{
					ReleaseAutomaticLabel(source.Chest, pair.Key, containers, labelRange2);
				}
				break;
			}
		}
	}

	private static int FreeCapacity(IInventory inventory, ItemData item)
	{
		if (inventory == null || item?.m_shared == null)
		{
			return 0;
		}
		int max = Math.Max(1, item.m_shared.m_maxStackSize);
		int num = inventory.Items.Where((ItemData x) => CanStack(x, item)).Sum((ItemData x) => Math.Max(0, max - x.m_stack));
		int val = inventory.Inventory.GetWidth() * inventory.Inventory.GetHeight() - inventory.Items.Count;
		return num + Math.Max(0, val) * max;
	}

	private void ReleaseAutomaticLabel(ServersideQoLZDO chest, string token, List<ServersideQoLZDO> containers, float labelRange2)
	{
		foreach (ServersideQoLZDO item in StorageSigns.Where((ServersideQoLZDO sign) => sign.ZDO.GetBool(AutoLabelManagedKey, false) && sign.ZDO.GetString(AutoLabelItemKey, string.Empty) == token && sign.ZDO.GetInt(AutoLabelPriorityKey, 0) == 0 && LinkedOrNearestContainer(sign, containers, labelRange2) == chest).ToList())
		{
			if (!item.IsOwnerOrUnassigned())
			{
				item.ClaimOwnership();
			}
			item.ZDO.Set(AutoLabelItemKey, string.Empty);
			item.ZDO.Set(AutoLabelPriorityKey, 0, false);
			ServersideQoLZDO.ZDOVars vars = item.Vars;
			vars.SetText("<color=white>...");
		}
	}

	private void UpdateNoSpace(string id, Dictionary<string, int> current)
	{
		_noSpace.TryGetValue(id, out var previous);
		if (current.Count == 0)
		{
			if (previous != null)
			{
				this.Logger.LogInfo((object)("Warehouse '" + id + "': all input items found room again."));
			}
			_noSpace.Remove(id);
			_noSpaceNotices.Remove(id);
			return;
		}
		int value;
		bool num = previous == null || current.Any((KeyValuePair<string, int> pair) => !previous.TryGetValue(pair.Key, out value) || pair.Value > value);
		_noSpace[id] = current;
		if (num)
		{
			_noSpaceNotices[id] = new DeliveryNotice(current, WarehouseClock.UtcNow.AddSeconds(Math.Max(2f, ConfigBase<WarehouseConfig>.Instance.DeliveryNoticeSeconds.Value)));
			this.Logger.LogWarning((object)("Warehouse '" + id + "': no room for " + string.Join(", ", current.Select((KeyValuePair<string, int> x) => x.Key + " x" + x.Value)) + "; items stay in IN. Add storage chests or a SONSTIGES chest."));
		}
	}

	private StatsCacheEntry TakeStatsHistory(ServersideQoLZDO anchor)
	{
		string text = anchor.ZDO.GetString(StatsHistoryKey, string.Empty);
		if (_statsCache.TryGetValue(anchor, out var value) && value.Raw == text)
		{
			_statsCache.Remove(anchor);
			return value;
		}
		_statsCache.Remove(anchor);
		return new StatsCacheEntry
		{
			Raw = text,
			History = WarehouseStatsHistory.Parse(text),
			WrittenAt = DateTime.MinValue
		};
	}

	private void StoreStatsHistory(ServersideQoLZDO anchor, StatsCacheEntry entry)
	{
		DateTime utcNow = WarehouseClock.UtcNow;
		if (entry.History.Changed && (utcNow - entry.WrittenAt).TotalSeconds >= 60.0)
		{
			string text = entry.History.Serialize();
			if (!anchor.IsOwnerOrUnassigned())
			{
				anchor.ClaimOwnership();
			}
			anchor.ZDO.Set(StatsHistoryKey, text);
			entry.Raw = text;
			entry.WrittenAt = utcNow;
		}
		_statsCache[anchor] = entry;
	}

	private void OnWorldSaveStarted()
	{
		try
		{
			FlushStatsHistories();
		}
		catch (Exception ex)
		{
			this.Logger.LogWarning((object)("Could not flush warehouse statistics before world save: " + ex.Message));
		}
		try
		{
			ExportStatisticsCsv();
		}
		catch (Exception ex2)
		{
			this.Logger.LogWarning((object)("Could not export warehouse statistics CSV: " + ex2.Message));
		}
	}

	private void FlushStatsHistories()
	{
		foreach (KeyValuePair<ServersideQoLZDO, StatsCacheEntry> item in _statsCache.ToList())
		{
			if (item.Value.History.Changed && _signs.Contains(item.Key))
			{
				string text = item.Value.History.Serialize();
				if (!item.Key.IsOwnerOrUnassigned())
				{
					item.Key.ClaimOwnership();
				}
				item.Key.ZDO.Set(StatsHistoryKey, text);
				item.Value.Raw = text;
				item.Value.WrittenAt = WarehouseClock.UtcNow;
			}
		}
	}

	private void ExportStatisticsCsv()
	{
		string path = DataDirectory;
		Directory.CreateDirectory(path);
		foreach (KeyValuePair<ServersideQoLZDO, StatsCacheEntry> item in _statsCache.ToList())
		{
			string text = item.Key.ZDO.GetString(StatsWarehouseKey, string.Empty).Trim();
			if (text.Length != 0 && Regex.IsMatch(text, "^[A-Za-z0-9_-]+$"))
			{
				string language = LanguageForWarehouse(text);
				string text2 = Path.Combine(path, "ModerWarehouse-statistics-" + text.ToUpperInvariant() + ".csv");
				string text3 = text2 + ".tmp";
				File.WriteAllText(text3, item.Value.History.ToCsv((string token) => LocalizedTokenName(token, language)), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
				if (File.Exists(text2))
				{
					File.Delete(text2);
				}
				File.Move(text3, text2);
			}
		}
	}

	private void SnapshotStatistics(WarehouseStatsHistory stats, List<ServersideQoLZDO> stores, ContainerRegistryProcessor registry)
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.Ordinal);
		int num = 0;
		foreach (ServersideQoLZDO store in stores)
		{
			bool flag = false;
			IInventory obj = InventoryFor(registry, store);
			IEnumerable<ItemData> enumerable = ((obj != null) ? obj.Items : null);
			foreach (ItemData item in enumerable ?? Enumerable.Empty<ItemData>())
			{
				if (item != null && item.m_stack > 0 && item.m_shared != null)
				{
					flag = true;
					string name = item.m_shared.m_name;
					dictionary[name] = (dictionary.TryGetValue(name, out var value) ? (value + item.m_stack) : item.m_stack);
				}
			}
			if (flag)
			{
				num++;
			}
		}
		stats.SetSnapshot(dictionary, num, stores.Count);
	}

	private void ReportSkippedCycle(string id, string reason, string displayKey, string language)
	{
		if (reason != null)
		{
			ShowInactivePanels(id, displayKey, language);
		}
		_skipReported.TryGetValue(id, out var value);
		if (!string.Equals(value, reason, StringComparison.Ordinal))
		{
			if (reason == null)
			{
				_skipReported.Remove(id);
				this.Logger.LogInfo((object)("Warehouse '" + id + "': processing resumed."));
				return;
			}
			_skipReported[id] = reason;
			this.Logger.LogWarning((object)("Warehouse '" + id + "': cycle skipped, boards show INACTIVE: " + reason + "."));
		}
	}

	private void ShowInactivePanels(string id, string displayKey, string language)
	{
		string text = "<color=#80ffff>" + WarehouseLocalization.Text(language, "board.warehouse") + "\n" + id.ToUpperInvariant();
		string text2 = "<color=#ff6666>" + WarehouseLocalization.Text(language, "board.inactive");
		string text3 = "<color=#ffb347>" + WarehouseLocalization.Text(language, displayKey ?? "board.no_input");
		foreach (IGrouping<string, ServersideQoLZDO> item in from sign in _signs
			where sign.ZDO.GetString(BoardWarehouseKey, string.Empty).Equals(id, StringComparison.OrdinalIgnoreCase) || sign.ZDO.GetString(StatsWarehouseKey, string.Empty).Equals(id, StringComparison.OrdinalIgnoreCase)
			group sign by sign.ZDO.GetString(BoardGroupKey, string.Empty) + "|" + sign.ZDO.GetString(StatsGroupKey, string.Empty))
		{
			bool stats = item.Key.StartsWith("|", StringComparison.Ordinal);
			Dictionary<int, ServersideQoLZDO> cells = (from sign in item
				group sign by sign.ZDO.GetInt(stats ? StatsCellKey : BoardCellKey, -1)).ToDictionary((IGrouping<int, ServersideQoLZDO> x) => x.Key, (IGrouping<int, ServersideQoLZDO> x) => x.First());
			int num = (stats ? 16 : 16);
			for (int num2 = 0; num2 < num; num2++)
			{
				WriteBoardCell(cells, num2, num2 switch
				{
					2 => text3, 
					1 => text2, 
					0 => text, 
					_ => "<color=#8090a0>-", 
				});
			}
		}
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
		foreach (ServersideQoLZDO sign in _signs)
		{
			if (sign != inputSign)
			{
				WarehouseText.MatchCache marker = Marker;
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
		int num = 0;
		string text = inputSign.ZDO.GetString(HelpGroupKey, string.Empty);
		HashSet<string> hashSet = new HashSet<string>(from sign in _signs
			where sign.ZDO.GetString(BoardWarehouseKey, string.Empty).Equals(oldId, StringComparison.OrdinalIgnoreCase) || sign.ZDO.GetString(BoardWarehouseKey, string.Empty).Equals(newId, StringComparison.OrdinalIgnoreCase)
			select sign.ZDO.GetString(BoardGroupKey, string.Empty) into @group
			where !string.IsNullOrWhiteSpace(@group)
			select @group, StringComparer.Ordinal);
		HashSet<string> hashSet2 = new HashSet<string>(from sign in _signs
			where sign.ZDO.GetString(StatsWarehouseKey, string.Empty).Equals(oldId, StringComparison.OrdinalIgnoreCase) || sign.ZDO.GetString(StatsWarehouseKey, string.Empty).Equals(newId, StringComparison.OrdinalIgnoreCase)
			select sign.ZDO.GetString(StatsGroupKey, string.Empty) into @group
			where !string.IsNullOrWhiteSpace(@group)
			select @group, StringComparer.Ordinal);
		foreach (ServersideQoLZDO item in _signs.Where((ServersideQoLZDO sign) => sign != inputSign).ToList())
		{
			bool flag = false;
			int[] array = new int[5] { RequestWarehouseKey, InfoWarehouseKey, BoardWarehouseKey, StatsWarehouseKey, HelpWarehouseKey };
			foreach (int num3 in array)
			{
				if (item.ZDO.GetString(num3, string.Empty).Equals(oldId, StringComparison.OrdinalIgnoreCase))
				{
					SetWarehouseKey(item, num3, newId);
					flag = true;
				}
			}
			string text2 = item.ZDO.GetString(BoardGroupKey, string.Empty);
			if (!string.IsNullOrWhiteSpace(text2) && hashSet.Contains(text2) && !item.ZDO.GetString(BoardWarehouseKey, string.Empty).Equals(newId, StringComparison.OrdinalIgnoreCase))
			{
				SetWarehouseKey(item, BoardWarehouseKey, newId);
				flag = true;
			}
			string text3 = item.ZDO.GetString(StatsGroupKey, string.Empty);
			if (!string.IsNullOrWhiteSpace(text3) && hashSet2.Contains(text3) && !item.ZDO.GetString(StatsWarehouseKey, string.Empty).Equals(newId, StringComparison.OrdinalIgnoreCase))
			{
				SetWarehouseKey(item, StatsWarehouseKey, newId);
				flag = true;
			}
			if (!string.IsNullOrWhiteSpace(text) && item.ZDO.GetBool(HelpChildKey, false) && item.ZDO.GetString(HelpGroupKey, string.Empty) == text && !item.ZDO.GetString(HelpWarehouseKey, string.Empty).Equals(newId, StringComparison.OrdinalIgnoreCase))
			{
				SetWarehouseKey(item, HelpWarehouseKey, newId);
				flag = true;
			}
			ServersideQoLZDO.ZDOVars vars = item.Vars;
			string value = PlainText(vars.GetText(""));
			Match match = Marker.Match(value);
			if (match.Success && !match.Groups["role"].Value.Equals("IN", StringComparison.OrdinalIgnoreCase) && match.Groups["id"].Value.Equals(oldId, StringComparison.OrdinalIgnoreCase))
			{
				string text4 = "[WH:" + newId + ":" + match.Groups["role"].Value.ToUpperInvariant() + "]" + match.Groups["payload"].Value;
				if (!item.IsOwnerOrUnassigned())
				{
					item.ClaimOwnership();
				}
				vars = item.Vars;
				vars.SetText(text4);
				flag = true;
			}
			if (flag)
			{
				num++;
			}
		}
		MigrateWarehouseRuntimeState(oldId, newId);
		_warehousesDirty.Add(newId);
		inputSign.ZDO.Set(HelpWarehouseKey, newId);
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
		_panelPositions.Remove(oldId);
		_acceptedWarehouseIds = _acceptedWarehouseIds.Select((string known) => (!known.Equals(oldId, StringComparison.OrdinalIgnoreCase)) ? known : newId).ToList();
		_panelPositions.Remove(newId);
		bool num2 = _warehousesDirty.Remove(oldId);
		MoveRuntimeValue(_deliveryNotices, oldId, newId);
		MoveRuntimeValue(_noSpace, oldId, newId);
		MoveRuntimeValue(_noSpaceNotices, oldId, newId);
		MoveRuntimeValue(_requestSearchNotices, oldId, newId);
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

	private static bool SetWarehouseKey(ServersideQoLZDO sign, int key, string id)
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
		List<WarehouseNode> list = new List<WarehouseNode>();
		foreach (ServersideQoLZDO sign in _signs)
		{
			WarehouseText.MatchCache marker = Marker;
			ServersideQoLZDO.ZDOVars vars = sign.Vars;
			Match match = marker.Match(PlainText(vars.GetText("")));
			if (match.Success && match.Groups["role"].Value.Equals("IN", StringComparison.OrdinalIgnoreCase))
			{
				ServersideQoLZDO val = NearestContainer(sign, containers, markerRange2);
				if (val != null)
				{
					float num = ParseInputRadius(match.Groups["payload"].Value);
					sign.ZDO.Set(InputRadiusKey, num);
					list.Add(new WarehouseNode(match.Groups["id"].Value, sign, val, num, ResolveLanguage(match.Groups["payload"].Value)));
				}
			}
		}
		return list;
	}

	private unsafe void ValidateWarehouseNodes(List<WarehouseNode> nodes, float markerRange2)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal) { ["CFG"] = ConfigBase<WarehouseConfig>.Instance.MinimumWarehouseDistance.Value.ToString("R", CultureInfo.InvariantCulture) };
		Vector3 position;
		foreach (ServersideQoLZDO sign in _signs)
		{
			WarehouseText.MatchCache marker = Marker;
			ServersideQoLZDO.ZDOVars vars = sign.Vars;
			Match match = marker.Match(PlainText(vars.GetText("")));
			if (match.Success && match.Groups["role"].Value.Equals("IN", StringComparison.OrdinalIgnoreCase))
			{
				string text = sign.ZDO.m_uid.ToString();
				hashSet.Add(text);
				string key = "M:" + text;
				string[] obj = new string[5]
				{
					match.Groups["id"].Value,
					":",
					null,
					null,
					null
				};
				position = sign.ZDO.GetPosition();
				obj[2] = position.ToString("R");
				obj[3] = ":";
				obj[4] = ParseInputRadius(match.Groups["payload"].Value).ToString("R", CultureInfo.InvariantCulture);
				dictionary[key] = string.Concat(obj);
			}
		}
		foreach (WarehouseNode node in nodes)
		{
			ZDOID uid = node.Sign.ZDO.m_uid;
			string key2 = "N:" + uid.ToString();
			uid = node.Input.ZDO.m_uid;
			string? text2 = uid.ToString();
			position = node.Input.ZDO.GetPosition();
			dictionary[key2] = text2 + ":" + position.ToString("R");
		}
		string value3;
		string key3 = dictionary.FirstOrDefault((KeyValuePair<string, string> pair) => !_validatedTopologyParts.TryGetValue(pair.Key, out value3) || (value3 != pair.Value && !value3.StartsWith("~", StringComparison.Ordinal))).Key;
		bool flag = hashSet.Any((string key4) => !_globalInputErrors.ContainsKey(key4));
		if (GlobalScanPolicy.ShouldValidateTopology(flag, key3 != null, WarehouseClock.UtcNow, _globalInputValidationAt))
		{
			if (_validatedTopologyParts.Count > 0 && (key3 != null || flag))
			{
				this.Logger.LogInfo((object)("Warehouse topology revalidation: " + ((key3 != null) ? ("changed '" + key3 + "'") : "new IN marker") + "."));
			}
			Dictionary<string, string> dictionary2 = new Dictionary<string, string>(StringComparer.Ordinal);
			RefreshGlobalInputValidation(markerRange2, dictionary2);
			foreach (KeyValuePair<string, string> item in dictionary2)
			{
				_validatedTopologyParts[item.Key] = item.Value;
			}
			foreach (KeyValuePair<string, string> item2 in dictionary)
			{
				if (!dictionary2.ContainsKey(item2.Key))
				{
					_validatedTopologyParts[item2.Key] = item2.Value;
				}
			}
		}
		else
		{
			foreach (KeyValuePair<string, string> item3 in dictionary)
			{
				if (_validatedTopologyParts.TryGetValue(item3.Key, out var value) && value.Length == item3.Value.Length + 1 && value.StartsWith("~", StringComparison.Ordinal) && string.CompareOrdinal(value, 1, item3.Value, 0, item3.Value.Length) == 0)
				{
					_validatedTopologyParts[item3.Key] = item3.Value;
				}
			}
		}
		foreach (WarehouseNode node2 in nodes)
		{
			if (_globalInputErrors.TryGetValue(node2.Sign.ZDO.m_uid.ToString(), out var value2) && !string.IsNullOrWhiteSpace(value2))
			{
				node2.Valid = false;
				node2.Error = value2;
			}
		}
	}

	private unsafe void RefreshGlobalInputValidation(float markerRange2, Dictionary<string, string> facts)
	{
		try
		{
			if (!ScanWorld(out var containers, out var signs))
			{
				return;
			}
			PersistedContainerSpatialLookup persistedContainerSpatialLookup = new PersistedContainerSpatialLookup(containers);
			List<PersistedInputCandidate> list = new List<PersistedInputCandidate>();
			int num = 0;
			_globalInputErrors.Clear();
			foreach (PersistedSign item in signs)
			{
				Match match = Marker.Match(item.Text);
				if (match.Success && match.Groups["role"].Value.Equals("IN", StringComparison.OrdinalIgnoreCase))
				{
					ZDO val = persistedContainerSpatialLookup.Nearest(item.Zdo, markerRange2);
					string text = match.Groups["id"].Value.Trim();
					string text2 = item.Zdo.m_uid.ToString();
					string key = "M:" + text2;
					string[] obj = new string[5]
					{
						match.Groups["id"].Value,
						":",
						null,
						null,
						null
					};
					Vector3 position = item.Zdo.GetPosition();
					obj[2] = position.ToString("R");
					obj[3] = ":";
					obj[4] = ParseInputRadius(match.Groups["payload"].Value).ToString("R", CultureInfo.InvariantCulture);
					facts[key] = string.Concat(obj);
					if (val != null)
					{
						string key2 = "N:" + text2;
						ZDOID uid = val.m_uid;
						string? text3 = uid.ToString();
						position = val.GetPosition();
						facts[key2] = "~" + text3 + ":" + position.ToString("R");
					}
					if (val == null)
					{
						_globalInputErrors[item.Zdo.m_uid.ToString()] = "NO_INPUT";
						num++;
						this.Logger.LogWarning((object)$"IN '{text}' rejected: no input chest found within {Mathf.Sqrt(markerRange2):0.#} m.");
					}
					else
					{
						list.Add(new PersistedInputCandidate(text, item.Zdo, val, item.Zdo.GetBool(InputCanonicalKey, false)));
					}
				}
			}
			List<PersistedInputCandidate> list2 = new List<PersistedInputCandidate>();
			float minimum = Mathf.Max(1f, ConfigBase<WarehouseConfig>.Instance.MinimumWarehouseDistance.Value);
			list.Sort((PersistedInputCandidate left, PersistedInputCandidate right) => WarehouseDuplicatePolicy.Compare(left.Canonical, left.Sign.m_uid.ToString(), right.Canonical, right.Sign.m_uid.ToString()));
			foreach (PersistedInputCandidate candidate in list)
			{
				if (list2.FirstOrDefault((PersistedInputCandidate item) => item.Id.Equals(candidate.Id, StringComparison.OrdinalIgnoreCase)) != null)
				{
					_globalInputErrors[candidate.Sign.m_uid.ToString()] = "DUPLICATE";
					this.Logger.LogWarning((object)("IN '" + candidate.Id + "' rejected: warehouse name already exists."));
					continue;
				}
				PersistedInputCandidate persistedInputCandidate = list2.FirstOrDefault((PersistedInputCandidate item) => WarehouseDistancePolicy.IsTooClose(Vector3.Distance(item.Input.GetPosition(), candidate.Input.GetPosition()), minimum));
				if (persistedInputCandidate != null)
				{
					float num2 = Vector3.Distance(persistedInputCandidate.Input.GetPosition(), candidate.Input.GetPosition());
					_globalInputErrors[candidate.Sign.m_uid.ToString()] = $"TOO_CLOSE|{persistedInputCandidate.Id.ToUpperInvariant()}|{num2:0}|{minimum:0}";
					this.Logger.LogWarning((object)$"IN '{candidate.Id}' rejected: {num2:0.#} m from '{persistedInputCandidate.Id}', minimum is {minimum:0.#} m.");
				}
				else
				{
					list2.Add(candidate);
					if (!candidate.Canonical)
					{
						candidate.Sign.Set(InputCanonicalKey, true);
					}
					_globalInputErrors[candidate.Sign.m_uid.ToString()] = string.Empty;
				}
			}
			_acceptedWarehouseIds = list2.Select((PersistedInputCandidate item) => item.Id).ToList();
			HashSet<string> acceptedIds = new HashSet<string>(list2.Select((PersistedInputCandidate item) => item.Id), StringComparer.OrdinalIgnoreCase);
			bool flag = false;
			foreach (string item2 in _warehouseStockSnapshots.Keys.Where((string id) => !acceptedIds.Contains(id)).ToList())
			{
				_warehouseStockSnapshots.Remove(item2);
				_warehouseStockTimes.Remove(item2);
				_freshlyReadWarehouses.Remove(item2);
				flag = true;
			}
			if (flag)
			{
				SaveStockCache();
			}
			_globalInputValidationAt = WarehouseClock.UtcNow;
			this.Logger.LogInfo((object)($"Global warehouse topology: {list2.Count} accepted, " + $"{list.Count - list2.Count + num} blocked IN marker(s)."));
		}
		catch (Exception ex)
		{
			this.Logger.LogWarning((object)("Global warehouse topology validation failed: " + ex.Message));
		}
	}

	private void UpdateInputHelpSigns(List<WarehouseNode> nodes)
	{
		float num = Mathf.Max(1f, ConfigBase<WarehouseConfig>.Instance.MinimumWarehouseDistance.Value);
		foreach (WarehouseNode node in nodes)
		{
			string group = node.Sign.ZDO.GetString(HelpGroupKey, string.Empty);
			ServersideQoLZDO val = ((IEnumerable<ServersideQoLZDO>)_signs).FirstOrDefault((Func<ServersideQoLZDO, bool>)((ServersideQoLZDO x) => x.ZDO.GetBool(HelpChildKey, false) && x.ZDO.GetString(HelpGroupKey, string.Empty) == group));
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
			WarehouseText.MatchCache marker = Marker;
			ServersideQoLZDO.ZDOVars vars = sign.Vars;
			Match match = marker.Match(PlainText(vars.GetText("")));
			if (!match.Success || !match.Groups["role"].Value.Equals("IN", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			bool flag = WarehouseLocalization.Resolve(ResolveLanguage(match.Groups["payload"].Value)) == "de";
			string text2 = FormatInputError(value2, flag, num, value);
			string group = sign.ZDO.GetString(HelpGroupKey, string.Empty);
			ServersideQoLZDO val = ((IEnumerable<ServersideQoLZDO>)_signs).FirstOrDefault((Func<ServersideQoLZDO, bool>)((ServersideQoLZDO item) => item.ZDO.GetBool(HelpChildKey, false) && item.ZDO.GetString(HelpGroupKey, string.Empty) == group));
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
		HashSet<ServersideQoLZDO> endpoints = new HashSet<ServersideQoLZDO> { node.Input };
		foreach (ServersideQoLZDO sign in _signs)
		{
			WarehouseText.MatchCache marker = Marker;
			ServersideQoLZDO.ZDOVars vars = sign.Vars;
			Match match = marker.Match(PlainText(vars.GetText("")));
			if (!match.Success || !match.Groups["id"].Value.Equals(node.Id, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			string text = match.Groups["role"].Value.ToUpperInvariant();
			if (!(text != "IN") || !(text != "OUT") || !(text != "FILL"))
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
		Dictionary<ServersideQoLZDO, string> dictionary = DiscoverLabels(containers, ConfigBase<WarehouseConfig>.Instance.LabelDistance.Value * ConfigBase<WarehouseConfig>.Instance.LabelDistance.Value, signs, rememberItemTokens: false, node.Id);
		Dictionary<ServersideQoLZDO, ServersideQoLZDO> dictionary2 = DiscoverBlankSigns(containers, ConfigBase<WarehouseConfig>.Instance.LabelDistance.Value * ConfigBase<WarehouseConfig>.Instance.LabelDistance.Value, signs, node.Id);
		HashSet<ServersideQoLZDO> optedIn = new HashSet<ServersideQoLZDO>(dictionary.Keys);
		optedIn.UnionWith(dictionary2.Keys);
		float radius2 = node.Radius * node.Radius;
		return containers.Where((ServersideQoLZDO x) => optedIn.Contains(x) && WarehouseContainerPolicy.CanManage(endpoints.Contains(x), ignored.Contains(x), IsPrivateContainer(x), inUse: false, hasStorageSign: true) && Distance2(x, node.Input) <= radius2 && ContainsOnlyWarehouseItems(InventoryFor(registry, x))).ToList();
	}

	private List<ServersideQoLZDO> StorageSignsForNode(WarehouseNode node)
	{
		float signRadius = node.Radius + Math.Max(ConfigBase<WarehouseConfig>.Instance.LabelDistance.Value, ConfigBase<WarehouseConfig>.Instance.MarkerDistance.Value) + 2f;
		return _signs.Where((ServersideQoLZDO sign) => Distance2(sign, node.Input) <= signRadius * signRadius || LinkedInside(sign, node)).ToList();
	}

	private Dictionary<ServersideQoLZDO, string> DiscoverLabels(List<ServersideQoLZDO> containers, float range2, IEnumerable<ServersideQoLZDO> signs = null, bool rememberItemTokens = true, string warehouseId = null)
	{
		Dictionary<ServersideQoLZDO, string> dictionary = new Dictionary<ServersideQoLZDO, string>();
		if (rememberItemTokens)
		{
			_labelItemTokens.Clear();
			_labelWarehouseIds.Clear();
			_labelPriorities.Clear();
		}
		foreach (ServersideQoLZDO item in signs ?? StorageSigns)
		{
			string labelWarehouse = item.ZDO.GetString(AutoLabelWarehouseKey, string.Empty);
			if ((item.ZDO.GetBool(AutoLabelManagedKey, false) && !string.IsNullOrWhiteSpace(labelWarehouse) && !string.IsNullOrWhiteSpace(warehouseId) && !labelWarehouse.Equals(warehouseId, StringComparison.OrdinalIgnoreCase)) || IsRememberedRequest(item) || IsRememberedInfo(item) || IsRememberedBoard(item) || IsRememberedStats(item) || item.ZDO.GetBool(HelpChildKey, false))
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
				dictionary[val] = (dictionary.TryGetValue(val, out var value) ? (value + "\u001f" + text) : text);
				string value2 = item.ZDO.GetString(AutoLabelItemKey, string.Empty);
				if (rememberItemTokens && !string.IsNullOrWhiteSpace(value2))
				{
					_labelItemTokens[val] = value2;
				}
				if (rememberItemTokens && item.ZDO.GetBool(AutoLabelManagedKey, false))
				{
					_labelWarehouseIds[val] = item.ZDO.GetString(AutoLabelWarehouseKey, string.Empty);
					_labelPriorities[val] = item.ZDO.GetInt(AutoLabelPriorityKey, 0);
				}
			}
		}
		return dictionary;
	}

	private Dictionary<ServersideQoLZDO, ServersideQoLZDO> DiscoverBlankSigns(List<ServersideQoLZDO> containers, float range2, IEnumerable<ServersideQoLZDO> signs = null, string warehouseId = null)
	{
		Dictionary<ServersideQoLZDO, ServersideQoLZDO> dictionary = new Dictionary<ServersideQoLZDO, ServersideQoLZDO>();
		foreach (ServersideQoLZDO item in signs ?? StorageSigns)
		{
			string text = item.ZDO.GetString(AutoLabelWarehouseKey, string.Empty);
			if ((item.ZDO.GetBool(AutoLabelManagedKey, false) && !string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(warehouseId) && !text.Equals(warehouseId, StringComparison.OrdinalIgnoreCase)) || IsRememberedRequest(item) || IsRememberedInfo(item) || IsRememberedBoard(item) || IsRememberedStats(item) || item.ZDO.GetBool(HelpChildKey, false))
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

	private HashSet<ServersideQoLZDO> DiscoverIgnored(List<ServersideQoLZDO> containers, float range2)
	{
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
		if (!_nearestMemo.TryGetValue(containers, out var value))
		{
			value = (_nearestMemo[containers] = new Dictionary<(ServersideQoLZDO, float), ServersideQoLZDO>());
		}
		if (value.TryGetValue((sign, range2), out var value2))
		{
			return value2;
		}
		Vector3 position = sign.ZDO.GetPosition();
		ServersideQoLZDO val = Lookup(containers).Nearest(position.x, position.y, position.z, range2);
		value[(sign, range2)] = val;
		return val;
	}

	private static bool LinkedInside(ServersideQoLZDO sign, WarehouseNode node)
	{
		Vector3 val = new Vector3(sign.ZDO.GetFloat(AutoChestXKey, float.NaN), sign.ZDO.GetFloat(AutoChestYKey, float.NaN), sign.ZDO.GetFloat(AutoChestZKey, float.NaN)) - node.Input.ZDO.GetPosition();
		return val.sqrMagnitude <= node.Radius * node.Radius;
	}

	private string CreateAutomaticLabel(string warehouseId, ServersideQoLZDO chest, ItemData item, Dictionary<ServersideQoLZDO, ServersideQoLZDO> blankSigns, string language)
	{
		if (!blankSigns.TryGetValue(chest, out var value))
		{
			return null;
		}
		string text = LocalizedItemName(item, language);
		if (!value.IsOwnerOrUnassigned())
		{
			value.ClaimOwnership();
		}
		ServersideQoLZDO.ZDOVars vars = value.Vars;
		vars.SetText("<color=white>" + text);
		MarkAutomaticLabel(value, chest, item?.m_shared?.m_name, warehouseId, 0);
		this.Logger.LogInfo((object)("Reused blank sign and labeled chest '" + text + "'."));
		return text;
	}

	private static void MarkAutomaticLabel(ServersideQoLZDO sign, ServersideQoLZDO chest, string itemToken, string warehouseId, int priority)
	{
		Vector3 position = chest.ZDO.GetPosition();
		sign.ZDO.Set(AutoChestXKey, position.x);
		sign.ZDO.Set(AutoChestYKey, position.y);
		sign.ZDO.Set(AutoChestZKey, position.z);
		sign.ZDO.Set(AutoLabelManagedKey, true);
		sign.ZDO.Set(AutoLabelItemKey, itemToken ?? string.Empty);
		sign.ZDO.Set(AutoLabelWarehouseKey, warehouseId ?? string.Empty);
		sign.ZDO.Set(AutoLabelPriorityKey, (priority >= 1 && priority <= 99) ? priority : 0, false);
	}

	private void RefreshAutomaticLabels(List<ServersideQoLZDO> containers, float range2, ContainerRegistryProcessor registry, string language, string warehouseId)
	{
		foreach (ServersideQoLZDO storageSign in StorageSigns)
		{
			string text = storageSign.ZDO.GetString(AutoLabelItemKey, string.Empty);
			if (!storageSign.ZDO.GetBool(AutoLabelManagedKey, false))
			{
				continue;
			}
			string text2 = storageSign.ZDO.GetString(AutoLabelWarehouseKey, string.Empty);
			if (!string.IsNullOrWhiteSpace(text2) && !text2.Equals(warehouseId, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			if (string.IsNullOrWhiteSpace(text2))
			{
				storageSign.ZDO.Set(AutoLabelWarehouseKey, warehouseId);
			}
			ServersideQoLZDO.ZDOVars vars = storageSign.Vars;
			if (WarehouseContainerPolicy.IsExplicitLabelReset(PlainText(vars.GetText(""))))
			{
				if (!storageSign.IsOwnerOrUnassigned())
				{
					storageSign.ClaimOwnership();
				}
				storageSign.ZDO.Set(AutoLabelItemKey, string.Empty);
				storageSign.ZDO.Set(AutoLabelPriorityKey, 0, false);
				continue;
			}
			ServersideQoLZDO val = LinkedOrNearestContainer(storageSign, containers, range2);
			if (val == null || (_localStorage != null && !_localStorage.Contains(val)))
			{
				continue;
			}
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
				MarkAutomaticLabel(storageSign, val, text, warehouseId, 0);
			}
			int num = storageSign.ZDO.GetInt(AutoLabelPriorityKey, 0);
			vars = storageSign.Vars;
			if (WarehouseContainerPolicy.TryParseNumberedLabel(PlainText(vars.GetText("")), out var itemLabel, out var priority) && LabelNameMatchesToken(itemLabel, text))
			{
				num = priority;
				storageSign.ZDO.Set(AutoLabelPriorityKey, num, false);
			}
			string text3 = FormatAutomaticLabel(LocalizedTokenName(text, language), num);
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

	private static bool LabelNameMatchesToken(string label, string token)
	{
		string text = Normalize(label);
		if (text == Normalize(token) || text == Normalize(token?.Replace("$item_", string.Empty)))
		{
			return true;
		}
		if ((!GermanDisplayNames.TryGetValue(token ?? string.Empty, out var value) || !(text == Normalize(value))) && (!EnglishDisplayNames.TryGetValue(token ?? string.Empty, out var value2) || !(text == Normalize(value2))))
		{
			if (PrefabNames.TryGetValue(token ?? string.Empty, out var value3))
			{
				return text == Normalize(value3);
			}
			return false;
		}
		return true;
	}

	private static string FormatAutomaticLabel(string name, int priority)
	{
		if (priority < 1 || priority > 99)
		{
			return "<color=white>" + name;
		}
		return $"<color=white>{name} <size=60%>{priority}</size>";
	}

	private int FillPriority(ServersideQoLZDO chest, string warehouseId, string itemToken)
	{
		if (!_labelItemTokens.TryGetValue(chest, out var value) || !string.Equals(value, itemToken, StringComparison.Ordinal) || !_labelWarehouseIds.TryGetValue(chest, out var value2) || !string.Equals(value2, warehouseId, StringComparison.OrdinalIgnoreCase))
		{
			return int.MaxValue;
		}
		int value3;
		return WarehouseContainerPolicy.EffectiveFillPriority(_labelPriorities.TryGetValue(chest, out value3) ? value3 : 0);
	}

	private void LabelHomogeneousStores(string warehouseId, List<ServersideQoLZDO> stores, Dictionary<ServersideQoLZDO, string> labels, Dictionary<ServersideQoLZDO, ServersideQoLZDO> blankSigns, ContainerRegistryProcessor registry, string language)
	{
		if (!ConfigBase<WarehouseConfig>.Instance.AutoCreateLabels.Value)
		{
			return;
		}
		foreach (ServersideQoLZDO store in stores)
		{
			if (labels.ContainsKey(store))
			{
				continue;
			}
			ServersideQoLZDO.ZDOVars vars = store.Vars;
			if (vars.GetInUse(false) || registry.GetState(store) == null)
			{
				continue;
			}
			List<ItemData> list = InventoryFor(registry, store).Items.Where((ItemData x) => x != null && x.m_stack > 0 && x.m_shared != null).ToList();
			if (list.Count == 0)
			{
				continue;
			}
			string itemType = list[0].m_shared.m_name;
			if (!list.Any((ItemData x) => x.m_shared.m_name != itemType))
			{
				string value = CreateAutomaticLabel(warehouseId, store, list[0], blankSigns, language);
				if (!string.IsNullOrEmpty(value))
				{
					labels[store] = value;
				}
			}
		}
	}

	private bool IsPrivateContainer(ServersideQoLZDO chest)
	{
		return WarehouseContainerPolicy.IsPrivatePrefab(Processor.GetPrefabInfo(chest).PrefabName);
	}

	private void WhitenWarehouseLabels(List<ServersideQoLZDO> managed, List<ServersideQoLZDO> containers, float range2)
	{
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
		DateTime utcNow = WarehouseClock.UtcNow;
		DateTime value;
		bool flag = !_miscJanitorAt.TryGetValue(warehouseId, out value) || utcNow >= value;
		if (flag)
		{
			_miscJanitorAt[warehouseId] = utcNow.AddSeconds(30.0);
		}
		foreach (ServersideQoLZDO sourceZdo in stores.ToList())
		{
			if (moves <= 0 || !labels.TryGetValue(sourceZdo, out var value2) || (!flag && IsMiscChest(sourceZdo, labels)))
			{
				continue;
			}
			ContainerState state = registry.GetState(sourceZdo);
			IInventory val = InventoryFor(registry, sourceZdo);
			if (val == null || state == null)
			{
				continue;
			}
			foreach (ItemData item in val.Items.ToList())
			{
				if (moves <= 0)
				{
					break;
				}
				if (item == null || item.m_stack <= 0 || !IsWarehouseItem(item) || LabelMatches(sourceZdo, value2, item, registry))
				{
					continue;
				}
				if (!Own(state))
				{
					break;
				}
				int num2 = item.m_stack;
				string value4;
				foreach (ServersideQoLZDO item2 in from targetZdo in stores
					where targetZdo != sourceZdo
					where (!labels.TryGetValue(targetZdo, out value4)) ? IsSingleItemType(InventoryFor(registry, targetZdo), item) : LabelMatches(targetZdo, value4, item, registry)
					orderby FillPriority(targetZdo, warehouseId, item.m_shared.m_name), (!HasItem(InventoryFor(registry, targetZdo), item)) ? ((labels.TryGetValue(targetZdo, out value4) && LabelMatches(targetZdo, value4, item, registry)) ? 1 : 0) : 2 descending, Distance2(targetZdo, sourceZdo)
					select targetZdo)
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
							string value3;
							string text = (labels.TryGetValue(item2, out value3) ? LabelForLog(value3) : "(free/unlabelled)");
							this.Logger.LogInfo((object)($"Warehouse '{warehouseId}' janitor moved {num3} x " + "'" + item.m_shared.m_name + "' from '" + LabelForLog(value2) + "' to '" + text + "'."));
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

	private void ReportQuarantinedStorage(string warehouseId, List<ServersideQoLZDO> quarantined, ContainerRegistryProcessor registry)
	{
		string text = string.Join(";", quarantined.Select((ServersideQoLZDO chest) => chest.ZDO.m_uid.ToString()).OrderBy<string, string>((string uid) => uid, StringComparer.Ordinal));
		if (_quarantineReported.TryGetValue(warehouseId, out var value) && value == text)
		{
			return;
		}
		_quarantineReported[warehouseId] = text;
		if (quarantined.Count == 0)
		{
			return;
		}
		foreach (ServersideQoLZDO item in quarantined.Take(10))
		{
			Vector3 position = item.ZDO.GetPosition();
			IInventory obj = InventoryFor(registry, item);
			IEnumerable<ItemData> enumerable = ((obj != null) ? obj.Items : null);
			string text2 = string.Join(", ", from stack in enumerable ?? Enumerable.Empty<ItemData>()
				where stack != null && stack.m_stack > 0 && !IsWarehouseItem(stack)
				select $"{ItemName(stack)} x{stack.m_stack}" + (stack.m_cheated ? " (cheated)" : string.Empty));
			this.Logger.LogWarning((object)($"Warehouse '{warehouseId}': signed chest at {position.x:0.#}/{position.y:0.#}/{position.z:0.#} " + "is excluded from the warehouse because it holds non-warehouse item(s): " + text2 + ". Nothing was removed."));
		}
		if (quarantined.Count > 10)
		{
			this.Logger.LogWarning((object)$"Warehouse '{warehouseId}': {quarantined.Count - 10} further signed chest(s) are excluded for the same reason.");
		}
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

	private void ProcessRequest(string id, string language, Request request, ServersideQoLZDO outputZdo, List<ServersideQoLZDO> stores, ContainerRegistryProcessor registry, WarehouseStatsHistory stats, ref int moves, float markerRange2)
	{
		if (!IsWithdrawalRequest(request))
		{
			return;
		}
		if (outputZdo == null)
		{
			WriteStatus(request.Sign, WarehouseLocalization.Text(language, "request.no_output"));
			_requestClearAt[request.Sign] = WarehouseClock.UtcNow.AddSeconds(RequestDisplayPolicy.ClampResultSeconds(ConfigBase<WarehouseConfig>.Instance.RequestResultSeconds.Value));
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
				RefreshWorldStockIndex(markerRange2);
				worldRead = true;
			}
		};
		string[] array = (from x in request.Payload.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries)
			select x.Trim() into x
			where x.Length > 0
			select x).Take(10).ToArray();
		if (array.Length == 0)
		{
			WriteStatus(request.Sign, WarehouseLocalization.Text(language, "request.error_example"));
			return;
		}
		List<string> list = new List<string>();
		Dictionary<string, int> craftReserved = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		string[] array2 = array;
		foreach (string text in array2)
		{
			if (TryParseCraftRequest(text, out var craftQuery, out var craftCount, out var craftLevel))
			{
				list.Add(ProcessCraftRequest(id, language, craftQuery, craftCount, craftLevel, output, stores, registry, stats, ref moves, action, craftReserved));
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
				List<string> list2 = FindRemoteStock(id, language, query);
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
		WriteStatus(request.Sign, string.Join("\n", list));
		_requestClearAt[request.Sign] = WarehouseClock.UtcNow.AddSeconds(RequestDisplayPolicy.ClampResultSeconds(ConfigBase<WarehouseConfig>.Instance.RequestResultSeconds.Value));
	}

	private static bool IsWithdrawalRequest(Request request)
	{
		if (!string.IsNullOrWhiteSpace(request.Payload) && !IsStatus(request.Payload) && !IsRequestPrompt(request.Payload) && !RequestPolicy.TryStripKeyword(request.Payload, WarehouseLocalization.TargetKeywords(), out var _))
		{
			return !string.Equals(request.Payload, request.Sign.ZDO.GetString(RequestRenderedTextKey, string.Empty), StringComparison.Ordinal);
		}
		return false;
	}

	private string ProcessSingleRequest(string id, string language, string query, int amount, IInventory output, List<ServersideQoLZDO> stores, ContainerRegistryProcessor registry, WarehouseStatsHistory stats, ref int moves, out int locallyAvailable)
	{
		int num = amount;
		int num2 = 0;
		string text = query;
		List<ItemData> list = (from item in stores.SelectMany(delegate(ServersideQoLZDO store)
			{
				IInventory val2 = InventoryFor(registry, store);
				IEnumerable<ItemData> enumerable = ((val2 != null) ? val2.Items : null);
				return enumerable ?? Enumerable.Empty<ItemData>();
			})
			where item != null && item.m_stack > 0 && item.m_shared != null
			select item).ToList();
		string token = ChooseRequestToken(list, query);
		locallyAvailable = list.Where(Requested).Sum((ItemData item) => item.m_stack);
		bool flag = locallyAvailable > 0;
		foreach (ServersideQoLZDO item in stores.OrderByDescending(delegate(ServersideQoLZDO store)
		{
			IInventory obj = InventoryFor(registry, store);
			IEnumerable<ItemData> enumerable = ((obj != null) ? obj.Items : null);
			return (enumerable ?? Enumerable.Empty<ItemData>()).Where(Requested).Select(delegate(ItemData item)
			{
				int num4 = FillPriority(store, id, item.m_shared?.m_name);
				return (num4 == int.MaxValue) ? 1 : num4;
			}).DefaultIfEmpty(1)
				.Max();
		}))
		{
			if (moves <= 0 || num <= 0)
			{
				break;
			}
			ServersideQoLZDO.ZDOVars vars = item.Vars;
			if (vars.GetInUse(false))
			{
				continue;
			}
			ContainerState state = registry.GetState(item);
			if (state == null || !Own(state))
			{
				continue;
			}
			IInventory val = InventoryFor(registry, item);
			foreach (ItemData item2 in val.Items.Where(Requested).ToList())
			{
				if (moves <= 0 || num <= 0)
				{
					break;
				}
				flag = true;
				text = LocalizedItemName(item2, language);
				int num3 = Move(val, output, item2, num);
				if (num3 > 0)
				{
					num2 += num3;
					num -= num3;
					moves--;
					stats?.AddOutput(item2.m_shared.m_name, num3);
				}
			}
		}
		string result = ((num2 == amount) ? WarehouseLocalization.Format(language, "request.done", text, num2) : ((num2 > 0) ? WarehouseLocalization.Format(language, "request.partial", text, num2, amount) : (flag ? WarehouseLocalization.Format(language, "request.output_full", text) : WarehouseLocalization.Format(language, "request.not_found", query))));
		this.Logger.LogInfo((object)($"Warehouse '{id}': withdrawal '{query}' moved {num2}/{amount} item(s)." + ((locallyAvailable < amount) ? $" Local stock {locallyAvailable}/{amount}; informational world lookup may follow." : string.Empty)));
		return result;
		bool Requested(ItemData item)
		{
			if (token != null && item != null && item.m_stack > 0 && item.m_shared != null && item.m_shared.m_name == token)
			{
				return ItemMatchesQuery(item, query);
			}
			return false;
		}
	}

	private static string ChooseRequestToken(IEnumerable<ItemData> items, string query)
	{
		string text = Normalize(query);
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		Dictionary<string, (int, int)> dictionary = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
		foreach (ItemData item in items)
		{
			if (item?.m_shared != null && item.m_stack > 0 && ItemMatchesQuery(item, query))
			{
				string name = item.m_shared.m_name;
				string value;
				string value2;
				bool localizedName = (EnglishNames.TryGetValue(name, out value) && value == text) || (GermanNames.TryGetValue(name, out value2) && value2 == text);
				bool prefabOrToken = Normalize(ItemName(item)) == text || Normalize(name.Replace("$item_", string.Empty)) == text;
				int num = RequestPolicy.MatchRank(localizedName, prefabOrToken);
				dictionary[name] = (dictionary.TryGetValue(name, out var value3) ? (Math.Min(value3.Item1, num), value3.Item2 + item.m_stack) : (num, item.m_stack));
			}
		}
		return RequestPolicy.ChooseToken(dictionary.Select<KeyValuePair<string, (int, int)>, (string, int, int)>((KeyValuePair<string, (int Rank, int Amount)> pair) => (pair.Key, pair.Value.Rank, pair.Value.Amount)));
	}

	private List<string> FindRemoteStock(string localId, string language, string query)
	{
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, Dictionary<string, int>> item in _warehouseStockSnapshots.Where((KeyValuePair<string, Dictionary<string, int>> pair) => !pair.Key.Equals(localId, StringComparison.OrdinalIgnoreCase)).OrderBy<KeyValuePair<string, Dictionary<string, int>>, string>((KeyValuePair<string, Dictionary<string, int>> pair) => pair.Key, StringComparer.OrdinalIgnoreCase))
		{
			int num = item.Value.Where((KeyValuePair<string, int> stock) => TokenMatchesQuery(stock.Key, query)).Sum((KeyValuePair<string, int> stock) => stock.Value);
			if (num > 0)
			{
				list.Add(item.Key.ToUpperInvariant() + " " + FormatAmount(num) + " " + StockStatus(item.Key, language));
			}
		}
		return list;
	}

	private static bool TokenMatchesQuery(string token, string query)
	{
		string value = Normalize(query);
		if (string.IsNullOrWhiteSpace(value) || string.IsNullOrEmpty(token))
		{
			return false;
		}
		return Array.IndexOf(TokenAliases(token), value) >= 0;
	}

	private static string[] TokenAliases(string token)
	{
		string value;
		string text = (PrefabNames.TryGetValue(token, out value) ? value : token);
		string key = text + "\u0001" + token;
		if (AliasCache.TryGetValue(key, out var value2))
		{
			return value2;
		}
		string value3;
		string englishNormalized = (EnglishNames.TryGetValue(token, out value3) ? value3 : null);
		string value4;
		string germanNormalized = (GermanNames.TryGetValue(token, out value4) ? value4 : null);
		string[] array = WarehouseText.BuildAliases(text, token, englishNormalized, germanNormalized);
		AliasCache[key] = array;
		return array;
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

	private void RefreshWorldStockIndex(float markerRange2)
	{
		// The world index decodes chest contents from saved ZDO data, so pending moves must be written first.
		FlushInventorySaves();
		_freshlyReadWarehouses.Clear();
		DateTime utcNow = WarehouseClock.UtcNow;
		if (GlobalScanPolicy.ShouldRefreshWorldStock(utcNow, _worldStockRefreshAt))
		{
			bool num = RefreshPersistedWorldStockIndex(markerRange2);
			_worldStockRefreshAt = utcNow;
			if (num)
			{
				SaveStockCache();
			}
		}
	}

	private bool RefreshPersistedWorldStockIndex(float markerRange2)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		long num = 0L;
		long num2 = 0L;
		long num3 = 0L;
		try
		{
			List<ZDO> containers;
			List<PersistedSign> signs;
			int worldObjects;
			bool flag = ResolveWorldIndex(out containers, out signs, out worldObjects);
			if (containers == null)
			{
				return false;
			}
			num = stopwatch.ElapsedMilliseconds;
			PersistedContainerSpatialLookup persistedContainerSpatialLookup = new PersistedContainerSpatialLookup(containers);
			List<PersistedWarehouse> list = new List<PersistedWarehouse>();
			foreach (PersistedSign item in signs)
			{
				Match match = Marker.Match(item.Text);
				if (match.Success && match.Groups["role"].Value.Equals("IN", StringComparison.OrdinalIgnoreCase))
				{
					ZDO val = persistedContainerSpatialLookup.Nearest(item.Zdo, markerRange2);
					if (val != null)
					{
						list.Add(new PersistedWarehouse(match.Groups["id"].Value.Trim(), item.Zdo, val, ParseInputRadius(match.Groups["payload"].Value)));
					}
				}
			}
			if (list.Count == 0)
			{
				return false;
			}
			HashSet<ZDO> endpoints = new HashSet<ZDO>();
			HashSet<ZDO> ignored = new HashSet<ZDO>();
			foreach (PersistedSign item2 in signs)
			{
				Match match2 = Marker.Match(item2.Text);
				string text = (match2.Success ? match2.Groups["role"].Value.ToUpperInvariant() : string.Empty);
				bool flag2 = text == "IN" || text == "OUT" || text == "FILL";
				bool flag3 = IgnoreMarker.IsMatch(item2.Text);
				if (!flag2 && !flag3)
				{
					continue;
				}
				ZDO val2 = persistedContainerSpatialLookup.Nearest(item2.Zdo, markerRange2);
				if (val2 != null)
				{
					if (flag2)
					{
						endpoints.Add(val2);
					}
					if (flag3)
					{
						ignored.Add(val2);
					}
				}
			}
			float range = ConfigBase<WarehouseConfig>.Instance.LabelDistance.Value * ConfigBase<WarehouseConfig>.Instance.LabelDistance.Value;
			HashSet<ZDO> hashSet = new HashSet<ZDO>();
			foreach (PersistedSign item3 in signs)
			{
				string text2 = item3.Text ?? string.Empty;
				if (!Marker.IsMatch(text2) && !IgnoreMarker.IsMatch(text2) && !DeleteMarker.IsMatch(text2) && !CleanMarker.IsMatch(text2) && !item3.Zdo.GetBool(HelpChildKey, false) && string.IsNullOrWhiteSpace(item3.Zdo.GetString(RequestWarehouseKey, string.Empty)) && string.IsNullOrWhiteSpace(item3.Zdo.GetString(InfoWarehouseKey, string.Empty)) && string.IsNullOrWhiteSpace(item3.Zdo.GetString(BoardWarehouseKey, string.Empty)) && string.IsNullOrWhiteSpace(item3.Zdo.GetString(StatsWarehouseKey, string.Empty)))
				{
					ZDO val3 = persistedContainerSpatialLookup.Nearest(item3.Zdo, range);
					if (val3 != null)
					{
						hashSet.Add(val3);
					}
				}
			}
			num2 = stopwatch.ElapsedMilliseconds - num;
			string value2;
			List<PersistedWarehouse> validNodes = (from @group in list.Where((PersistedWarehouse candidate) => !_globalInputErrors.TryGetValue(candidate.Sign.m_uid.ToString(), out value2) || string.IsNullOrWhiteSpace(value2)).GroupBy<PersistedWarehouse, string>((PersistedWarehouse candidate) => candidate.Id, StringComparer.OrdinalIgnoreCase)
				select @group.First()).ToList();
			HashSet<ZDO> eligibleStorage = new HashSet<ZDO>(hashSet.Where((ZDO chest) => !endpoints.Contains(chest) && !ignored.Contains(chest) && validNodes.Any(delegate(PersistedWarehouse persistedWarehouse)
			{
				Vector3 val4 = chest.GetPosition() - persistedWarehouse.Input.GetPosition();
				return val4.sqrMagnitude <= persistedWarehouse.Radius * persistedWarehouse.Radius;
			})));
			List<PersistedContainer> list2 = new List<PersistedContainer>(eligibleStorage.Count);
			foreach (ZDO item4 in eligibleStorage)
			{
				PersistedContainer persistedContainer = DecodeCached(item4);
				if (persistedContainer != null)
				{
					list2.Add(persistedContainer);
				}
			}
			num3 = stopwatch.ElapsedMilliseconds - num - num2;
			bool result = false;
			DateTime utcNow = WarehouseClock.UtcNow;
			foreach (PersistedWarehouse node in validNodes)
			{
				float radius2 = node.Radius * node.Radius;
				Dictionary<string, int> dictionary = (from item in list2.Where(delegate(PersistedContainer container)
					{
						if (eligibleStorage.Contains(container.Zdo))
						{
							Vector3 val4 = container.Zdo.GetPosition() - node.Input.GetPosition();
							return val4.sqrMagnitude <= radius2;
						}
						return false;
					}).SelectMany((PersistedContainer container) => container.Items)
					where item != null && item.m_stack > 0 && item.m_shared != null && IsWarehouseItem(item)
					group item by item.m_shared.m_name).ToDictionary<IGrouping<string, ItemData>, string, int>((IGrouping<string, ItemData> group) => group.Key, (IGrouping<string, ItemData> group) => group.Sum((ItemData item) => item.m_stack), StringComparer.Ordinal);
				if (!_warehouseStockSnapshots.TryGetValue(node.Id, out var value) || !StockEqual(value, dictionary))
				{
					_warehouseStockSnapshots[node.Id] = dictionary;
					result = true;
				}
				_warehouseStockTimes[node.Id] = utcNow;
				_freshlyReadWarehouses.Add(node.Id);
				result = true;
			}
			stopwatch.Stop();
			if (stopwatch.ElapsedMilliseconds >= 100)
			{
				this.Logger.LogInfo((object)($"Global read-only stock query: {list.Count} warehouse(s), " + $"{list2.Count}/{eligibleStorage.Count} eligible stocked container(s), " + $"{worldObjects} world objects, {signs.Count} signs; " + string.Format("{0} ms ({1} {2}, ", stopwatch.ElapsedMilliseconds, flag ? "index" : "scan", num) + $"signs {num2}, inventory decode {num3})."));
			}
			return result;
		}
		catch (Exception ex)
		{
			this.Logger.LogWarning((object)("Global read-only stock query failed; using indexed/last-known data. " + ex.Message));
			return false;
		}
	}

	// The world's chests and signs come from the world index (WorldObjectIndex), kept as objects are
	// created and destroyed, instead of a walk of every object; decoded inventories of chests that left
	// the world are dropped.
	private bool ScanWorld(out List<ZDO> containers, out List<PersistedSign> signs)
	{
		containers = new List<ZDO>();
		signs = new List<PersistedSign>();
		if (!ReadWorldChestsAndSigns(containers, signs))
		{
			containers = null;
			signs = null;
			return false;
		}
		if (_decodedContainers.Count > 0)
		{
			HashSet<ZDOID> live = new HashSet<ZDOID>(containers.Select((ZDO x) => x.m_uid));
			foreach (ZDOID item in _decodedContainers.Keys.Where((ZDOID uid) => !live.Contains(uid)).ToList())
			{
				_decodedContainers.Remove(item);
			}
		}
		return true;
	}

	// True when the chests and signs came from the index; containers is null when the world cannot be read.
	private bool ResolveWorldIndex(out List<ZDO> containers, out List<PersistedSign> signs, out int worldObjects)
	{
		bool flag = ScanWorld(out containers, out signs);
		worldObjects = (flag ? (containers.Count + signs.Count) : 0);
		return flag;
	}

	private PersistedContainer DecodeCached(ZDO chest)
	{
		uint dataRevision = chest.DataRevision;
		if (_decodedContainers.TryGetValue(chest.m_uid, out var value) && value.Key == dataRevision && value.Value?.Zdo == chest)
		{
			return value.Value;
		}
		byte[] byteArray = chest.GetByteArray(ZDOVars.s_items, (byte[])null);
		PersistedContainer persistedContainer = ((byteArray == null || byteArray.Length == 0) ? null : ReadPersistedContainer(chest, byteArray));
		_decodedContainers[chest.m_uid] = new KeyValuePair<uint, PersistedContainer>(dataRevision, persistedContainer);
		return persistedContainer;
	}

	// Unity's frame count: the warm-up spends a few milliseconds per frame. The tests have no engine.
	private static int FrameCount()
	{
		return Time.frameCount;
	}

	private bool IsDecoded(ContainerRegistryProcessor registry, ServersideQoLZDO zdo)
	{
		if (_warmReflectionFailed)
		{
			return true;
		}
		try
		{
			ContainerState state = registry.GetState(zdo);
			if (state == null)
			{
				return true;
			}
			Type type = ((object)state).GetType();
			if (type != _warmStateType)
			{
				_warmStateType = type;
				_warmInventoryField = type.GetField("_inventory", BindingFlags.Instance | BindingFlags.NonPublic);
				if (_warmInventoryField == null)
				{
					_warmReflectionFailed = true;
					this.Logger.LogWarning((object)(type.FullName + "._inventory not found: inventory warm-up is off."));
					return true;
				}
			}
			return _warmInventoryField.GetValue(state) != null;
		}
		catch (Exception ex)
		{
			_warmReflectionFailed = true;
			this.Logger.LogWarning((object)("Inventory warm-up is off: the container state could not be inspected. " + ex));
			return true;
		}
	}

	private bool DeferForInventoryWarmUp(string id, IEnumerable<ServersideQoLZDO> candidates)
	{
		DateTime utcNow = WarehouseClock.UtcNow;
		DateTime value;
		bool flag = _warmDeadline.TryGetValue(id, out value);
		if (flag && utcNow >= value)
		{
			_warmDeadline.Remove(id);
			return false;
		}
		if (!flag && _warmArmedAt.TryGetValue(id, out var value2) && (utcNow - value2).TotalSeconds < 60.0)
		{
			return false;
		}
		ContainerRegistryProcessor registry = Processor.Instance<ContainerRegistryProcessor>();
		int num = 0;
		foreach (ServersideQoLZDO candidate in candidates)
		{
			if (!IsDecoded(registry, candidate))
			{
				num++;
				if (_warmQueued.Add(candidate))
				{
					_warmQueue.Enqueue(candidate);
				}
			}
		}
		if (num < 48)
		{
			_warmDeadline.Remove(id);
			return false;
		}
		if (!flag)
		{
			_warmDeadline[id] = utcNow.AddSeconds(3.0);
			_warmArmedAt[id] = utcNow;
			this.Logger.LogInfo((object)$"Warehouse '{id}': decoding {num} chest inventories over the next frames before the first cycle.");
		}
		return true;
	}

	private void WarmInventories()
	{
		if (_warmQueue.Count == 0)
		{
			return;
		}
		int frameCount = FrameCount();
		if (frameCount != _warmFrame)
		{
			_warmFrame = frameCount;
			_warmSpentMs = 0.0;
		}
		if (_warmSpentMs >= 4.0)
		{
			return;
		}
		ContainerRegistryProcessor val = Processor.Instance<ContainerRegistryProcessor>();
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (_warmQueue.Count > 0 && _warmSpentMs + stopwatch.Elapsed.TotalMilliseconds < 4.0)
		{
			ServersideQoLZDO val2 = _warmQueue.Dequeue();
			_warmQueued.Remove(val2);
			if (val2 == null)
			{
				continue;
			}
			try
			{
				ContainerState state = val.GetState(val2);
				if (state != null)
				{
					state.GetInventory();
				}
			}
			catch (Exception ex)
			{
				Vector3 position = val2.ZDO.GetPosition();
				this.Logger.LogWarning((object)$"Inventory warm-up: the chest at {position.x:0.#}/{position.y:0.#}/{position.z:0.#} could not be read. {ex}");
			}
		}
		_warmSpentMs += stopwatch.Elapsed.TotalMilliseconds;
		if (_warmQueue.Count != 0)
		{
			return;
		}
		foreach (string key in _warmDeadline.Keys)
		{
			_warehousesDirty.Add(key);
		}
	}

	private string CanonicalWarehouseId(string id)
	{
		if (_knownNodes.TryGetValue(id, out var value))
		{
			return value.Id;
		}
		foreach (string acceptedWarehouseId in _acceptedWarehouseIds)
		{
			if (acceptedWarehouseId.Equals(id, StringComparison.OrdinalIgnoreCase))
			{
				return acceptedWarehouseId;
			}
		}
		return id;
	}

	private static PersistedContainer ReadPersistedContainer(ZDO zdo, byte[] data)
	{
		//IL_003e: Expected O, but got Unknown
		//IL_0089: Expected O, but got Unknown
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

	private string ProcessCraftRequest(string id, string language, string query, int count, int level, IInventory output, List<ServersideQoLZDO> stores, ContainerRegistryProcessor registry, WarehouseStatsHistory stats, ref int moves, Action readWorld, Dictionary<string, int> reserved)
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
				List<string> list4 = FindRemoteStock(id, language, name);
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
		return WarehouseLocalization.StartsWithAnyTranslation(payload, "request.done", "request.partial", "request.output_full", "request.not_found", "request.wildcard_too_many", "request.craft_header", "request.craft_header_level", "request.craft_unknown", "request.craft_max_level", "request.error", "request.error_example", "request.suggestions", "request.shown_on_board", "request.no_output");
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

	private static void WriteRequestPrompt(ServersideQoLZDO sign, string language)
	{
		string value = sign.ZDO.GetString(RequestTargetsKey, string.Empty);
		string text = WarehouseLocalization.Text(language, "request.example_item");
		string text2 = (string.IsNullOrWhiteSpace(value) ? ("<color=#80ffff>" + WarehouseLocalization.Text(language, "request.idle") + "\n<color=white>" + text + " 100\n" + WarehouseLocalization.Text(language, "request.target") + " " + text + " 1000") : ("<color=#80ffff>" + WarehouseLocalization.Text(language, "request.title") + "\n<color=white>" + WarehouseLocalization.Text(language, "request.updating")));
		WriteRequestText(sign, text2);
	}

	private static void WriteRequestText(ServersideQoLZDO sign, string text)
	{
		string text2 = PlainText(text).Trim();
		sign.ZDO.Set(RequestRenderedTextKey, text2);
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

	private static void WriteStatus(ServersideQoLZDO sign, string status)
	{
		if (!sign.IsOwnerOrUnassigned())
		{
			sign.ClaimOwnership();
		}
		string text = "<color=white>" + status;
		sign.ZDO.Set(RequestRenderedTextKey, PlainText(text).Trim());
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
		inputSign.ZDO.Set(HelpWarehouseKey, id);
		string group = inputSign.ZDO.GetString(HelpGroupKey, string.Empty);
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
		ServersideQoLZDO val = ((IEnumerable<ServersideQoLZDO>)_signs).FirstOrDefault((Func<ServersideQoLZDO, bool>)((ServersideQoLZDO sign) => sign.ZDO.GetBool(HelpChildKey, false) && sign.ZDO.GetString(HelpGroupKey, string.Empty) == group));
		string text = "<size=70%><color=#80ffff>" + WarehouseLocalization.Text(language, "help.title") + "\n<color=white>" + WarehouseLocalization.Text(language, "help.in") + "\n" + WarehouseLocalization.Text(language, "help.req") + "\n" + WarehouseLocalization.Text(language, "help.out") + "\n" + WarehouseLocalization.Text(language, "help.board");
		ServersideQoLZDO.ZDOVars vars;
		DateTime value;
		if (val != null)
		{
			// Local patch 5: remove duplicate help signs left behind by earlier restarts (each one
			// got its own group id, so match them by position next to this IN sign).
			float helpRange2 = HelpSignRange * HelpSignRange;
			foreach (ServersideQoLZDO item in _signs.Where((ServersideQoLZDO sign) => sign != val && PlacedByWarehouse(sign) && sign.ZDO.GetBool("ModerWarehouse.HelpChild", false) && Distance2(sign, inputSign) <= helpRange2).ToList())
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
			val3.ZDO.Set(HelpGroupKey, group);
			val3.ZDO.Set(HelpChildKey, true);
			val3.ZDO.Set(HelpWarehouseKey, id);
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
		string group = anchor.ZDO.GetString(BoardGroupKey, string.Empty);
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
		List<ServersideQoLZDO> source = _signs.Where((ServersideQoLZDO x) => x.ZDO.GetString(BoardGroupKey, string.Empty) == group).ToList();
		int num = 0;
		foreach (IGrouping<int, ServersideQoLZDO> item in from x in source
			group x by x.ZDO.GetInt(BoardCellKey, -1))
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
			where x.ZDO.GetString(BoardGroupKey, string.Empty) == @group
			group x by x.ZDO.GetInt(BoardCellKey, -1)).ToDictionary((IGrouping<int, ServersideQoLZDO> x) => x.Key, (IGrouping<int, ServersideQoLZDO> x) => x.First());
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
			val2.ZDO.Set(BoardWarehouseKey, id);
			val2.ZDO.Set(BoardGroupKey, group);
			val2.ZDO.Set(BoardCellKey, num3, false);
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
		string group = anchor.ZDO.GetString(StatsGroupKey, string.Empty);
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
			where x.ZDO.GetString(StatsGroupKey, string.Empty) == @group
			group x by x.ZDO.GetInt(StatsCellKey, -1)).ToDictionary((IGrouping<int, ServersideQoLZDO> x) => x.Key, (IGrouping<int, ServersideQoLZDO> x) => x.First());
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
			val2.ZDO.Set(StatsWarehouseKey, id);
			val2.ZDO.Set(StatsGroupKey, group);
			val2.ZDO.Set(StatsCellKey, num2, false);
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
		if (!dictionary.Values.Any((ServersideQoLZDO x) => x.ZDO.GetBool(StatsControlKey, false)))
		{
			Vector3 val3 = position + rotation * new Vector3(-1.53f, -0.55f, 0f);
			ServersideQoLZDO val4 = this.PlacePiece(val3, Prefabs.Sign, rotation, (CreatorMarkers)2);
			if (val4 != null)
			{
				val4.Fields<Piece>().Set((Func<Expression<Func<Piece, bool>>>)(() => (Piece x) => x.m_canBeRemoved), true);
				val4.ZDO.Set(StatsWarehouseKey, id);
				val4.ZDO.Set(StatsGroupKey, group);
				val4.ZDO.Set(StatsCellKey, -2, false);
				val4.ZDO.Set(StatsControlKey, true);
				vars = val4.Vars;
				vars.SetText("<color=#80ffff>CONTROL\n<color=white>E: 1-" + 365 + " AUTO");
				ZDO zDO = val4.ZDO;
				int boardRenderedTextKey = BoardRenderedTextKey;
				vars = val4.Vars;
				zDO.Set(boardRenderedTextKey, PlainText(vars.GetText("")).Trim());
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
		if (string.IsNullOrWhiteSpace(group) || (cell != 1 && cell != 2))
		{
			return;
		}
		string text = sign.ZDO.GetString(BoardRenderedTextKey, string.Empty);
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

	private void WriteShoppingLists(string language, IEnumerable<ServersideQoLZDO> requestSigns, List<ServersideQoLZDO> stores, ContainerRegistryProcessor registry)
	{
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
			Dictionary<string, int> dictionary = DeserializeTargets(item.ZDO.GetString(RequestTargetsKey, string.Empty));
			if (dictionary.Count == 0)
			{
				continue;
			}
			ServersideQoLZDO.ZDOVars vars = item.Vars;
			string text = PlainText(vars.GetText("")).Trim();
			string b = item.ZDO.GetString(RequestRenderedTextKey, string.Empty);
			string rest;
			bool flag = RequestPolicy.TryStripKeyword(text, WarehouseLocalization.TargetKeywords(), out rest);
			if ((!string.Equals(text, b, StringComparison.Ordinal) && !flag) || IsStatus(text))
			{
				continue;
			}
			List<TargetStatus> list = (from x in dictionary.Select(delegate(KeyValuePair<string, int> target)
				{
					string token = ChooseRequestToken(items, target.Key);
					int current3 = ((token != null) ? items.Where((ItemData item) => item.m_shared.m_name == token && ItemMatchesQuery(item, target.Key)).Sum((ItemData item) => item.m_stack) : 0);
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
		if (_noSpace.ContainsKey(id))
		{
			text = "#ff6666";
			text2 = WarehouseLocalization.Text(language, "board.no_space");
		}
		Dictionary<string, StockEntry> dictionary = list.ToDictionary<StockEntry, string, StockEntry>((StockEntry entry) => entry.Token, (StockEntry entry) => entry, StringComparer.Ordinal);
		foreach (IGrouping<string, ServersideQoLZDO> item in from sign in boardSigns
			where !string.IsNullOrWhiteSpace(sign.ZDO.GetString(BoardGroupKey, string.Empty))
			group sign by sign.ZDO.GetString(BoardGroupKey, string.Empty))
		{
			Dictionary<int, ServersideQoLZDO> dictionary2 = (from sign in item
				group sign by sign.ZDO.GetInt(BoardCellKey, -1)).ToDictionary((IGrouping<int, ServersideQoLZDO> x) => x.Key, (IGrouping<int, ServersideQoLZDO> x) => x.First());
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
			if (_noSpaceNotices.TryGetValue(id, out var value4))
			{
				if (WarehouseClock.UtcNow < value4.Until)
				{
					WriteBoardMessage(dictionary2, id, language, WarehouseLocalization.Text(language, "board.no_space"), value4.Items.Select((KeyValuePair<string, int> x) => new BoardMessage(x.Key, "x" + FormatAmount(x.Value), "#ff6666")).ToList(), text, text2);
					continue;
				}
				_noSpaceNotices.Remove(id);
			}
			if (_deliveryNotices.TryGetValue(id, out var value5))
			{
				if (WarehouseClock.UtcNow < value5.Until)
				{
					WriteBoardMessage(dictionary2, id, language, WarehouseLocalization.Text(language, "board.deposited"), value5.Items.Select((KeyValuePair<string, int> x) => new BoardMessage(x.Key, "+" + FormatAmount(x.Value), "#80ff80")).ToList(), text, text2);
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
			if (_boardControls.TryGetValue(item.Key, out var value6))
			{
				if ((WarehouseClock.UtcNow - value6.ChangedAt).TotalSeconds >= 60.0)
				{
					_boardControls.Remove(item.Key);
				}
				else
				{
					boardControl = value6;
				}
			}
			List<StockEntry> list2 = list;
			if (boardControl != null && !string.IsNullOrWhiteSpace(boardControl.Prefix))
			{
				string prefix = Normalize(boardControl.Prefix);
				list2 = list.Where((StockEntry entry) => Normalize(entry.Name).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
			}
			bool flag2 = boardControl == null && NetworkWarehouseIds(network).Count > 1;
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

	private List<string> NetworkWarehouseIds(List<WarehouseNode> network)
	{
		return _acceptedWarehouseIds.Concat(from x in network
			where x.Valid
			select x.Id).Distinct<string>(StringComparer.OrdinalIgnoreCase).OrderBy<string, string>((string id) => id, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private NetworkOverview GetNetworkOverview(string language, List<WarehouseNode> network, List<ServersideQoLZDO> containers, float markerRange2, ContainerRegistryProcessor registry)
	{
		List<string> list = NetworkWarehouseIds(network);
		string text = WarehouseLocalization.Resolve(language) + "|" + string.Join("|", list.Select(delegate(string text2)
		{
			WarehouseNode warehouseNode2 = network.FirstOrDefault((WarehouseNode x) => x.Valid && x.Id.Equals(text2, StringComparison.OrdinalIgnoreCase));
			DateTime value2;
			return text2.ToUpperInvariant() + ":" + ((warehouseNode2 == null) ? ("remote:" + (_warehouseStockTimes.TryGetValue(text2, out value2) ? value2.Ticks : 0)) : (warehouseNode2.Radius.ToString("0.###", CultureInfo.InvariantCulture) + ":" + ((object)warehouseNode2.Input.ZDO.GetPosition()/*cast due to .constrained prefix*/).ToString()));
		}));
		if (_networkOverview != null && !_networkOverviewDirty && _networkOverview.Signature == text && (WarehouseClock.UtcNow - _networkOverviewAt).TotalSeconds < (double)Math.Max(3f, ConfigBase<WarehouseConfig>.Instance.ScanInterval.Value))
		{
			return _networkOverview;
		}
		List<BoardMessage> list2 = new List<BoardMessage>();
		int num = 0;
		int num2 = 0;
		bool flag = false;
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		foreach (string id in list)
		{
			WarehouseNode warehouseNode = network.FirstOrDefault((WarehouseNode x) => x.Valid && x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
			if (warehouseNode != null)
			{
				List<ServersideQoLZDO> list3 = StoresForNode(warehouseNode, containers, markerRange2, registry);
				List<ItemData> list4 = (from x in list3.SelectMany(delegate(ServersideQoLZDO x)
					{
						IInventory obj = InventoryFor(registry, x);
						IEnumerable<ItemData> enumerable = ((obj != null) ? obj.Items : null);
						return enumerable ?? Enumerable.Empty<ItemData>();
					})
					where x != null && x.m_stack > 0 && x.m_shared != null
					select x).ToList();
				int num3 = list4.Sum((ItemData x) => x.m_stack);
				num += list3.Count;
				num2 += num3;
				foreach (ItemData item in list4)
				{
					hashSet.Add(item.m_shared.m_name);
				}
				list2.Add(new BoardMessage(id.ToUpperInvariant(), $"{FormatAmount(num3)} / {list3.Count}K", "#80ffff"));
				continue;
			}
			flag = true;
			int num4 = 0;
			if (_warehouseStockSnapshots.TryGetValue(id, out var value))
			{
				foreach (KeyValuePair<string, int> item2 in value)
				{
					num4 += item2.Value;
					hashSet.Add(item2.Key);
				}
			}
			num2 += num4;
			list2.Add(new BoardMessage(id.ToUpperInvariant(), FormatAmount(num4) + " / ?K", "#8090a0"));
		}
		list2.Add(new BoardMessage(WarehouseLocalization.Text(language, "network.warehouses"), list.Count.ToString(CultureInfo.InvariantCulture), "#80ff80"));
		list2.Add(new BoardMessage(WarehouseLocalization.Text(language, "network.chests_world"), num.ToString(CultureInfo.InvariantCulture) + (flag ? "+" : string.Empty), "#80ff80"));
		list2.Add(new BoardMessage(WarehouseLocalization.Text(language, "network.stock_world"), FormatAmount(num2), "#80ff80"));
		list2.Add(new BoardMessage(WarehouseLocalization.Text(language, "network.types_world"), hashSet.Count.ToString(CultureInfo.InvariantCulture), "#80ff80"));
		_networkOverview = new NetworkOverview(text, list2);
		_networkOverviewAt = WarehouseClock.UtcNow;
		_networkOverviewDirty = false;
		return _networkOverview;
	}

	private void WriteStatisticsBoard(string id, string language, IEnumerable<ServersideQoLZDO> statsSigns, WarehouseStatsHistory history, List<ServersideQoLZDO> stores, ContainerRegistryProcessor registry, IReadOnlyList<Peer> peers, List<WarehouseNode> network, List<ServersideQoLZDO> containers, float markerRange2)
	{
		foreach (IGrouping<string, ServersideQoLZDO> item in from x in statsSigns
			where !string.IsNullOrWhiteSpace(x.ZDO.GetString(StatsGroupKey, string.Empty))
			group x by x.ZDO.GetString(StatsGroupKey, string.Empty))
		{
			Dictionary<int, ServersideQoLZDO> dictionary = (from x in item
				group x by x.ZDO.GetInt(StatsCellKey, -1)).ToDictionary((IGrouping<int, ServersideQoLZDO> x) => x.Key, (IGrouping<int, ServersideQoLZDO> x) => x.First());
			if (!dictionary.TryGetValue(12, out var _))
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
				WriteBoardCell(dictionary, 3, "<color=#8090a0>" + 365 + " " + WarehouseLocalization.Text(language, "stats.days_short"));
				SleepDisplayPolicy.Layout layout = SleepDisplayPolicy.Create(WarehouseLocalization.Text(language, "stats.sleep_word"), 12);
				for (int num2 = 0; num2 < 12; num2++)
				{
					string arg = ((num2 < layout.LabelLength) ? "#80ffff" : "#8090a0");
					WriteBoardCell(dictionary, num2 + 4, $"<color={arg}><size=180%>{layout.Text[num2]}");
				}
				continue;
			}
			double num3 = Math.Max(3f, ConfigBase<WarehouseConfig>.Instance.BoardPageSeconds.Value);
			int num4 = ((NetworkWarehouseIds(network).Count > 1) ? 5 : 4);
			int num5 = ((statsControl != null && statsControl.Page.HasValue) ? (statsControl.Page.Value - 1) : ((int)(WarehouseClock.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds / num3) % num4));
			int selectedDays = statsControl?.Days ?? (num5 switch
			{
				3 => 365, 
				0 => 1, 
				_ => 30, 
			});
			WriteBoardCell(dictionary, 1, "<color=white>" + WarehouseLocalization.Text(language, "stats.period") + "\n" + ((statsControl != null && statsControl.Days.HasValue) ? WarehouseLocalization.Format(language, "stats.days", selectedDays) : (num5 switch
			{
				1 => "7 / 30 / " + 365, 
				0 => WarehouseLocalization.Text(language, "stats.today"), 
				_ => WarehouseLocalization.Format(language, "stats.days", selectedDays), 
			})));
			WriteBoardCell(dictionary, 2, string.Format("<color=white>{0}\n{1} / {2}", WarehouseLocalization.Text(language, "board.page"), num5 + 1, num4));
			WriteBoardCell(dictionary, 3, "<color=white>" + WarehouseLocalization.Text(language, "stats.tracked_since") + "\n" + DayLabel(history.StartDayKey, fullYear: true));
			List<WarehouseStatsHistory.Day> source = history.Window(selectedDays);
			Dictionary<string, int> incoming = history.SumItems(selectedDays, input: true);
			Dictionary<string, int> outgoing = history.SumItems(selectedDays, input: false);
			List<BoardMessage> list = new List<BoardMessage>();
			switch (num5)
			{
			case 0:
			{
				int num10 = source.Sum((WarehouseStatsHistory.Day x) => x.Input);
				int num11 = source.Sum((WarehouseStatsHistory.Day x) => x.Output);
				int amount = history.CurrentStock.Values.Sum();
				int num12 = stores.Count(delegate(ServersideQoLZDO x)
				{
					IInventory obj = InventoryFor(registry, x);
					return obj != null && obj.Items.Any((ItemData i) => i != null && i.m_stack > 0);
				});
				double num13 = ((stores.Count == 0) ? 0.0 : (100.0 * (double)num12 / (double)stores.Count));
				list.Add(Metric(language, "stats.input", FormatAmount(num10), "#80ff80"));
				list.Add(Metric(language, "stats.output", FormatAmount(num11), "#ffb347"));
				list.Add(SignedMetric(language, "stats.balance", num10 - num11));
				list.Add(Metric(language, "stats.per_day", FormatAmount(num10 + num11), "#80ffff"));
				list.Add(ItemMessage(language, "stats.top_input", incoming, "#80ff80"));
				list.Add(ItemMessage(language, "stats.top_output", outgoing, "#ffb347"));
				list.Add(Metric(language, "stats.current", FormatAmount(amount), "#80ffff"));
				list.Add(Metric(language, "stats.item_types", history.CurrentStock.Count.ToString(), "white"));
				list.Add(Metric(language, "stats.chests_now", num12 + " / " + stores.Count, "white"));
				list.Add(Metric(language, "stats.avg_use", num13.ToString("0", CultureInfo.InvariantCulture) + "%", "#80ffff"));
				string value4 = ((num11 > 0) ? ((double)num10 / (double)num11).ToString("0.00", CultureInfo.InvariantCulture) : ((num10 > 0) ? "∞" : "-"));
				list.Add(Metric(language, "stats.ratio", value4, "#80ffff"));
				list.Add(Metric(language, "stats.days_tracked", history.TrackedDays.ToString(), "white"));
				break;
			}
			case 1:
			{
				int[] array = ((statsControl == null || !statsControl.Days.HasValue) ? new int[3] { 7, 30, 365 } : new int[3]
				{
					selectedDays,
					-selectedDays,
					365
				});
				foreach (int num7 in array)
				{
					List<WarehouseStatsHistory.Day> source2 = ((num7 < 0) ? history.PreviousWindow(-num7) : history.Window(num7));
					int num8 = source2.Sum((WarehouseStatsHistory.Day x) => x.Input);
					int num9 = source2.Sum((WarehouseStatsHistory.Day x) => x.Output);
					string value3 = ((num7 < 0) ? WarehouseLocalization.Text(language, "stats.previous") : WarehouseLocalization.Format(language, "stats.days", num7));
					list.Add(Metric(language, "stats.period", value3, "white"));
					list.Add(Metric(language, "stats.input", FormatAmount(num8), "#80ff80"));
					list.Add(Metric(language, "stats.output", FormatAmount(num9), "#ffb347"));
					list.Add(SignedMetric(language, "stats.balance", num8 - num9));
				}
				break;
			}
			case 2:
			{
				int value6;
				int value7;
				Dictionary<string, int> dictionary2 = incoming.Keys.Union(outgoing.Keys).ToDictionary((string k) => k, (string k) => (incoming.TryGetValue(k, out value6) ? value6 : 0) - (outgoing.TryGetValue(k, out value7) ? value7 : 0));
				List<KeyValuePair<string, int>> values = (from x in dictionary2
					where x.Value > 0
					orderby x.Value descending
					select x).Take(2).ToList();
				List<KeyValuePair<string, int>> values2 = (from x in dictionary2
					where x.Value < 0
					orderby x.Value
					select x).Take(2).ToList();
				bool flag = history.TrackedDays >= Math.Min(7, selectedDays);
				var anon = (from x in flag ? dictionary2 : new Dictionary<string, int>()
					where x.Value < 0 && history.CurrentStock.ContainsKey(x.Key)
					select new
					{
						Key = x.Key,
						Days = (double)history.CurrentStock[x.Key] * (double)selectedDays / (double)(-x.Value)
					} into x
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
				double chestsPerDay;
				double daysToFull;
				bool flag2 = history.TryCapacityForecast(Math.Max(selectedDays, 7), out chestsPerDay, out daysToFull);
				list.Add(Metric(language, "stats.chest_trend", (!flag2) ? "-" : (((chestsPerDay * 7.0 >= 0.0) ? "+" : string.Empty) + (chestsPerDay * 7.0).ToString("0.#", CultureInfo.InvariantCulture) + " / " + WarehouseLocalization.Text(language, "stats.week")), (chestsPerDay > 0.0) ? "#ffb347" : "#80ff80"));
				list.Add(Metric(language, "stats.full_in", (!flag2) ? WarehouseLocalization.Text(language, "stats.not_enough") : (double.IsInfinity(daysToFull) ? "-" : ((daysToFull <= 0.0) ? WarehouseLocalization.Text(language, "board.expand") : (daysToFull.ToString("0", CultureInfo.InvariantCulture) + " d"))), (!flag2 || double.IsInfinity(daysToFull)) ? "#80ff80" : ((daysToFull < 14.0) ? "#ff6666" : "#ffb347")));
				string text = Math.Min(history.TrackedDays, selectedDays) + " / " + selectedDays + " d";
				list.Add(Metric(language, "stats.confidence", flag ? text : (WarehouseLocalization.Text(language, "stats.not_enough") + " " + text), flag ? "#80ff80" : "#ffb347"));
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
			if (dictionary.TryGetValue(-2, out var _))
			{
				string text2;
				if (statsControl == null)
				{
					text2 = "<color=#80ffff>" + WarehouseLocalization.Text(language, "stats.control") + "\n<color=white>E: 1-" + 365 + " AUTO";
				}
				else
				{
					int num15 = Math.Max(0, 60 - (int)(WarehouseClock.UtcNow - statsControl.ChangedAt).TotalSeconds);
					string text3 = (statsControl.Days.HasValue ? WarehouseLocalization.Format(language, "stats.days", statsControl.Days.Value) : WarehouseLocalization.Text(language, "common.auto"));
					string text4 = (statsControl.Page.HasValue ? WarehouseLocalization.Text(language, "stats.page_" + statsControl.Page.Value) : WarehouseLocalization.Text(language, "common.auto"));
					text2 = "<color=#ffb347>" + WarehouseLocalization.Text(language, "stats.manual") + "\n<color=white>" + text3 + " · " + text4 + "\n" + WarehouseLocalization.Format(language, "stats.auto_in", num15);
				}
				WriteBoardCell(dictionary, -2, text2);
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
		if (!cells.TryGetValue(cell, out var value))
		{
			return;
		}
		string text = value.ZDO.GetString(BoardRenderedTextKey, string.Empty);
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
			int num = list.FirstOrDefault((int x) => x >= 1 && x <= 365);
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

	internal static string DayLabel(int key, bool fullYear = false)
	{
		return (key % 100).ToString("00", CultureInfo.InvariantCulture) + "." + (key / 100 % 100).ToString("00", CultureInfo.InvariantCulture) + "." + (fullYear ? (key / 10000).ToString(CultureInfo.InvariantCulture) : (key / 10000 % 100).ToString("00", CultureInfo.InvariantCulture));
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
			value.ZDO.Set(BoardRenderedTextKey, PlainText(text).Trim());
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
		return !string.IsNullOrWhiteSpace(sign.ZDO.GetString(RequestWarehouseKey, string.Empty));
	}

	private static bool IsRememberedInfo(ServersideQoLZDO sign)
	{
		return !string.IsNullOrWhiteSpace(sign.ZDO.GetString(InfoWarehouseKey, string.Empty));
	}

	private static bool IsRememberedBoard(ServersideQoLZDO sign)
	{
		return !string.IsNullOrWhiteSpace(sign.ZDO.GetString(BoardWarehouseKey, string.Empty));
	}

	private static bool IsRememberedStats(ServersideQoLZDO sign)
	{
		return !string.IsNullOrWhiteSpace(sign.ZDO.GetString(StatsWarehouseKey, string.Empty));
	}

	private ServersideQoLZDO LinkedOrNearestContainer(ServersideQoLZDO sign, List<ServersideQoLZDO> containers, float range2)
	{
		float num = sign.ZDO.GetFloat(AutoChestXKey, float.NaN);
		float num2 = sign.ZDO.GetFloat(AutoChestYKey, float.NaN);
		float num3 = sign.ZDO.GetFloat(AutoChestZKey, float.NaN);
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
		if (TryLabelCategory(label, out var category))
		{
			if (category != WarehouseCategories.Category.Misc)
			{
				return InCategory(item, category);
			}
			return false;
		}
		if (!WarehouseText.LabelNamesItem(label, ItemAliases(item), (string alias) => AliasOwners.TryGetValue(alias, out var value2) && value2.Count > 1, out var needsSample))
		{
			return false;
		}
		if (needsSample)
		{
			return HasItem(InventoryFor(registry, chest), item);
		}
		return true;
	}

	private static bool TryLabelCategory(string label, out WarehouseCategories.Category category)
	{
		category = WarehouseCategories.Category.None;
		if (!string.IsNullOrEmpty(label) && label.IndexOf("\u001f", StringComparison.Ordinal) < 0)
		{
			return WarehouseCategories.TryParse(Normalize(label), out category);
		}
		return false;
	}

	private static bool InCategory(ItemData item, WarehouseCategories.Category category)
	{
		if (item?.m_shared != null)
		{
			return WarehouseCategories.Contains(category, item.m_shared.m_name, item.m_shared.m_itemType.ToString(), item.m_shared.m_food, item.m_shared.m_foodStamina);
		}
		return false;
	}

	private bool IsMiscChest(ServersideQoLZDO chest, Dictionary<ServersideQoLZDO, string> labels)
	{
		if (!_labelItemTokens.ContainsKey(chest) && labels.TryGetValue(chest, out var value) && TryLabelCategory(value, out var category))
		{
			return category == WarehouseCategories.Category.Misc;
		}
		return false;
	}

	private static string LabelForLog(string label)
	{
		return PlainText(label ?? string.Empty).Replace("\u001f", " + ").Trim();
	}

	private static bool ItemMatchesQuery(ItemData item, string query)
	{
		return Array.IndexOf(ItemAliases(item), Normalize(query)) >= 0;
	}

	private static string[] ItemAliases(ItemData item)
	{
		string text = ItemName(item);
		string text2 = item?.m_shared?.m_name ?? string.Empty;
		string key = text + "\u0001" + text2;
		if (AliasCache.TryGetValue(key, out var value))
		{
			return value;
		}
		string value2;
		string englishNormalized = ((text2.Length > 0 && EnglishNames.TryGetValue(text2, out value2)) ? value2 : null);
		string value3;
		string germanNormalized = ((text2.Length > 0 && GermanNames.TryGetValue(text2, out value3)) ? value3 : null);
		string[] array = WarehouseText.BuildAliases(text, text2, englishNormalized, germanNormalized);
		AliasCache[key] = array;
		return array;
	}

	private static void LoadGermanNames()
	{
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
			AliasCache.Clear();
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

	private static string ItemName(ItemData item)
	{
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
		return WarehouseText.Normalize(value);
	}

	private static readonly FieldInfo ObjectsByIdField = typeof(ZDOMan).GetField("m_objectsByID", BindingFlags.Instance | BindingFlags.NonPublic);

	private static WorldObjectIndex _worldIndex;

	private static ZDOMan _worldIndexOwner;

	private static Dictionary<ZDOID, ZDO> _worldObjects;

	private static bool _creationPatchTried;

	private static bool _followsCreations;

	private readonly List<ZDO> _worldSigns = new List<ZDO>();

	private void EnsureWorldIndex()
	{
		ZDOMan zdoMan = ZDOMan.instance;
		if (zdoMan == null || (_worldIndex != null && _worldIndexOwner == zdoMan))
		{
			return;
		}
		if (_worldIndexOwner != null)
		{
			_worldIndexOwner.m_onZDODestroyed -= OnWorldObjectDestroyed;
		}
		_worldObjects = ObjectsByIdField?.GetValue(zdoMan) as Dictionary<ZDOID, ZDO>;
		if (_worldObjects == null)
		{
			this.Logger.LogError((object)"ZDOMan.m_objectsByID not found: world-wide warehouse reads (topology, stock of other warehouses) are off.");
		}
		_worldIndex = new WorldObjectIndex(() => _worldObjects?.Values ?? (IEnumerable<ZDO>)Array.Empty<ZDO>(), IsLiveWorldObject, (ZDO zdo) => IsContainerPrefab(zdo.GetPrefab()), IsSignObject);
		_worldIndexOwner = zdoMan;
		zdoMan.m_onZDODestroyed += OnWorldObjectDestroyed;
		PatchWorldObjectCreation();
		_worldIndex.FollowsCreations = _followsCreations;
	}

	private void PatchWorldObjectCreation()
	{
		if (_creationPatchTried)
		{
			return;
		}
		_creationPatchTried = true;
		try
		{
			MethodInfo create = typeof(ZDOMan).GetMethod("CreateNewZDO", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[3]
			{
				typeof(ZDOID),
				typeof(Vector3),
				typeof(int)
			}, null);
			if (create == null)
			{
				this.Logger.LogError((object)"ZDOMan.CreateNewZDO(ZDOID, Vector3, int) not found: world-wide warehouse reads walk every object in the world.");
				return;
			}
			new HarmonyLib.Harmony(WarehousePlugin.Guid).Patch(create, postfix: new HarmonyLib.HarmonyMethod(typeof(WarehouseProcessor), nameof(OnWorldObjectCreated)));
			_followsCreations = true;
		}
		catch (Exception ex)
		{
			this.Logger.LogError((object)("Following created objects failed: world-wide warehouse reads walk every object in the world. " + ex));
		}
	}

	internal static void OnWorldObjectCreated(ZDO __result)
	{
		_worldIndex?.Created(__result);
	}

	private static void OnWorldObjectDestroyed(ZDO zdo)
	{
		_worldIndex?.Destroyed(zdo);
	}

	private static bool IsLiveWorldObject(ZDO zdo)
	{
		return _worldObjects != null && _worldObjects.TryGetValue(zdo.m_uid, out var value) && value == zdo;
	}

	private bool ReadWorldChestsAndSigns(List<ZDO> chests, List<PersistedSign> signs)
	{
		EnsureWorldIndex();
		if (_worldObjects == null)
		{
			return false;
		}
		_worldIndex.Read(chests, _worldSigns);
		foreach (ZDO worldSign in _worldSigns)
		{
			string text = worldSign.GetString(ZDOVars.s_text, string.Empty);
			if (!string.IsNullOrWhiteSpace(text))
			{
				signs.Add(new PersistedSign(worldSign, PlainText(text)));
			}
		}
		return true;
	}

	private static bool IsSignObject(ZDO zdo)
	{
		//IL_0040: Expected O, but got Unknown
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
		return WarehouseText.PlainText(value);
	}

	// Local patch 9: ServersideQoL ContainerSigns writes a chest's contents between these markers.
	internal static bool IsContainerSignsList(string raw)
	{
		return (raw ?? string.Empty).IndexOf("<i ls>", StringComparison.Ordinal) >= 0;
	}

	private const string ContainerSignsGuid = "ArgusMagnus.ServersideQoL.ContainerSigns";

	// The warehouse places its signs as ProcessorOwned; ServersideQoL ContainerSigns places its chest
	// signs as None. Only the warehouse's own signs are removed or replaced: ContainerSigns keeps a
	// list of its signs, and one removed by someone else makes removing its chest fail in
	// ContainerSigns ("not found in PlacedObjects"), which stops the game's handling of every other
	// object removed with it. A ContainerSigns sign used as a label is written on, never removed.
	private static bool PlacedByWarehouse(ServersideQoLZDO sign)
	{
		CreatorMarkers marker;
		return sign.IsModCreator(out marker) && marker == CreatorMarkers.ProcessorOwned;
	}

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
		if (((state != null) ? state.ZDO : null) == null)
		{
			return false;
		}
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
			AppendPerformance("SLOW", id, (awake ? "awake" : "sleeping") + " [" + _cycleDetail.ToString().Trim() + "]", 1, perfWindow.Skipped, elapsedMs, elapsedMs, _cycleTopologyMs, _cycleClassifyMs, _cycleDisplayMs, _cycleTransferMs, _cycleContainers, _cycleSigns, _cycleStores, _cycleInventoryReads, _cyclePeers, _cyclePingAverage, _cyclePingMaximum, _cycleLocalQualityMinimum, _cycleRemoteQualityMinimum, _cycleOutBytesPerSecond, _cycleInBytesPerSecond);
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

	private void CapturePeerMetrics()
	{
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
			Vector3 val = peer.RefPos - position;
			return val.sqrMagnitude <= distance2;
		}));
	}
}
