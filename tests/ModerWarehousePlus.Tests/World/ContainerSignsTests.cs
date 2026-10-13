using Xunit;

namespace ModerWarehouse.Tests;

/// <summary>
/// With ServersideQoL ContainerSigns installed: inside a warehouse its signs show the warehouse's
/// labels (the label is written to the chest, whose text ContainerSigns puts on the chest's signs);
/// outside, ContainerSigns is left alone. Without it, nothing of this happens.
/// </summary>
[Collection("Game state")]
public sealed class ContainerSignsTests : IDisposable
{
    private const string ContentsList = "<i ls></i>Wood 5<i le></i>";

    private readonly TestWorld _world = new();

    private readonly Chest _input;

    public ContainerSignsTests()
    {
        _world.ContainerSignsInstalled = true;
        _input = _world.Chest(0, 0);
        _world.Sign("[WH:main:IN]", 0, 1);
    }

    public void Dispose() => _world.Dispose();

    [Fact]
    public void AWarehouseChestKeepsItsLabelSoContainerSignsShowsIt()
    {
        Chest wood = _world.Chest(5, 0);
        var label = _world.Sign("Wood", 5, 0.5f);
        _input.Add(TestItems.Make(_world.Items.Wood, 10));

        _world.Run("main");

        Assert.Equal(_world.TextOf(label), _world.ChestText(wood));
        Assert.Equal("Wood", WarehouseProcessor.PlainText(_world.ChestText(wood)));
    }

    [Fact]
    public void AContainerSignsContentsListEnrolsItsChest()
    {
        Chest listed = _world.Chest(5, 0);
        _world.Sign(ContentsList, 5, 0.5f);
        _input.Add(TestItems.Make(_world.Items.Amber, 3));

        _world.Run("main");

        Assert.Equal(3, listed.Count(_world.Items.Amber));
    }

    [Fact]
    public void TheWarehousesOwnLabelIsTheOneKeptWhenAChestHasTwo()
    {
        Chest wood = _world.Chest(5, 0);
        _world.Sign("Holz", 5, 0.5f);
        var own = _world.Sign("<color=white>Wood", 5.5f, 0);
        own.ZDO.Set("ModerWarehouse.AutoLabelManaged", true);

        _world.Run("main");

        Assert.Equal(_world.TextOf(own), _world.ChestText(wood));
    }

    [Fact]
    public void ChestsOutsideTheWarehouseAreLeftToContainerSigns()
    {
        _world.Config.Radius.Value = 20;
        Chest outside = _world.Chest(40, 0);
        _world.Sign("Wood", 40, 0.5f);

        _world.Run("main");

        Assert.Equal(string.Empty, _world.ChestText(outside));
    }

    [Fact]
    public void WithoutContainerSignsTheChestIsNotWrittenTo()
    {
        _world.ContainerSignsInstalled = false;
        Chest wood = _world.Chest(5, 0);
        _world.Sign("Wood", 5, 0.5f);
        _input.Add(TestItems.Make(_world.Items.Wood, 10));

        _world.Run("main");

        Assert.Equal(10, wood.Count(_world.Items.Wood));
        Assert.Equal(string.Empty, _world.ChestText(wood));
    }
}
