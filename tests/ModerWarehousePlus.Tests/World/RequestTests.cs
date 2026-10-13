using Xunit;

namespace ModerWarehouse.Tests;

/// <summary>
/// A REQ sign takes items out of the warehouse into OUT: what a player writes on it and what the
/// warehouse does and writes back (whole warehouse cycles in a TestWorld).
/// </summary>
[Collection("Game state")]
public sealed class RequestTests : IDisposable
{
    private readonly TestWorld _world = new();

    private readonly Chest _output;

    private readonly Chest _wood;

    private readonly Chest _stone;

    private readonly ServersideQoL.ServersideQoLZDO _request;

    public RequestTests()
    {
        _world.Chest(0, 0);
        _world.Sign("[WH:main:IN]", 0, 1);
        _output = _world.Chest(2, 0);
        _world.Sign("[WH:main:OUT]", 2, 1);
        _wood = Labelled("Wood", 5);
        _stone = Labelled("Stone", 7);
        _wood.Add(TestItems.Make(Items.Wood, 50));
        _wood.Add(TestItems.Make(Items.Wood, 50));
        _stone.Add(TestItems.Make(Items.Stone, 20));
        _request = _world.Sign("[WH:main:REQ]", 3, 1);
        _world.Process(_request);
    }

    public void Dispose() => _world.Dispose();

    private TestItems Items => _world.Items;

    private Chest Labelled(string label, float x)
    {
        Chest chest = _world.Chest(x, 0);
        _world.Sign(label, x, 0.5f);
        return chest;
    }

    [Fact]
    public void AnInitialisedRequestSignShowsItsPrompt() =>
        Assert.StartsWith("REQ", _world.PlainTextOf(_request), StringComparison.Ordinal);

    [Fact]
    public void ARequestPutsTheItemsIntoOut()
    {
        _world.Write(_request, "Wood 30");

        Assert.Equal(30, _output.Count(Items.Wood));
        Assert.Equal(70, _wood.Count(Items.Wood));
        Assert.StartsWith("DONE", _world.PlainTextOf(_request), StringComparison.Ordinal);
    }

    [Fact]
    public void SeveralItemsCanBeAskedForAtOnce()
    {
        _world.Write(_request, "Wood 5, Stone 3");

        Assert.Equal(5, _output.Count(Items.Wood));
        Assert.Equal(3, _output.Count(Items.Stone));
    }

    [Fact]
    public void GermanAndPrefabNamesWorkToo()
    {
        _world.Write(_request, "Holz 4, Stone 2");

        Assert.Equal(4, _output.Count(Items.Wood));
        Assert.Equal(2, _output.Count(Items.Stone));
    }

    [Fact]
    public void WhatTheWarehouseLacksIsDeliveredAsFarAsItGoes()
    {
        _world.Write(_request, "Stone 50");

        Assert.Equal(20, _output.Count(Items.Stone));
        Assert.Equal(0, _stone.Count(Items.Stone));
    }

    [Fact]
    public void AnItemTheWarehouseDoesNotHaveIsReported()
    {
        _world.Write(_request, "Resin 5");

        Assert.Empty(_output.Items);
        Assert.Equal(100, _wood.Count(Items.Wood));
    }

    [Fact]
    public void AWildcardTakesEveryMatchingItem()
    {
        Chest meads = Labelled("...", 9);
        meads.Add(TestItems.Make(Items.MeadFrost, 4));
        meads.Add(TestItems.Make(Items.MeadHealth, 6));

        _world.Write(_request, "*mead* 2");

        Assert.Equal(2, _output.Count(Items.MeadFrost));
        Assert.Equal(2, _output.Count(Items.MeadHealth));
    }

    [Fact]
    public void TargetStockIsSavedOnTheSign()
    {
        _world.Write(_request, "SOLL Wood 1000, Stone 10");

        Assert.Contains(_world.Log, l => l.Contains("Warehouse 'main': saved 2 target stock value(s).", StringComparison.Ordinal));
        Assert.Empty(_output.Items);
    }
}
