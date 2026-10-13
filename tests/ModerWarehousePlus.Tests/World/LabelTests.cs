using ServersideQoL;
using Xunit;

namespace ModerWarehouse.Tests;

/// <summary>
/// Chest labels the warehouse manages, its janitor (items in the wrong chest), and the INFO sign.
/// </summary>
[Collection("Game state")]
public sealed class LabelTests : IDisposable
{
    private readonly TestWorld _world;

    private readonly ServersideQoLZDO _inSign;

    public LabelTests()
    {
        _world = new TestWorld();
        _world.Chest(0, 0);
        _inSign = _world.Sign("[WH:main:IN]", 0, 1);
        _world.Process(_inSign);
    }

    public void Dispose() => _world.Dispose();

    private TestItems Items => _world.Items;

    private (Chest Chest, ServersideQoLZDO Sign) Labelled(string label, float x)
    {
        Chest chest = _world.Chest(x, 0);
        return (chest, _world.Sign(label, x, 0.5f));
    }

    [Fact]
    public void AutomaticLabelsFollowTheWarehousesLanguageButPlayersLabelsDoNot()
    {
        var (resin, resinSign) = Labelled("...", 9);
        resin.Add(TestItems.Make(Items.Resin, 3));
        var (_, woodSign) = Labelled("Wood", 5);
        _world.Run("main");
        Assert.Equal("Resin", _world.PlainTextOf(resinSign));

        _world.Write(_inSign, "[WH:main:IN]\nLANG=DE");
        _world.Run("main");

        Assert.Equal("Harz", _world.PlainTextOf(resinSign));
        Assert.Equal("Wood", _world.PlainTextOf(woodSign));
    }

    [Fact]
    public void AnEmptiedChestKeepsItsAutomaticLabelAndStaysReserved()
    {
        _world.Chest(2, 0);
        _world.Sign("[WH:main:OUT]", 2, 1);
        var (resin, resinSign) = Labelled("...", 9);
        resin.Add(TestItems.Make(Items.Resin, 3));
        ServersideQoLZDO request = _world.Sign("[WH:main:REQ]", 4, 1);
        _world.Process(request);
        _world.Run("main");

        _world.Write(request, "Resin 3");

        Assert.Empty(resin.Items);
        Assert.Equal("Resin", _world.PlainTextOf(resinSign));
        Assert.Equal("$item_resin", resinSign.ZDO.GetString("ModerWarehouse.AutoLabelItem", string.Empty));
    }

    [Fact]
    public void WithAutomaticLabelsOffABlankSignStaysBlank()
    {
        using var world = new TestWorld(c => c.AutoCreateLabels.Value = false);
        world.Chest(0, 0);
        world.Process(world.Sign("[WH:main:IN]", 0, 1));
        Chest resin = world.Chest(9, 0);
        ServersideQoLZDO sign = world.Sign("...", 9, 0.5f);
        resin.Add(TestItems.Make(world.Items.Resin, 3));

        world.Run("main");

        Assert.Equal("...", world.PlainTextOf(sign));
    }

    [Fact]
    public void TheJanitorMovesAnItemFromTheWrongChestToAFreeOne()
    {
        var (wood, _) = Labelled("Wood", 5);
        var (free, freeSign) = Labelled("...", 11);
        wood.Add(TestItems.Make(Items.Wood, 20));
        wood.Add(TestItems.Make(Items.Stone, 5));

        _world.Run("main");

        Assert.Equal(0, wood.Count(Items.Stone));
        Assert.Equal(20, wood.Count(Items.Wood));
        Assert.Equal(5, free.Count(Items.Stone));
        Assert.Equal("Stone", _world.PlainTextOf(freeSign));
        Assert.Contains(_world.Log, l => l.Contains("janitor moved 5 x '$item_stone' from 'Wood'", StringComparison.Ordinal));
    }

    [Fact]
    public void TheJanitorMovesAnItemToTheChestLabelledForIt()
    {
        var (wood, _) = Labelled("Wood", 5);
        var (stone, _) = Labelled("Stone", 7);
        wood.Add(TestItems.Make(Items.Stone, 5));

        _world.Run("main");

        Assert.Equal(5, stone.Count(Items.Stone));
        Assert.Empty(wood.Items);
    }

    [Fact]
    public void AMisplacedItemWithNowhereToGoStaysPut()
    {
        var (wood, _) = Labelled("Wood", 5);
        wood.Add(TestItems.Make(Items.Stone, 5));

        _world.Run("main");

        Assert.Equal(5, wood.Count(Items.Stone));
    }

    // Moder Warehouse 2.0.0's readme says the janitor removes forbidden objects from managed storage;
    // the code (here as in 2.0.0) leaves a chest holding one out of the warehouse instead, and deletes
    // nothing. This test pins what the code does.
    [Fact]
    public void AChestHoldingAnInternalObjectIsLeftOutOfTheWarehouse()
    {
        var (stone, _) = Labelled("Stone", 7);
        var attack = new ItemDrop.ItemData.SharedData { m_name = "$attack_bonemass", m_maxStackSize = 1 };
        stone.Add(TestItems.Make(attack, 1));
        Chest input = _world.Chests.First(c => c.Zdo.ZDO.GetPosition().x == 0);
        input.Add(TestItems.Make(Items.Stone, 4));

        _world.Run("main");

        Assert.Equal(1, stone.Count(attack));
        Assert.Equal(0, stone.Count(Items.Stone));
        Assert.Equal(4, input.Count(Items.Stone));
    }

    [Fact]
    public void TheInfoSignCountsFreeReservedAndOccupiedChests()
    {
        var (wood, _) = Labelled("Wood", 5);
        wood.Add(TestItems.Make(Items.Wood, 10));
        Labelled("Stone", 7);
        Labelled("...", 9);
        ServersideQoLZDO info = _world.Sign("[WH:main:INFO]", 3, 1);
        _world.Process(info);

        _world.Run("main");

        Assert.Equal("WAREHOUSE MAIN\nFREE 1\nEMPTY RESERVED 1\nOCCUPIED 1 / 3", _world.PlainTextOf(info));
    }
}
