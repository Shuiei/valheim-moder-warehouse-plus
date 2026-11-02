using Xunit;

namespace ModerWarehouse.Tests;

public class WarehouseTextTests
{
    [Fact]
    public void BarsOresAndScrapHaveTheirOwnGermanNames()
    {
        Assert.Contains("kupferbarren", WarehouseText.GermanAliases("copper"));
        Assert.DoesNotContain("kupfererz", WarehouseText.GermanAliases("copper"));
        Assert.Contains("kupfererz", WarehouseText.GermanAliases("copperore"));
        Assert.DoesNotContain("kupferbarren", WarehouseText.GermanAliases("copperore"));
        Assert.Contains("altmetall", WarehouseText.GermanAliases("scrapiron"));
        Assert.DoesNotContain("altmetall", WarehouseText.GermanAliases("iron"));
        Assert.Contains("holz", WarehouseText.GermanAliases("wood"));
        Assert.Empty(WarehouseText.GermanAliases("nothinglikethis"));
        Assert.Empty(WarehouseText.GermanAliases(null!));
    }

    [Fact]
    public void AnItemIsKnownByItsPrefabTokenAndShownNames()
    {
        string[] aliases = WarehouseText.BuildAliases("Wood", "$item_wood", "wood", "holz");
        Assert.Equal(["wood", "holz"], aliases);
        Assert.Contains("kupferbarren", WarehouseText.BuildAliases("Copper", "$item_copper", "copper", null!));
    }

    [Fact]
    public void ALabelNamesAnItemWhenEveryPartIsOneOfItsNames()
    {
        string[] aliases = ["wood", "holz"];
        Assert.True(WarehouseText.LabelNamesItem("Holz", aliases, null!, out bool needsSample));
        Assert.False(needsSample);
        Assert.False(WarehouseText.LabelNamesItem("Holz Stein", aliases, null!, out _));
        Assert.False(WarehouseText.LabelNamesItem("", aliases, null!, out _));
        Assert.False(WarehouseText.LabelNamesItem("Holz", null!, null!, out _));
    }

    [Fact]
    public void AnAmbiguousNameNeedsASampleInTheChest()
    {
        Assert.True(WarehouseText.LabelNamesItem("Harz", ["resin", "harz"], name => name == "harz", out bool needsSample));
        Assert.True(needsSample);
    }

    [Fact]
    public void AMatchIsRememberedPerText()
    {
        var cache = new WarehouseText.MatchCache(WarehouseProcessor.CleanMarker);
        Assert.True(cache.IsMatch("[WH:CLEAN]"));
        Assert.Same(cache.Match("[WH:CLEAN]"), cache.Match("[WH:CLEAN]"));
        Assert.False(cache.IsMatch(null!));
    }
}
