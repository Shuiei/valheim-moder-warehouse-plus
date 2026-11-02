using Xunit;

namespace ModerWarehouse.Tests;

/// <summary>
/// Category, catch-all (MISC) and refill (FILL) chests, from Moder Warehouse 2.0.2: whole warehouse
/// cycles in a TestWorld. The IN chest is at 0, 0; storage chests along x, each label sign half a
/// metre from its chest.
/// </summary>
[Collection("Game state")]
public sealed class ChestKindsTests : IDisposable
{
    private readonly TestWorld _world = new();

    private readonly Chest _input;

    public ChestKindsTests()
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
    public void ACategoryChestTakesItsWholeGroup()
    {
        Chest weapons = Labelled("WEAPONS", 5);
        _input.Add(TestItems.Make(Items.Sword, 1));

        _world.Run("main");

        Assert.Empty(_input.Items);
        Assert.Equal(1, weapons.Count(Items.Sword));
    }

    [Fact]
    public void AnItemsOwnChestComesBeforeACatchAllChest()
    {
        Chest stone = Labelled("Stone", 5);
        Chest misc = Labelled("MISC", 7);
        _input.Add(TestItems.Make(Items.Stone, 12));
        _input.Add(TestItems.Make(Items.Resin, 7));

        _world.Run("main");

        Assert.Empty(_input.Items);
        Assert.Equal(12, stone.Count(Items.Stone));
        Assert.Equal(7, misc.Count(Items.Resin));
        Assert.Equal(0, misc.Count(Items.Stone));
    }

    [Fact]
    public void ARefillChestIsToppedUpFromStorageAndNeverOverfilled()
    {
        Chest wood = Labelled("Wood", 5);
        wood.Add(TestItems.Make(Items.Wood, 50));
        Chest kiln = _world.Chest(10, 0);
        _world.Sign("[WH:main:FILL] Wood 20", 10, 1);

        _world.Run("main");

        Assert.Equal(20, kiln.Count(Items.Wood));
        Assert.Equal(30, wood.Count(Items.Wood));

        _world.Advance(TimeSpan.FromMinutes(1));
        _world.Run("main");

        Assert.Equal(20, kiln.Count(Items.Wood));
        Assert.Equal(30, wood.Count(Items.Wood));
    }
}
