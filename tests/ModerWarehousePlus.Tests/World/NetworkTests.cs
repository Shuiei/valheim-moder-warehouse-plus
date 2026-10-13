using ServersideQoL;
using Xunit;

namespace ModerWarehouse.Tests;

/// <summary>
/// Several warehouses in one world, and signs ServersideQoL ContainerSigns recreates: a request the
/// warehouse cannot fill looks at the others (read-only); a REQ sign on a chest keeps its link when
/// its sign is replaced.
/// </summary>
[Collection("Game state")]
public sealed class NetworkTests : IDisposable
{
    private readonly TestWorld _world = new();

    private readonly Chest _output;

    private readonly Chest _depotAmber;

    public NetworkTests()
    {
        _world.Chest(0, 0);
        _world.Process(_world.Sign("[WH:main:IN]", 0, 1));
        _output = _world.Chest(2, 0);
        _world.Sign("[WH:main:OUT]", 2, 1);
        Chest wood = _world.Chest(5, 0);
        _world.Sign("Wood", 5, 0.5f);
        wood.Add(TestItems.Make(_world.Items.Wood, 40));
        _world.Chest(300, 0);
        _world.Process(_world.Sign("[WH:depot:IN]", 300, 1));
        _depotAmber = _world.Chest(305, 0);
        _world.Sign("Amber", 305, 0.5f);
        _depotAmber.Add(TestItems.Make(_world.Items.Amber, 9));
        _world.Run("main");
        _world.Run("depot");
    }

    public void Dispose() => _world.Dispose();

    private ServersideQoLZDO Request(float x, float z)
    {
        ServersideQoLZDO request = _world.Sign("[WH:main:REQ]", x, z);
        _world.Process(request);
        return request;
    }

    [Fact]
    public void ARequestTheWarehouseCannotFillShowsWhatAnotherOneHas()
    {
        ServersideQoLZDO request = Request(4, 1);

        _world.Write(request, "Amber 5");

        Assert.Equal("NOT FOUND Amber\nDEPOT 9 LIVE", string.Join("\n", _world.PlainTextOf(request).Split('\n').Take(2)));
        Assert.Empty(_output.Items);
        Assert.Equal(9, _depotAmber.Count(_world.Items.Amber));
    }

    [Fact]
    public void AMisspelledRequestPointsToTheBoardsSuggestions()
    {
        ServersideQoLZDO request = Request(4, 1);

        _world.Write(request, "Wod 5");

        Assert.Equal("NOT FOUND Wod\nSEARCH Wod\n1 HIT(S) ON MAIN BOARD", _world.PlainTextOf(request));
        Assert.Empty(_output.Items);
    }

    [Fact]
    public void ARequestSignOnAChestKeepsItsLinkWhenItsSignIsRecreated()
    {
        // A ContainerSigns sign on the OUT chest, used as REQ sign: ContainerSigns keeps the chest's
        // text and recreates the sign from it at each server start.
        ServersideQoLZDO request = Request(2, 0.5f);
        _world.Write(request, "SOLL Wood 100");
        _world.Run("main");
        string text = _world.TextOf(request);
        _output.Zdo.Vars.SetText(text);

        _world.Remove(request);
        ServersideQoLZDO recreated = _world.Sign(text, 2, 0.5f);
        _world.Process(recreated);
        _world.Run("main");

        Assert.Equal("main", recreated.ZDO.GetString("ModerWarehouse.RequestWarehouse", string.Empty));
        Assert.Equal("NEEDED\nWood 40/100", _world.PlainTextOf(recreated));
        _world.Write(recreated, "Wood 3");
        Assert.Equal(3, _output.Count(_world.Items.Wood));
    }
}
