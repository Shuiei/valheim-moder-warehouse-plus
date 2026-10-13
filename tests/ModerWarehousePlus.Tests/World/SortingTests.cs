using Xunit;

namespace ModerWarehouse.Tests;

/// <summary>
/// The IN chest is sorted into the warehouse's chests: a whole warehouse cycle in a TestWorld.
/// The IN chest is at 0, 0; storage chests along x, each label sign half a metre from its chest.
/// </summary>
[Collection("Game state")]
public sealed class SortingTests : IDisposable
{
    private readonly TestWorld _world = new();

    private readonly Chest _input;

    public SortingTests()
    {
        _input = _world.Chest(0, 0);
        _world.Sign("[WH:main:IN]", 0, 1);
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
    public void ItemsGoToTheChestLabelledForThem()
    {
        Chest wood = Labelled("Wood", 5);
        Chest stone = Labelled("Stone", 7);
        _input.Add(TestItems.Make(Items.Wood, 30));
        _input.Add(TestItems.Make(Items.Stone, 12));

        _world.Run("main");

        Assert.Empty(_input.Items);
        Assert.Equal(30, wood.Count(Items.Wood));
        Assert.Equal(12, stone.Count(Items.Stone));
        Assert.Equal(0, wood.Count(Items.Stone));
    }

    [Fact]
    public void ALabelInGermanOrWithTagsWorksToo()
    {
        Chest wood = Labelled("<color=white>Holz", 5);
        _input.Add(TestItems.Make(Items.Wood, 30));

        _world.Run("main");

        Assert.Equal(30, wood.Count(Items.Wood));
    }

    [Fact]
    public void ItemsWithNoPlaceStayInTheInChest()
    {
        Labelled("Stone", 5);
        _input.Add(TestItems.Make(Items.Resin, 7));

        _world.Run("main");

        Assert.Equal(7, _input.Count(Items.Resin));
    }

    [Fact]
    public void AChestWithoutSignIsNeverUsed()
    {
        Chest unsigned = _world.Chest(5, 0);
        unsigned.Add(TestItems.Make(Items.Wood, 10));
        _input.Add(TestItems.Make(Items.Wood, 30));

        _world.Run("main");

        Assert.Equal(30, _input.Count(Items.Wood));
        Assert.Equal(10, unsigned.Count(Items.Wood));
    }

    [Fact]
    public void AnIgnoredChestIsNeverUsed()
    {
        Chest ignored = Labelled("Wood", 5);
        _world.Sign("[WH:IGNORE]", 5, -0.5f);
        _input.Add(TestItems.Make(Items.Wood, 30));

        _world.Run("main");

        Assert.Equal(30, _input.Count(Items.Wood));
        Assert.Empty(ignored.Items);
    }

    [Fact]
    public void APrivateChestIsNeverUsed()
    {
        Chest personal = _world.Chest(5, 0, prefab: "piece_chest_private");
        _world.Sign("Wood", 5, 0.5f);
        _input.Add(TestItems.Make(Items.Wood, 30));

        _world.Run("main");

        Assert.Equal(30, _input.Count(Items.Wood));
        Assert.Empty(personal.Items);
    }

    [Fact]
    public void CheatedItemsStayInTheInChest()
    {
        Chest wood = Labelled("Wood", 5);
        // A full stack first: Valheim stacks items by name and quality only, so the cheated stack
        // would otherwise be merged into it.
        _input.Add(TestItems.Make(Items.Wood, 50));
        _input.Add(TestItems.Make(Items.Wood, 30, cheated: true));

        _world.Run("main");

        Assert.Equal(50, wood.Count(Items.Wood));
        Assert.Equal(30, Assert.Single(_input.Items, i => i.m_cheated).m_stack);
        Assert.Single(_input.Items);
    }

    [Fact]
    public void ExistingStacksAreFilledBeforeNewOnesStart()
    {
        Chest wood = Labelled("Wood", 5);
        wood.Add(TestItems.Make(Items.Wood, 45));
        _input.Add(TestItems.Make(Items.Wood, 10));

        _world.Run("main");

        Assert.Equal(55, wood.Count(Items.Wood));
        Assert.Equal([50, 5], wood.Items.Select(i => i.m_stack).Order().Reverse());
    }

    [Fact]
    public void AFullChestOverflowsIntoTheNextOneForTheSameItem()
    {
        Chest first = _world.Chest(5, 0, width: 1, height: 1);
        _world.Sign("Wood", 5, 0.5f);
        Chest second = _world.Chest(7, 0, width: 1, height: 1);
        _world.Sign("Wood", 7, 0.5f);
        _input.Add(TestItems.Make(Items.Wood, 50));
        _input.Add(TestItems.Make(Items.Wood, 30));

        _world.Run("main");

        Assert.Equal(80, first.Count(Items.Wood) + second.Count(Items.Wood));
        Assert.Empty(_input.Items);
    }

    [Fact]
    public void WhenEverythingIsFullItemsStayInTheInChest()
    {
        Chest full = _world.Chest(5, 0, width: 1, height: 1);
        _world.Sign("Wood", 5, 0.5f);
        full.Add(TestItems.Make(Items.Wood, 50));
        _input.Add(TestItems.Make(Items.Wood, 20));

        _world.Run("main");

        Assert.Equal(20, _input.Count(Items.Wood));
        Assert.Equal(50, full.Count(Items.Wood));
    }

    [Fact]
    public void ChestsOutsideTheWarehousesRadiusAreNotUsed()
    {
        _world.Config.Radius.Value = 20;
        Chest far = Labelled("Wood", 25);
        _input.Add(TestItems.Make(Items.Wood, 30));

        _world.Run("main");

        Assert.Equal(30, _input.Count(Items.Wood));
        Assert.Empty(far.Items);
    }

    [Fact]
    public void ABlankSignEnrolsAnEmptyChestForAnything()
    {
        Chest free = Labelled("...", 5);
        _input.Add(TestItems.Make(Items.Amber, 3));

        _world.Run("main");

        Assert.Equal(3, free.Count(Items.Amber));
        Assert.Empty(_input.Items);
    }

    [Fact]
    public void AnUnlabelledChestHoldingOneItemTakesMoreOfIt()
    {
        Chest resin = Labelled("...", 5);
        resin.Add(TestItems.Make(Items.Resin, 3));
        Chest free = Labelled("...", 7);
        _input.Add(TestItems.Make(Items.Resin, 4));

        _world.Run("main");

        Assert.Equal(7, resin.Count(Items.Resin));
        Assert.Empty(free.Items);
    }

    [Fact]
    public void ABlankSignOnAChestHoldingOneItemBecomesItsLabel()
    {
        Chest resin = _world.Chest(5, 0);
        var sign = _world.Sign("...", 5, 0.5f);
        resin.Add(TestItems.Make(Items.Resin, 3));
        _input.Add(TestItems.Make(Items.Resin, 4));

        _world.Run("main");

        Assert.Equal("Resin", _world.PlainTextOf(sign));
        Assert.Contains(_world.Log, l => l.Contains("Reused blank sign and labeled chest 'Resin'", StringComparison.Ordinal));
    }

    [Fact]
    public void AFrozenContentsLineIsTakenAsItsItem()
    {
        Chest wood = _world.Chest(5, 0);
        var sign = _world.Sign("Wood 521", 5, 0.5f);
        _input.Add(TestItems.Make(Items.Wood, 30));

        _world.Run("main");

        Assert.Equal(30, wood.Count(Items.Wood));
        Assert.Equal("Wood", _world.PlainTextOf(sign));
    }

    [Fact]
    public void AnInputSignWithoutAChestIsNoWarehouse()
    {
        Chest wood = Labelled("Wood", 5);
        _world.Sign("[WH:lost:IN]", 50, 50);

        _world.Run("lost");

        Assert.Empty(wood.Items);
    }
}
