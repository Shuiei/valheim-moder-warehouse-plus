using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ServersideQoL;
using ServersideQoL.Processors;
using UnityEngine;
using Xunit;

namespace ModerWarehouse.Tests;

/// <summary>
/// A server's world for the warehouse to work in: chests (with real Valheim inventories), signs and
/// items, with the real WarehouseProcessor running over them. The game around it is stood in for by
/// <see cref="GameStubs"/>; ZNet and ZDOMan are the game's, made without the engine.
/// One world at a time (the game's singletons are static): its tests are in the "Game state"
/// collection.
/// </summary>
public sealed class TestWorld : IDisposable
{
    public static readonly int SignPrefab = StringExtensionMethods.GetStableHashCode("sign");

    private readonly Dictionary<int, string> _prefabNames = new() { [SignPrefab] = "sign" };

    private static readonly AccessTools.FieldRef<WarehouseProcessor, HashSet<ServersideQoLZDO>> Signs =
        AccessTools.FieldRefAccess<WarehouseProcessor, HashSet<ServersideQoLZDO>>("_signs");

    private readonly TestBepInEx _bepInEx = new();

    private readonly List<ServersideQoLZDO> _objects = [];

    private readonly HashSet<ServersideQoLZDO> _placedByMod = [];

    private readonly Dictionary<ServersideQoLZDO, Chest> _chests = [];

    private readonly ConditionalWeakTable<ServersideQoLZDO, ContainerState> _states = [];

    private readonly List<string> _log = [];

    private uint _nextId = 1;

    internal static TestWorld? Current { get; private set; }

    public TestWorld(Action<WarehouseConfig>? configure = null)
    {
        GameStubs.Install();
        Current = this;
        Now = DateTime.UtcNow;
        WarehouseClock.Source = () => Now;
        MakeGameSingletons();
        var logger = new ServersideQoL.Logger("Moder Warehouse Plus");
        var file = new ConfigFile(_bepInEx.Config(WarehousePlugin.Guid + ".cfg"), saveOnInit: false);
        // ServersideQoL's own settings first: the warehouse's are bound through them. Each is made the
        // current one as ServersideQoL does once its plugin starts (RaiseInitialized, which also
        // watches the file: not wanted here).
        MakeCurrent(Activator.CreateInstance(typeof(ServersideQoL.Config), file, logger)!);
        Config = new WarehouseConfig(file, logger);
        MakeCurrent(Config);
        configure?.Invoke(Config);
        Items = new TestItems();
        Processor = new WarehouseProcessor();
        AccessTools.PropertySetter(typeof(Processor), "Logger").Invoke(Processor, [logger]);
        AccessTools.Method(typeof(WarehouseProcessor), "Initialize").Invoke(Processor, []);
    }

    public WarehouseConfig Config { get; }

    private static void MakeCurrent(object config) =>
        AccessTools.PropertySetter(typeof(ConfigBase<>).MakeGenericType(config.GetType()), "Instance").Invoke(null, [config]);

    public TestItems Items { get; }

    public WarehouseProcessor Processor { get; }

    public IReadOnlyList<string> Log => _log;

    /// <summary>The warehouse's time (WarehouseClock): it moves only with <see cref="Advance"/>.</summary>
    public DateTime Now { get; private set; }

    public void Advance(TimeSpan time) => Now += time;

    /// <summary>Whether ServersideQoL ContainerSigns is installed beside the warehouse.</summary>
    public bool ContainerSignsInstalled { get; set; }

    /// <summary>The text a chest keeps (ContainerSigns shows it on the chest's signs).</summary>
    public string ChestText(Chest chest) => chest.Zdo.Vars.GetText("");

    public IEnumerable<Chest> Chests => _chests.Values;

    public IEnumerable<ServersideQoLZDO> AllSigns => _objects.Where(o => o.ZDO.GetPrefab() == SignPrefab);

    /// <summary>A chest at x, z: by default a wooden one (8 x 4 slots, like the game's).</summary>
    public Chest Chest(float x, float z, int width = 8, int height = 4, string prefab = "piece_chest_wood")
    {
        int hash = StringExtensionMethods.GetStableHashCode(prefab);
        _prefabNames[hash] = prefab;
        ServersideQoLZDO zdo = NewObject(hash, new Vector3(x, 0, z), Quaternion.identity);
        var chest = new Chest(zdo, new Inventory("chest", null, width, height));
        _chests[zdo] = chest;
        return chest;
    }

    /// <summary>A player's sign at x, z with its text, as the warehouse finds it.</summary>
    public ServersideQoLZDO Sign(string text, float x, float z)
    {
        ServersideQoLZDO sign = NewObject(SignPrefab, new Vector3(x, 0, z), Quaternion.identity);
        sign.Vars.SetText(text);
        Signs(Processor).Add(sign);
        return sign;
    }

    /// <summary>A player writes on a sign: the warehouse handles it as ServersideQoL would tell it.</summary>
    public void Write(ServersideQoLZDO sign, string text)
    {
        sign.Vars.SetText(text);
        Process(sign);
    }

    /// <summary>ServersideQoL hands a sign to the warehouse (on a change, or once in a while).</summary>
    public void Process(ServersideQoLZDO sign) =>
        AccessTools.DeclaredMethod(typeof(WarehouseProcessor), "Process", [typeof(ServersideQoLZDO), typeof(IReadOnlyList<Peer>), typeof(ProcessorPrefabInfo<Sign>)])
            .Invoke(Processor, [sign, Array.Empty<Peer>(), null]);

    /// <summary>One warehouse cycle, now: sorting, requests, labels, boards.</summary>
    public void Run(string warehouse) =>
        AccessTools.Method(typeof(WarehouseProcessor), "ProcessWarehouse").Invoke(Processor, [warehouse, Array.Empty<Peer>(), true]);

    public string TextOf(ServersideQoLZDO sign) => sign.Vars.GetText("");

    public string PlainTextOf(ServersideQoLZDO sign) => WarehouseProcessor.PlainText(TextOf(sign));

    /// <summary>The help sign the warehouse placed for an IN sign, if any.</summary>
    public ServersideQoLZDO? HelpSignOf(ServersideQoLZDO input)
    {
        string group = input.ZDO.GetString("ModerWarehouse.HelpGroup", string.Empty);
        return AllSigns.FirstOrDefault(s => s.ZDO.GetBool("ModerWarehouse.HelpChild", false) && group.Length > 0
            && s.ZDO.GetString("ModerWarehouse.HelpGroup", string.Empty) == group);
    }

    /// <summary>Every sign and what it says, for a failing test's message.</summary>
    public string Describe() => string.Join("\n", AllSigns.Select(s =>
    {
        Vector3 p = s.ZDO.GetPosition();
        return $"sign at {p.x:0.##},{p.z:0.##}{(IsPlacedByMod(s) ? " (placed by the warehouse)" : "")}: {PlainTextOf(s).Replace("\n", " | ", StringComparison.Ordinal)}";
    }));

    // ---- What GameStubs hands over.

    internal void AddLog(LogLevel level, string message)
    {
        lock (_log)
        {
            _log.Add($"{level}: {message}");
        }
    }

    internal ContainerState? StateOf(ServersideQoLZDO zdo)
    {
        if (!_chests.ContainsKey(zdo))
        {
            return null;
        }
        return _states.GetValue(zdo, key =>
        {
            Type impl = AccessTools.Inner(typeof(ContainerRegistryProcessor), "ContainerStateImpl");
            var state = (ContainerState)RuntimeHelpers.GetUninitializedObject(impl);
            AccessTools.Field(impl, "_zdo").SetValue(state, key);
            return state;
        });
    }

    internal ContainerState.IInventory? InventoryOf(ServersideQoLZDO zdo) => _chests.TryGetValue(zdo, out var chest) ? chest : null;

    internal ServersideQoLZDO Place(Vector3 position, int prefab, Quaternion rotation)
    {
        ServersideQoLZDO zdo = NewObject(prefab, position, rotation);
        _placedByMod.Add(zdo);
        return zdo;
    }

    internal void Remove(ServersideQoLZDO zdo)
    {
        _objects.Remove(zdo);
        _chests.Remove(zdo);
        ObjectsById.Remove(zdo.ZDO.m_uid);
        Signs(Processor).Remove(zdo);
        AccessTools.Method(typeof(ServersideQoLZDO), "OnDestroyed")?.Invoke(zdo, []);
    }

    internal bool IsPlacedByMod(ServersideQoLZDO zdo) => _placedByMod.Contains(zdo);

    private readonly Dictionary<(ServersideQoLZDO, string), bool> _pieceFields = [];

    internal void SetPieceField(ServersideQoLZDO zdo, string field, bool value) => _pieceFields[(zdo, field)] = value;

    /// <summary>Whether players can remove the object (the warehouse sets it on the signs it uses).</summary>
    public bool IsRemovable(ServersideQoLZDO zdo) => _pieceFields.TryGetValue((zdo, "m_canBeRemoved"), out bool value) && value;

    internal bool IsChestPrefab(int prefab) => prefab != SignPrefab && _prefabNames.ContainsKey(prefab);

    internal string PrefabName(ServersideQoLZDO zdo) => _prefabNames.TryGetValue(zdo.ZDO.GetPrefab(), out var name) ? name : string.Empty;

    internal List<ZDO> ObjectsNear(Vector3 position) =>
        _objects.Where(o => (o.ZDO.GetPosition() - position).sqrMagnitude <= 64f * 64f).Select(o => o.ZDO).ToList();

    public void Dispose()
    {
        WarehouseClock.Source = () => DateTime.UtcNow;
        Current = null;
        _bepInEx.Dispose();
    }

    // ---- The game's objects, without the engine.

    private static void MakeGameSingletons()
    {
        var net = (ZNet)RuntimeHelpers.GetUninitializedObject(typeof(ZNet));
        AccessTools.Field(typeof(ZNet), "m_isServer").SetValue(net, true);
        AccessTools.Field(typeof(ZNet), "m_instance").SetValue(null, net);
        var zdoMan = (ZDOMan)RuntimeHelpers.GetUninitializedObject(typeof(ZDOMan));
        // The world's objects by id (the warehouse reads every IN sign and chest from it).
        AccessTools.Field(typeof(ZDOMan), "m_objectsByID").SetValue(zdoMan, new Dictionary<ZDOID, ZDO>());
        AccessTools.Field(typeof(ZDOMan), "s_instance").SetValue(null, zdoMan);
        ZDOExtraData.Init();
    }

    private static Dictionary<ZDOID, ZDO> ObjectsById =>
        (Dictionary<ZDOID, ZDO>)AccessTools.Field(typeof(ZDOMan), "m_objectsByID").GetValue(ZDOMan.instance)!;

    private ServersideQoLZDO NewObject(int prefab, Vector3 position, Quaternion rotation)
    {
        var zdo = new ZDO();
        zdo.m_uid = new ZDOID(1, _nextId++);
        AccessTools.Field(typeof(ZDO), "m_position").SetValue(zdo, position);
        zdo.Init();
        zdo.SetPrefab(prefab);
        ObjectsById[zdo.m_uid] = zdo;
        // Made with the ZDO, as in the game (ServersideQoL's preloader patcher: GamePatcher).
        var wrapped = (ServersideQoLZDO)AccessTools.PropertyGetter(typeof(ZDO), "ServersideQoLZDO").Invoke(zdo, [])!;
        _objects.Add(wrapped);
        return wrapped;
    }
}

/// <summary>A chest in the world and its inventory (ServersideQoL's view of it: IInventory).</summary>
public sealed class Chest(ServersideQoLZDO zdo, Inventory inventory) : ContainerState.IInventory
{
    public ServersideQoLZDO Zdo { get; } = zdo;

    public Inventory Inventory { get; } = inventory;

    public List<ItemDrop.ItemData> Items => Inventory.GetAllItems();

    public int Saves { get; private set; }

    public void Save() => Saves++;

    public void Add(ItemDrop.ItemData item) => Assert.True(Inventory.AddItem(item), "The chest is full.");

    public int Count(ItemDrop.ItemData.SharedData item) => Items.Where(i => i.m_shared == item).Sum(i => i.m_stack);

    public int Count(string token) => Items.Where(i => i.m_shared.m_name == token).Sum(i => i.m_stack);
}
