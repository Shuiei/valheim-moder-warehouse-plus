using ServersideQoL;
using Xunit;

namespace ModerWarehouse.Tests;

/// <summary>
/// "craft ..." on a REQ sign: the materials of a recipe or building piece go into OUT, minus what
/// OUT already holds (whole warehouse cycles in a TestWorld with the game's recipes stood in for).
/// </summary>
[Collection("Game state")]
public sealed class CraftTests : IDisposable
{
    private readonly TestWorld _world = new();

    private readonly Chest _output;

    private readonly ServersideQoLZDO _request;

    public CraftTests()
    {
        _world.Chest(0, 0);
        _world.Process(_world.Sign("[WH:main:IN]", 0, 1));
        _output = _world.Chest(2, 0);
        _world.Sign("[WH:main:OUT]", 2, 1);
        Stock(Items.Wood, 5, 100);
        Stock(Items.Resin, 7, 40);
        Stock(Items.BronzeNails, 9, 60);
        Stock(Items.Stone, 11, 50);
        Stock(Items.Amber, 13, 20);
        // An iron sword (levels 1-4): 2 wood, 5 + 3 per level resin, 10 bronze nails.
        _world.Recipe(Items.Sword, _world.Needs(Items.Wood, 2), _world.Needs(Items.Resin, 5, 3), _world.Needs(Items.BronzeNails, 10));
        _world.Recipe(Items.MeadFrost, _world.Needs(Items.Resin, 4));
        _world.Recipe(Items.MeadHealth, _world.Needs(Items.Amber, 2));
        _world.BuildPiece("stone_wall_2x1", "Stone wall", "Steinmauer", _world.Needs(Items.Stone, 6));
        _request = _world.Sign("[WH:main:REQ]", 3, 1);
        _world.Process(_request);
    }

    public void Dispose() => _world.Dispose();

    private TestItems Items => _world.Items;

    private void Stock(ItemDrop.ItemData.SharedData item, float x, int amount)
    {
        Chest chest = _world.Chest(x, 0);
        _world.Sign(Items.PrefabOf(item), x, 0.5f);
        while (amount > 0)
        {
            int stack = Math.Min(amount, item.m_maxStackSize);
            chest.Add(TestItems.Make(item, stack));
            amount -= stack;
        }
    }

    private string Request => _world.PlainTextOf(_request);

    [Fact]
    public void CraftingPutsTheRecipesMaterialsIntoOut()
    {
        _world.Write(_request, "craft Iron sword");

        Assert.Equal(2, _output.Count(Items.Wood));
        Assert.Equal(5, _output.Count(Items.Resin));
        Assert.Equal(10, _output.Count(Items.BronzeNails));
        Assert.StartsWith("CRAFTING Iron sword x1:", Request, StringComparison.Ordinal);
    }

    // The game's rule (Piece.Requirement.GetAmount): upgrading to level n costs (n - 1) x the amount
    // per level, without the first level's amount.
    [Fact]
    public void AnUpgradeLevelTakesThatLevelsMaterials()
    {
        _world.Write(_request, "craft Iron sword lvl 3");

        Assert.Equal(2 * 3, _output.Count(Items.Resin));
        Assert.Equal(0, _output.Count(Items.Wood));
        Assert.StartsWith("CRAFTING Iron sword LVL 3 x1:", Request, StringComparison.Ordinal);
    }

    [Fact]
    public void ACountMultipliesTheMaterials()
    {
        _world.Write(_request, "craft Iron sword x3");

        Assert.Equal(6, _output.Count(Items.Wood));
        Assert.Equal(30, _output.Count(Items.BronzeNails));
    }

    [Fact]
    public void WhatOutAlreadyHoldsIsNotTakenAgain()
    {
        _output.Add(TestItems.Make(Items.BronzeNails, 10));
        _output.Add(TestItems.Make(Items.Wood, 1));

        _world.Write(_request, "craft Iron sword");

        Assert.Equal(10, _output.Count(Items.BronzeNails));
        Assert.Equal(2, _output.Count(Items.Wood));
        Assert.Contains("IN OUT Bronze nails x10", Request, StringComparison.Ordinal);
    }

    [Fact]
    public void ABuildingPieceCanBeCraftedByItsName()
    {
        _world.Write(_request, "craft Stone wall x10");

        Assert.Equal(50, _output.Count(Items.Stone));
        Assert.StartsWith("CRAFTING Stone wall x10:", Request, StringComparison.Ordinal);
    }

    [Fact]
    public void GermanNamesWorkToo()
    {
        _world.Write(_request, "herstellen Eisenschwert");

        Assert.Equal(10, _output.Count(Items.BronzeNails));
    }

    [Fact]
    public void ASmallTypoIsForgiven()
    {
        _world.Write(_request, "craft Iron swort");

        Assert.Equal(10, _output.Count(Items.BronzeNails));
    }

    [Fact]
    public void AWildcardNamingOneRecipeCraftsIt()
    {
        _world.Write(_request, "craft *frost*");

        Assert.Equal(4, _output.Count(Items.Resin));
    }

    [Fact]
    public void AWildcardNamingSeveralRecipesListsThem()
    {
        _world.Write(_request, "craft *mead*");

        Assert.Empty(_output.Items);
        Assert.Equal("NO RECIPE *mead*\nFrost resistance mead | Minor healing mead", Request);
    }

    [Fact]
    public void AnUnknownRecipeIsReported()
    {
        _world.Write(_request, "craft Dragon egg");

        Assert.Empty(_output.Items);
        Assert.StartsWith("NO RECIPE Dragon egg", Request, StringComparison.Ordinal);
    }

    [Fact]
    public void ALevelAboveTheItemsMaximumIsRefused()
    {
        _world.Write(_request, "craft Iron sword lvl 5");

        Assert.Empty(_output.Items);
        Assert.Equal("MAX LEVEL Iron sword: 4", Request);
    }

    [Fact]
    public void WhatTheWarehouseLacksIsDeliveredAsFarAsItGoes()
    {
        _world.Write(_request, "craft Iron sword x7");

        Assert.Equal(60, _output.Count(Items.BronzeNails));
        Assert.Contains("PARTIAL", Request, StringComparison.Ordinal);
    }
}
