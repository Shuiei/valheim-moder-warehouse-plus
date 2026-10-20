using HarmonyLib;
using ServersideQoL;
using UnityEngine;
using Xunit;

namespace ModerWarehouse.Tests;

/// <summary>
/// The world's chests and signs the warehouse reads world-wide (other warehouses, their stock): it
/// follows the game's object creation and removal instead of walking every object in the world.
/// </summary>
[Collection("Game state")]
public sealed class WorldObjectsTests : IDisposable
{
    private readonly TestWorld _world = new();

    public void Dispose() => _world.Dispose();

    private ServersideQoLZDO Input(string marker, float x)
    {
        _world.Chest(x, 0);
        ServersideQoLZDO sign = _world.Sign(marker, x, 1);
        _world.Process(sign);
        return sign;
    }

    [Fact]
    public void TheWarehouseFollowsTheGamesObjectCreation()
    {
        var create = AccessTools.Method(typeof(ZDOMan), "CreateNewZDO", [typeof(ZDOID), typeof(Vector3), typeof(int)]);

        Patches? patches = Harmony.GetPatchInfo(create);

        Assert.Contains(patches?.Postfixes ?? [], p => p.owner == WarehousePlugin.Guid);
    }

    [Fact]
    public void ARemovedWarehouseNoLongerHoldsItsName()
    {
        ServersideQoLZDO first = Input("[WH:main:IN]", 0);
        _world.Run("main");
        Chest firstChest = _world.Chests.Single();
        _world.Remove(first);
        _world.Remove(firstChest.Zdo);

        Input("[WH:main:IN]", 300);
        _world.Run("main");

        Assert.DoesNotContain(_world.Log, l => l.Contains("rejected", StringComparison.Ordinal));
    }
}
