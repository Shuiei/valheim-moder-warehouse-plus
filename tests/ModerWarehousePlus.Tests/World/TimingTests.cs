using ServersideQoL;
using Xunit;

namespace ModerWarehouse.Tests;

/// <summary>
/// What the warehouse does over time: request results and target stock on a REQ sign, a stuck IN
/// chest, and renaming a warehouse.
/// </summary>
[Collection("Game state")]
public sealed class TimingTests : IDisposable
{
    private readonly TestWorld _world = new();

    private readonly Chest _input;

    private readonly ServersideQoLZDO _inSign;

    private readonly Chest _output;

    private readonly Chest _wood;

    private readonly ServersideQoLZDO _request;

    public TimingTests()
    {
        _input = _world.Chest(0, 0);
        _inSign = _world.Sign("[WH:main:IN]", 0, 1);
        _world.Process(_inSign);
        _output = _world.Chest(2, 0);
        _world.Sign("[WH:main:OUT]", 2, 1);
        _wood = _world.Chest(5, 0);
        _world.Sign("Wood", 5, 0.5f);
        _wood.Add(TestItems.Make(_world.Items.Wood, 40));
        _request = _world.Sign("[WH:main:REQ]", 4, 1);
        _world.Process(_request);
        _world.Run("main");
    }

    public void Dispose() => _world.Dispose();

    private string Request => _world.PlainTextOf(_request);

    [Fact]
    public void TheRequestSignPromptsWithAnExample() =>
        Assert.Equal("REQ\nWood 100\nTARGET Wood 1000", Request);

    [Fact]
    public void TargetStockShowsWhatIsMissing()
    {
        _world.Write(_request, "SOLL Wood 100, Stone 10");
        _world.Run("main");

        Assert.Equal("NEEDED\nStone 0/10\nWood 40/100", Request);
    }

    [Fact]
    public void AResultShowsForEightSecondsThenTheTargetsReturn()
    {
        _world.Write(_request, "SOLL Wood 100");
        _world.Run("main");
        _world.Write(_request, "Wood 5");
        Assert.Equal("DONE Wood x5", Request);

        _world.Advance(TimeSpan.FromSeconds(7));
        _world.Process(_request);
        Assert.Equal("DONE Wood x5", Request);

        _world.Advance(TimeSpan.FromSeconds(2));
        _world.Process(_request);
        _world.Run("main");
        Assert.Equal("NEEDED\nWood 35/100", Request);
        Assert.Equal(5, _output.Count(_world.Items.Wood));
    }

    [Fact]
    public void AResultReturnsToThePromptWhenThereAreNoTargets()
    {
        _world.Write(_request, "Wood 5");
        _world.Advance(TimeSpan.FromSeconds(9));
        _world.Process(_request);

        Assert.Equal("REQ\nWood 100\nTARGET Wood 1000", Request);
    }

    [Fact]
    public void AnInChestLeftOpenByAPlayerWhoLeftIsFreedAfterHalfAMinute()
    {
        _input.Add(TestItems.Make(_world.Items.Wood, 3));
        _input.Zdo.Vars.SetInUse(true);
        _input.Zdo.ZDO.SetOwnerInternal(77);

        _world.Run("main");
        Assert.Equal(3, _input.Count(_world.Items.Wood));

        _world.Advance(TimeSpan.FromSeconds(31));
        _world.Run("main");

        Assert.Equal(0, _input.Count(_world.Items.Wood));
        Assert.False(_input.Zdo.Vars.GetInUse(false));
        Assert.Contains(_world.Log, l => l.Contains("released stale IN chest lock after 31 s; its owner is not connected.", StringComparison.Ordinal));
    }

    [Fact]
    public void AnInChestOpenByAPlayerStandingAtItStaysLockedUntilTheHardLimit()
    {
        Peer player = _world.Player(0, 1, id: 77);
        _input.Add(TestItems.Make(_world.Items.Wood, 3));
        _input.Zdo.Vars.SetInUse(true);
        _input.Zdo.ZDO.SetOwnerInternal(player.ZNetPeer.m_uid);
        _world.Run("main");

        _world.Advance(TimeSpan.FromSeconds(60));
        _world.Run("main");
        Assert.Equal(3, _input.Count(_world.Items.Wood));

        _world.Advance(TimeSpan.FromSeconds(31));
        _world.Run("main");
        Assert.Equal(0, _input.Count(_world.Items.Wood));
        Assert.Contains(_world.Log, l => l.Contains("hard limit 90 s reached", StringComparison.Ordinal));
    }

    [Fact]
    public void AnInChestOpenByAPlayerWhoWalkedAwayIsFreed()
    {
        Peer player = _world.Player(0, 1, id: 77);
        _input.Zdo.Vars.SetInUse(true);
        _input.Zdo.ZDO.SetOwnerInternal(player.ZNetPeer.m_uid);
        _world.Run("main");

        _world.Move(player, 40, 0);
        _world.Advance(TimeSpan.FromSeconds(31));
        _world.Run("main");

        Assert.False(_input.Zdo.Vars.GetInUse(false));
        Assert.Contains(_world.Log, l => l.Contains("its owner is more than 4 m away", StringComparison.Ordinal));
    }

    [Fact]
    public void RenamingAWarehouseTakesItsSignsAlong()
    {
        ServersideQoLZDO output = _world.AllSigns.First(s => _world.PlainTextOf(s) == "[WH:main:OUT]");

        _world.Write(_inSign, "[WH:depot:IN]");
        _world.Run("depot");

        Assert.Equal("[WH:depot:OUT]", _world.PlainTextOf(output));
        Assert.Equal("depot", _request.ZDO.GetString("ModerWarehouse.RequestWarehouse", string.Empty));
        Assert.Contains(_world.Log, l => l.StartsWith("Info: Warehouse renamed 'main' -> 'depot'", StringComparison.Ordinal));

        _world.Write(_request, "Wood 5");
        Assert.Equal(5, _output.Count(_world.Items.Wood));
    }
}
