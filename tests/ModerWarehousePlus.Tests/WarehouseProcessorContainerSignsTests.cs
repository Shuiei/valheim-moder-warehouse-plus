using Xunit;

namespace ModerWarehouse.Tests;

/// <summary>
/// Which sign is a warehouse chest's label, the text ContainerSigns then shows on all of that chest's
/// signs (WarehouseProcessor.ChooseChestLabels).
/// </summary>
public class WarehouseProcessorContainerSignsTests
{
    private sealed record Chest(string Name);

    private static readonly Chest A = new("a"), B = new("b"), Outside = new("outside");

    private static Dictionary<Chest, string> Choose(params (string Sign, Chest? Chest, bool AutoManaged)[] labels) =>
        WarehouseProcessor.ChooseChestLabels(labels.Select(l => (l.Sign, l.Chest!, l.AutoManaged)), new HashSet<Chest> { A, B });

    [Fact]
    public void EachManagedChestGetsItsLabel()
    {
        var chosen = Choose(("wood sign", A, false), ("stone sign", B, true));
        Assert.Equal("wood sign", chosen[A]);
        Assert.Equal("stone sign", chosen[B]);
    }

    [Fact]
    public void TheWarehousesOwnLabelWinsOverAPlayersWhicheverComesFirst()
    {
        Assert.Equal("auto", Choose(("player", A, false), ("auto", A, true))[A]);
        Assert.Equal("auto", Choose(("auto", A, true), ("player", A, false))[A]);
    }

    [Fact]
    public void BetweenTwoOfTheSameKindTheFirstStays()
    {
        Assert.Equal("first", Choose(("first", A, false), ("second", A, false))[A]);
        Assert.Equal("first", Choose(("first", A, true), ("second", A, true))[A]);
    }

    [Fact]
    public void ChestsOutsideTheWarehouseAndSignsOfNoChestAreLeftToContainerSigns()
    {
        var chosen = Choose(("outside", Outside, true), ("nowhere", null, true));
        Assert.Empty(chosen);
    }
}
