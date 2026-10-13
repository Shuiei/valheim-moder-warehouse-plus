using Xunit;

namespace ModerWarehouse.Tests;

/// <summary>
/// Board and statistics texts, and the typo distances behind REQ suggestions (WarehouseProcessor).
/// </summary>
public class WarehouseProcessorDisplayTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(999, "999")]
    [InlineData(1000, "1k")]
    [InlineData(1500, "1.5k")]
    [InlineData(999949, "999.9k")]
    [InlineData(2500000, "2.5M")]
    public void AmountsAreShortened(int amount, string text) => Assert.Equal(text, WarehouseProcessor.FormatAmount(amount));

    [Theory]
    [InlineData(-1500, "-1.5k")]
    [InlineData(-3, "-3")]
    [InlineData(42, "42")]
    public void SignedAmountsKeepTheirSign(int amount, string text) => Assert.Equal(text, WarehouseProcessor.FormatSignedAmount(amount));

    [Fact]
    public void ADayIsShownAsDayAndMonth() => Assert.Equal("03.10.", WarehouseProcessor.DayLabel(20261003));

    [Theory]
    [InlineData("wood", "W")]
    [InlineData("  öl", "Ö")]
    [InlineData("", "-")]
    [InlineData(null, "-")]
    public void AnInitialIsTheFirstLetterUpperCase(string? name, string initial) => Assert.Equal(initial, WarehouseProcessor.Initial(name));

    [Theory]
    [InlineData(150, 100, "+50%")]
    [InlineData(50, 100, "-50%")]
    [InlineData(100, 100, "0%")]
    [InlineData(0, 0, "-")]
    public void ChangesAreInPercent(int current, int previous, string text) =>
        Assert.Equal(text, WarehouseProcessor.PercentChange("en", current, previous));

    [Theory]
    [InlineData("today", new int[0], false, 1)]
    [InlineData("heuteubersicht", new int[0], false, 1)]
    [InlineData("vergleich", new int[0], false, 2)]
    [InlineData("trend", new int[0], false, 3)]
    [InlineData("records", new int[0], false, 4)]
    [InlineData("", new[] { 9, 2 }, false, 2)]
    [InlineData("", new[] { 3, 9 }, true, 0)]
    [InlineData("", new[] { 27, 3 }, true, 3)]
    [InlineData("", new int[0], false, 0)]
    public void AStatisticsPageIsChosenByWordOrNumber(string normalized, int[] numbers, bool combined, int page) =>
        Assert.Equal(page, WarehouseProcessor.ParseStatsPage(normalized, [.. numbers], combined));

    [Theory]
    [InlineData("wood", "wood", 0)]
    [InlineData("wood", "wod", 1)]
    [InlineData("wood", "wodo", 1)]
    [InlineData("stone", "tsone", 1)]
    [InlineData("kitten", "sitting", 3)]
    public void TyposCountSwappedLettersAsOne(string left, string right, int distance) =>
        Assert.Equal(distance, WarehouseProcessor.DamerauDistance(left, right));

    [Theory]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("abc", "acb", 2)]
    [InlineData("", "abc", 3)]
    public void LevenshteinCountsEachChange(string left, string right, int distance) =>
        Assert.Equal(distance, WarehouseProcessor.LevenshteinDistance(left, right));

    [Theory]
    [InlineData("wod", "finewood", 1)]
    [InlineData("wood", "finewood", 0)]
    [InlineData("finewood", "wood", 4)]
    public void APartOfANameCanBeTheBestMatch(string query, string name, int distance) =>
        Assert.Equal(distance, WarehouseProcessor.BestSubstringDistance(query, name));
}
