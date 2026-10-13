using ServersideQoL;
using Xunit;

namespace ModerWarehouse.Tests;

/// <summary>
/// The 4x4 stock board and statistics board: built from one anchor sign, asleep until a player
/// comes near, then showing the warehouse's stock, its chests and its history.
/// </summary>
[Collection("Game state")]
public sealed class BoardTests : IDisposable
{
    private readonly TestWorld _world = new();

    private readonly Chest _input;

    private readonly Chest _wood;

    private readonly Chest _stone;

    public BoardTests()
    {
        _input = _world.Chest(0, 0);
        _world.Process(_world.Sign("[WH:main:IN]", 0, 1));
        _wood = Labelled("Wood", 5);
        _stone = Labelled("Stone", 7);
        _wood.Add(TestItems.Make(_world.Items.Wood, 50));
        _wood.Add(TestItems.Make(_world.Items.Wood, 30));
        _stone.Add(TestItems.Make(_world.Items.Stone, 12));
    }

    public void Dispose() => _world.Dispose();

    private Chest Labelled(string label, float x)
    {
        Chest chest = _world.Chest(x, 0);
        _world.Sign(label, x, 0.5f);
        return chest;
    }

    /// <summary>An anchor sign, handed to the warehouse twice the board's wait apart.</summary>
    private ServersideQoLZDO Board(string marker, float x)
    {
        ServersideQoLZDO anchor = _world.Sign(marker, x, 0);
        _world.Process(anchor);
        _world.Advance(TimeSpan.FromSeconds(6));
        _world.Process(anchor);
        return anchor;
    }

    private string Cell(ServersideQoLZDO anchor, int cell) =>
        _world.BoardCells(anchor).TryGetValue(cell, out var sign) ? _world.PlainTextOf(sign) : $"(no cell {cell})\n{_world.Describe()}";

    [Fact]
    public void TheStockBoardIsBuiltAMomentAfterItsAnchor()
    {
        ServersideQoLZDO anchor = _world.Sign("[WH:main:BOARD]", 20, 0);
        _world.Process(anchor);
        Assert.Single(_world.BoardCells(anchor));

        _world.Advance(TimeSpan.FromSeconds(6));
        _world.Process(anchor);

        var cells = _world.BoardCells(anchor);
        Assert.Equal(Enumerable.Range(0, 16), cells.Keys.Order());
        Assert.All(cells.Where(c => c.Key != 12), c => Assert.True(_world.IsPlacedByMod(c.Value) && _world.IsRemovable(c.Value)));
        Assert.Contains(_world.Log, l => l.Contains("created 15 sign(s) for 4x4 stock board", StringComparison.Ordinal));
    }

    [Fact]
    public void TheBoardGrowsUpAndToOneSideOfItsAnchor()
    {
        ServersideQoLZDO anchor = Board("[WH:main:BOARD]", 20);
        var cells = _world.BoardCells(anchor);

        Assert.True(cells[0].ZDO.GetPosition().y > anchor.ZDO.GetPosition().y);
        Assert.Equal(anchor.ZDO.GetPosition().y, cells[15].ZDO.GetPosition().y, 0.01f);
        Assert.NotEqual(anchor.ZDO.GetPosition().x, cells[15].ZDO.GetPosition().x);
    }

    [Fact]
    public void WithNobodyNearTheBoardSleeps()
    {
        ServersideQoLZDO anchor = Board("[WH:main:BOARD]", 20);

        _world.Run("main");

        Assert.Equal("DISPLAY\nSLEEPING", Cell(anchor, 1));
        Assert.Equal("5 METRES\nWAKE UP", Cell(anchor, 2));
    }

    [Fact]
    public void APlayerNearbyWakesItToShowTheStock()
    {
        ServersideQoLZDO anchor = Board("[WH:main:BOARD]", 20);
        _world.Player(21, 1);

        _world.Run("main");

        Assert.Equal("WAREHOUSE\nMAIN", Cell(anchor, 0));
        Assert.Equal("PAGE\n1 / 1", Cell(anchor, 2));
        Assert.Equal("Stone\n12", Cell(anchor, 4));
        Assert.Equal("Wood\n80", Cell(anchor, 5));
        Assert.Equal("-", Cell(anchor, 6));
    }

    [Fact]
    public void TheChestsCellCountsOccupiedChestsAndAsksToExpandWhenFull()
    {
        ServersideQoLZDO anchor = Board("[WH:main:BOARD]", 20);
        _world.Player(21, 1);
        _world.Run("main");
        Assert.Equal("CHESTS\nEXPAND!", Cell(anchor, 3));

        Labelled("...", 9);
        Labelled("...", 11);
        _world.Run("main");
        Assert.Equal("CHESTS\n2 / 4", Cell(anchor, 3));
    }

    [Fact]
    public void WritingOnTheStockCellFiltersTheList()
    {
        ServersideQoLZDO anchor = Board("[WH:main:BOARD]", 20);
        _world.Player(21, 1);
        _world.Run("main");

        _world.Write(_world.BoardCells(anchor)[1], "W");
        _world.Run("main");

        Assert.Equal("Wood\n80", Cell(anchor, 4));
        Assert.Equal("-", Cell(anchor, 5));
    }

    [Fact]
    public void TheBoardIsInTheWarehousesLanguage()
    {
        _world.Write(_world.AllSigns.First(s => _world.PlainTextOf(s) == "[WH:main:IN]"), "[WH:main:IN]\nLANG=DE");
        ServersideQoLZDO anchor = Board("[WH:main:BOARD]", 20);

        _world.Run("main");

        Assert.Equal("ANZEIGE\nSCHLÄFT", Cell(anchor, 1));
    }

    [Fact]
    public void TheStatisticsBoardIsBuiltWithItsControlSign()
    {
        ServersideQoLZDO anchor = Board("[WH:main:STATS]", 30);

        Assert.Equal(Enumerable.Range(0, 16), _world.BoardCells(anchor).Keys.Order());
        Assert.Contains(_world.AllSigns, s => _world.PlainTextOf(s).StartsWith("CONTROL", StringComparison.Ordinal));
        Assert.Contains(_world.Log, l => l.Contains("created 16 sign/control(s) for 4x4 statistics board", StringComparison.Ordinal));
    }

    [Fact]
    public void TheStatisticsBoardCountsTodaysDepositsOnItsFirstPage()
    {
        ServersideQoLZDO anchor = Board("[WH:main:STATS]", 30);
        _world.Player(31, 1);
        _input.Add(TestItems.Make(_world.Items.Wood, 7));

        _world.Run("main");
        _world.Write(_world.BoardCells(anchor)[2], "1");
        _world.Run("main");

        Assert.Equal("STATISTICS\nMAIN", Cell(anchor, 0));
        Assert.Equal("PERIOD\nTODAY", Cell(anchor, 1));
        Assert.Equal("PAGE\n1 / 4", Cell(anchor, 2));
        Assert.Equal("INPUT\n7", Cell(anchor, 4));
        Assert.Equal("OUTPUT\n0", Cell(anchor, 5));
        Assert.Equal("BALANCE\n+7", Cell(anchor, 6));
        Assert.Equal("TOP INPUT\nWood +7", Cell(anchor, 8));
        Assert.Equal("CURRENT TOTAL\n99", Cell(anchor, 10));
        Assert.Equal("ITEM TYPES\n2", Cell(anchor, 11));
    }
}
