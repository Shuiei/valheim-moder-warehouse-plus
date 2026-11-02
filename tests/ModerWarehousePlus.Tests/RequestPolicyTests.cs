using Xunit;

namespace ModerWarehouse.Tests;

public class RequestPolicyTests
{
    [Theory]
    [InlineData("TARGET Wood 50", "Wood 50")]
    [InlineData("  soll Holz 20, Stein 10", "Holz 20, Stein 10")]
    [InlineData("OBJECTIF   Wood 5 ", "Wood 5")]
    public void ATargetKeywordInAnyLanguageIsStripped(string text, string rest)
    {
        Assert.True(RequestPolicy.TryStripKeyword(text, ["SOLL", "TARGET", "OBJECTIF"], out string actual));
        Assert.Equal(rest, actual);
    }

    [Theory]
    [InlineData("Targetwood 5")]
    [InlineData("TARGET")]
    [InlineData("Wood 5")]
    [InlineData("")]
    [InlineData(null)]
    public void AnythingElseKeepsItsText(string? text)
    {
        Assert.False(RequestPolicy.TryStripKeyword(text!, ["TARGET", " ", ""], out string rest));
        Assert.Equal(string.Empty, rest);
    }

    [Fact]
    public void NoKeywordsStripNothing() => Assert.False(RequestPolicy.TryStripKeyword("TARGET Wood 5", null!, out _));

    [Fact]
    public void AShownNameRanksBeforeAPrefabNameBeforeAnythingElse()
    {
        Assert.Equal(0, RequestPolicy.MatchRank(localizedName: true, prefabOrToken: true));
        Assert.Equal(1, RequestPolicy.MatchRank(localizedName: false, prefabOrToken: true));
        Assert.Equal(2, RequestPolicy.MatchRank(localizedName: false, prefabOrToken: false));
    }

    [Fact]
    public void TheBestRankWinsThenTheLargestAmountThenTheFirstToken()
    {
        Assert.Equal("$item_resin", RequestPolicy.ChooseToken([("$item_amber", 1, 500), ("$item_resin", 0, 1)]));
        Assert.Equal("$item_b", RequestPolicy.ChooseToken([("$item_a", 0, 5), ("$item_b", 0, 9)]));
        Assert.Equal("$item_a", RequestPolicy.ChooseToken([("$item_b", 0, 5), ("$item_a", 0, 5)]));
        Assert.Null(RequestPolicy.ChooseToken([("", 0, 5), (null!, 0, 9)]));
        Assert.Null(RequestPolicy.ChooseToken([]));
    }
}
