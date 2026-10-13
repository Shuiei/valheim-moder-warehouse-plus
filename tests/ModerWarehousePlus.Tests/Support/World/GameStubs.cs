using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using ServersideQoL;
using ServersideQoL.Processors;
using UnityEngine;

namespace ModerWarehouse.Tests;

/// <summary>
/// What stands in for the game in the warehouse tests: Harmony patches, installed once, on the code
/// that would reach Unity's engine (which only the game has) or the live world. Each patch hands the
/// call to the current <see cref="TestWorld"/>; the warehouse's own code runs unpatched.
/// Unity's engine functions themselves cannot be patched (they have no code to patch), so the
/// patches are on the code calling them.
/// </summary>
internal static class GameStubs
{
    private static readonly object Gate = new();

    private static bool _installed;

    public static void Install()
    {
        lock (Gate)
        {
            if (_installed)
            {
                return;
            }
            var harmony = new Harmony("ModerWarehousePlus.Tests");
            void Replace(MethodBase? original, string stub)
            {
                if (original == null)
                {
                    throw new InvalidOperationException($"Nothing to patch for {stub}: the patched code has changed.");
                }
                harmony.Patch(original, prefix: new HarmonyMethod(typeof(GameStubs), stub));
            }
            Type registry = typeof(ContainerRegistryProcessor);
            Type stateImpl = AccessTools.Inner(registry, "ContainerStateImpl");
            Type warehouse = typeof(WarehouseProcessor);

            // Unity objects: their type's static initializer asks the engine for an offset (only the
            // engine uses it). Skipped before any is made, as a failed one breaks the type for good.
            Replace(typeof(UnityEngine.Object).TypeInitializer, nameof(Skip));
            // Unity: the game's clock (logging) and rotations (placing signs).
            Replace(AccessTools.Method(typeof(ServersideQoL.Logger), "Log"), nameof(Log));
            Replace(AccessTools.Method(typeof(Quaternion), "Internal_FromEulerRad"), nameof(FromEulerRad));
            Replace(AccessTools.Method(typeof(Quaternion), "Internal_ToEulerRad"), nameof(ToEulerRad));
            // The game's translations, which it reads from Unity's resources (the warehouse uses its own).
            Replace(AccessTools.Method(typeof(Localization), "Initialize"), nameof(InitializeLocalization));
            Replace(AccessTools.Method(typeof(Localization), "GetSelectedLanguage"), nameof(SelectedLanguage));
            Replace(AccessTools.Method(typeof(Localization), "GetLanguages"), nameof(Languages));
            Replace(AccessTools.Method(typeof(Localization), "SetLanguage"), nameof(Skip));
            Replace(AccessTools.Method(typeof(Localization), "Localize", [typeof(string)]), nameof(Localize));
            // Achievements (adding a cheated item checks them, with Unity's frame count): a modded game
            // always counts as cheated (Game.isModded).
            Replace(AccessTools.Method(typeof(Achievements), "IsCheatedAtAll"), nameof(True));
            // The network: every object's data changes are sent to clients.
            Replace(AccessTools.Method(typeof(ZDOMan), "SetDirtySector"), nameof(Skip));
            // ServersideQoL: chests and their inventories, objects placed, destroyed and owned.
            Replace(AccessTools.Method(registry, "GetState", [typeof(ServersideQoLZDO)]), nameof(GetState));
            Replace(AccessTools.Method(registry, "GetContainers"), nameof(GetContainers));
            Replace(AccessTools.Method(stateImpl, "GetInventory"), nameof(GetInventory));
            Replace(AccessTools.Method(typeof(Processor), "PlacePiece", [typeof(Vector3), typeof(int), typeof(Quaternion), typeof(Processor.CreatorMarkers)]), nameof(PlacePiece));
            Replace(AccessTools.Method(typeof(ServersideQoLZDO), "Destroy"), nameof(Destroy));
            // Component fields set on an object (ServersideQoL keeps them in the object's data, read
            // against its prefab): the warehouse makes the signs it places removable by players. The
            // world records it (TestWorld.IsRemovable).
            Replace(AccessTools.Method(typeof(ServersideQoLZDO), "Fields").MakeGenericMethod(typeof(Piece)), nameof(PieceFields));
            Replace(AccessTools.Method(typeof(ServersideQoLZDO.ComponentFieldAccessor<Piece>), "Set",
                [typeof(Func<System.Linq.Expressions.Expression<Func<Piece, bool>>>), typeof(bool), typeof(string), typeof(int)]), nameof(SetPieceField));
            Replace(AccessTools.Method(typeof(ServersideQoLZDO), "ClaimOwnership"), nameof(Skip));
            // The server owns every object here: what it writes needs no ownership check.
            Replace(AccessTools.Method(typeof(ServersideQoLZDO.ZDOVars), "ValidateOwnership"), nameof(Skip));
            Replace(AccessTools.Method(typeof(ServersideQoLZDO), "IsOwnerOrUnassigned"), nameof(True));
            Replace(AccessTools.Method(typeof(ServersideQoLZDO), "IsModCreator", [typeof(Processor.CreatorMarkers).MakeByRefType()]), nameof(IsModCreator));
            // The warehouse's own reads of the world: chests, signs, nearby objects, players, item names.
            Replace(AccessTools.Method(warehouse, "AllContainers"), nameof(AllContainers));
            Replace(AccessTools.Method(warehouse, "IsContainerPrefab"), nameof(IsContainerPrefab));
            Replace(AccessTools.Method(warehouse, "IsPrivateContainer"), nameof(IsPrivateContainer));
            Replace(AccessTools.Method(warehouse, "ReadPersistedContainer"), nameof(ReadPersistedContainer));
            Replace(AccessTools.Method(AccessTools.Inner(warehouse, "PersistedContainerSpatialLookup"), "IsContainer"), nameof(IsContainerObject));
            Replace(AccessTools.Method(warehouse, "IsSignObject"), nameof(IsSignObject));
            Replace(AccessTools.Method(warehouse, "WorldObjectsNear"), nameof(WorldObjectsNear));
            Replace(AccessTools.Method(warehouse, "CapturePeerMetrics"), nameof(Skip));
            Replace(AccessTools.Method(warehouse, "LoadGermanNames"), nameof(Skip));
            // Unity objects' names (kept by the engine): the test world names its recipes, items and pieces.
            Replace(AccessTools.PropertyGetter(typeof(UnityEngine.Object), "name"), nameof(ObjectName));
            // The building pieces of the hammer, hoe and cultivator (read through Unity's GameObject).
            Replace(AccessTools.Method(warehouse, "BuildPieces"), nameof(BuildPieces));
            // Whether ServersideQoL ContainerSigns is installed (BepInEx's list of loaded plugins).
            Replace(AccessTools.PropertyGetter(warehouse, "ContainerSignsLoaded"), nameof(ContainerSignsLoaded));
            _installed = true;
        }
    }

    private static TestWorld World => TestWorld.Current ?? throw new InvalidOperationException("No TestWorld is open.");

    private static bool Skip() => false;

    private static bool True(ref bool __result)
    {
        __result = true;
        return false;
    }

    private static bool InitializeLocalization()
    {
        var field = AccessTools.Field(typeof(Localization), "m_instance");
        if (field.GetValue(null) == null)
        {
            field.SetValue(null, System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Localization)));
        }
        return false;
    }

    private static bool SelectedLanguage(ref string __result)
    {
        __result = "English";
        return false;
    }

    private static bool Languages(ref List<string> __result)
    {
        __result = ["English", "German"];
        return false;
    }

    private static bool Localize(string text, ref string __result)
    {
        __result = text;
        return false;
    }

    private static bool Log(LogLevel level, object data)
    {
        TestWorld.Current?.AddLog(level, data?.ToString() ?? string.Empty);
        return false;
    }

    private static bool FromEulerRad(Vector3 euler, ref Quaternion __result)
    {
        // Unity's order: Z, then X, then Y.
        float cx = MathF.Cos(euler.x / 2), sx = MathF.Sin(euler.x / 2);
        float cy = MathF.Cos(euler.y / 2), sy = MathF.Sin(euler.y / 2);
        float cz = MathF.Cos(euler.z / 2), sz = MathF.Sin(euler.z / 2);
        __result = new Quaternion(
            sx * cy * cz + cx * sy * sz,
            cx * sy * cz - sx * cy * sz,
            cx * cy * sz - sx * sy * cz,
            cx * cy * cz + sx * sy * sz);
        return false;
    }

    private static bool ToEulerRad(ref Vector3 __result)
    {
        __result = Vector3.zero;
        return false;
    }

    private static bool GetState(ServersideQoLZDO zdo, ref ContainerState? __result)
    {
        __result = World.StateOf(zdo);
        return false;
    }

    private static bool GetContainers(ref object? __result)
    {
        __result = null;
        return false;
    }

    private static bool GetInventory(ContainerState __instance, ref ContainerState.IInventory? __result)
    {
        __result = World.InventoryOf(__instance.ZDO);
        return false;
    }

    private static bool PlacePiece(Vector3 pos, int prefab, Quaternion rot, Processor.CreatorMarkers marker, ref ServersideQoLZDO __result)
    {
        __result = World.Place(pos, prefab, rot, marker);
        return false;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, ServersideQoLZDO> FieldOwners = [];

    private static bool PieceFields(ServersideQoLZDO __instance, ref ServersideQoLZDO.ComponentFieldAccessor<Piece> __result)
    {
        __result = (ServersideQoLZDO.ComponentFieldAccessor<Piece>)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ServersideQoLZDO.ComponentFieldAccessor<Piece>));
        FieldOwners.AddOrUpdate(__result, __instance);
        return false;
    }

    private static bool SetPieceField(ServersideQoLZDO.ComponentFieldAccessor<Piece> __instance,
        Func<System.Linq.Expressions.Expression<Func<Piece, bool>>> fieldExpressionFactory, bool value, ref ServersideQoLZDO.ComponentFieldAccessor<Piece> __result)
    {
        if (FieldOwners.TryGetValue(__instance, out var zdo))
        {
            World.SetPieceField(zdo, ((System.Linq.Expressions.MemberExpression)fieldExpressionFactory().Body).Member.Name, value);
        }
        __result = __instance;
        return false;
    }

    private static bool Destroy(ServersideQoLZDO __instance)
    {
        World.Remove(__instance);
        return false;
    }

    private static bool IsModCreator(ServersideQoLZDO __instance, ref Processor.CreatorMarkers marker, ref bool __result)
    {
        __result = World.IsPlacedByMod(__instance);
        marker = World.CreatorMarker(__instance);
        return false;
    }

    private static bool AllContainers(ref List<ServersideQoLZDO> __result)
    {
        __result = World.Chests.Select(c => c.Zdo).ToList();
        return false;
    }

    private static bool ObjectName(UnityEngine.Object __instance, ref string __result)
    {
        __result = TestWorld.Current?.NameOf(__instance) ?? string.Empty;
        return false;
    }

    private static bool BuildPieces(ref IEnumerable<Piece> __result)
    {
        __result = World.Pieces;
        return false;
    }

    private static bool ContainerSignsLoaded(ref bool __result)
    {
        __result = World.ContainerSignsInstalled;
        return false;
    }

    private static bool IsContainerPrefab(int prefab, ref bool __result)
    {
        __result = World.IsChestPrefab(prefab);
        return false;
    }

    // A chest's saved items, decoded (the game decodes them with its item prefabs, from Unity): the
    // world's chest's own items. Private chests are left out, as the warehouse does.
    private static bool ReadPersistedContainer(ZDO zdo, ref object? __result)
    {
        List<ItemDrop.ItemData>? items = World.SavedItemsOf(zdo);
        bool isPrivate = WarehouseContainerPolicy.IsPrivatePrefab(World.PrefabName(zdo));
        __result = items == null || isPrivate ? null
            : Activator.CreateInstance(AccessTools.Inner(typeof(WarehouseProcessor), "PersistedContainer"), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, [zdo, items], null);
        return false;
    }

    private static bool IsContainerObject(ZDO candidate, ref bool __result)
    {
        __result = World.IsChestPrefab(candidate.GetPrefab());
        return false;
    }

    // The game's prefab name (ServersideQoL's prefab registry), judged by the warehouse's own rule.
    private static bool IsPrivateContainer(ServersideQoLZDO chest, ref bool __result)
    {
        __result = WarehouseContainerPolicy.IsPrivatePrefab(World.PrefabName(chest));
        return false;
    }

    private static bool IsSignObject(ZDO zdo, ref bool __result)
    {
        __result = zdo.GetPrefab() == TestWorld.SignPrefab;
        return false;
    }

    private static bool WorldObjectsNear(Vector3 position, ref List<ZDO> __result)
    {
        __result = World.ObjectsNear(position);
        return false;
    }
}
