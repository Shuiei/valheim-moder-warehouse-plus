using Xunit;

namespace ModerWarehouse.Tests;

/// <summary>
/// What a REQ sign accepts: withdrawals, target stock, crafting and wildcard requests (WarehouseProcessor).
/// </summary>
public class WarehouseProcessorRequestTests
{
    [Theory]
    [InlineData("Wood 100", "Wood", 100)]
    [InlineData("  Fine wood x5 ", "Fine wood", 5)]
    [InlineData("Wood 5\nStone 3", "Wood", 5)]
    [InlineData("Wood 10000", "Wood", 10000)]
    public void AWithdrawalIsAnItemAndAnAmount(string payload, string item, int amount)
    {
        Assert.True(WarehouseProcessor.TryParseRequest(payload, out string query, out int parsed));
        Assert.Equal(item, query);
        Assert.Equal(amount, parsed);
    }

    [Theory]
    [InlineData("Wood")]
    [InlineData("Wood 0")]
    [InlineData("Wood 10001")]
    [InlineData("100")]
    [InlineData("")]
    public void AWithdrawalNeedsAnAmountFromOneToTenThousand(string payload) =>
        Assert.False(WarehouseProcessor.TryParseRequest(payload, out _, out _));

    [Theory]
    [InlineData("craft Iron sword", "Iron sword", 1, 1)]
    [InlineData("CRAFT Iron sword lvl 3", "Iron sword", 1, 3)]
    [InlineData("craft Iron nails x5", "Iron nails", 5, 1)]
    [InlineData("craft Iron sword level 2 x3", "Iron sword", 3, 2)]
    [InlineData("craft Iron sword q4", "Iron sword", 1, 4)]
    [InlineData("herstellen Eisenschwert stufe 2 3", "Eisenschwert", 3, 2)]
    [InlineData("craft *helmet x2", "*helmet", 2, 1)]
    [InlineData("craft Stone wall x1000\nignored", "Stone wall", 1000, 1)]
    public void ACraftRequestNamesWhatToMakeAndOptionallyLevelAndCount(string payload, string item, int count, int level)
    {
        Assert.True(WarehouseProcessor.TryParseCraftRequest(payload, out string query, out int parsedCount, out int parsedLevel));
        Assert.Equal(item, query);
        Assert.Equal(count, parsedCount);
        Assert.Equal(level, parsedLevel);
    }

    [Theory]
    [InlineData("craft")]
    [InlineData("craft Iron sword lvl 11")]
    [InlineData("craft Iron sword lvl 0")]
    [InlineData("craft Iron nails x1001")]
    [InlineData("craft Iron nails x0")]
    [InlineData("Iron sword")]
    [InlineData("crafting table 1")]
    public void ACraftRequestKeepsItsLimits(string payload) =>
        Assert.False(WarehouseProcessor.TryParseCraftRequest(payload, out _, out _, out _));

    [Theory]
    [InlineData("*mead*", "*mead*", 1)]
    [InlineData("*frost resistance 5", "*frost resistance", 5)]
    [InlineData("Mead* x3", "Mead*", 3)]
    public void AWildcardRequestHasAStarAndAnOptionalAmount(string payload, string display, int amount)
    {
        Assert.True(WarehouseProcessor.TryParseWildcardRequest(payload, out var pattern, out string parsedDisplay, out int parsedAmount));
        Assert.NotNull(pattern);
        Assert.Equal(display, parsedDisplay);
        Assert.Equal(amount, parsedAmount);
    }

    [Theory]
    [InlineData("Mead 5")]
    [InlineData("*mead* 0")]
    [InlineData("*mead* 10001")]
    public void AWildcardRequestNeedsAStarAndAReasonableAmount(string payload) =>
        Assert.False(WarehouseProcessor.TryParseWildcardRequest(payload, out _, out _, out _));

    [Theory]
    [InlineData("*frost resistance", "Mead: Frost resistance", true)]
    [InlineData("*frost resistance", "Frost resistance mead", false)]
    [InlineData("*mead*", "Minor healing mead", true)]
    [InlineData("mead*", "Mead base: Tasty", true)]
    [InlineData("mead*", "Tasty mead", false)]
    [InlineData("*", "Anything", true)]
    [InlineData("iron*sword", "Iron sword", true)]
    [InlineData("iron*sword", "Iron sword (2)", false)]
    public void AStarStandsForAnyTextIgnoringCaseSpacesAndPunctuation(string wildcard, string itemName, bool matches) =>
        Assert.Equal(matches, WarehouseProcessor.WildcardPattern(wildcard).IsMatch(WarehouseProcessor.Normalize(itemName)));

    [Fact]
    public void AWildcardTakesAtMostTenDifferentItems() => Assert.Equal(10, WarehouseProcessor.MaxWildcardItems);

    [Fact]
    public void TargetStockIsAListOfWithdrawals()
    {
        Assert.True(WarehouseProcessor.TryParseTargets("Wood 1000, Stone 500; Resin 100\nwood 50", out var targets));
        Assert.Equal(3, targets.Count);
        Assert.Equal(50, targets["Wood"]);
        Assert.Equal(500, targets["stone"]);
    }

    [Theory]
    [InlineData("Wood 1000, Stone")]
    [InlineData("")]
    [InlineData(" , ; ")]
    public void TargetStockWithABadOrNoEntryIsRefused(string payload) =>
        Assert.False(WarehouseProcessor.TryParseTargets(payload, out _));

    [Fact]
    public void TargetStockKeepsTwentyItems()
    {
        Assert.True(WarehouseProcessor.TryParseTargets(string.Join(",", Enumerable.Range(1, 25).Select(i => $"Item{i} {i}")), out var targets));
        Assert.Equal(20, targets.Count);
        Assert.DoesNotContain("Item21", targets.Keys);
    }

    [Fact]
    public void SavedTargetStockSkipsBrokenLines()
    {
        var targets = WarehouseProcessor.DeserializeTargets("Wood\t5\nBroken\nStone\t0\nResin\t-2\nFlint\tx\nwood\t7");
        Assert.Equal(7, Assert.Single(targets).Value);
    }
}
