using Xunit;

namespace ModerWarehouse.Tests;

// The history's days follow the warehouse's clock, which TestWorld sets: "Game state".
[Collection("Game state")]
public class WarehouseStatsHistoryTests
{
    private static int Key(int daysAgo)
    {
        DateTime day = WarehouseClock.UtcNow.Date.AddDays(-daysAgo);
        return day.Year * 10000 + day.Month * 100 + day.Day;
    }

    [Fact]
    public void DepositsAndWithdrawalsAddUpPerDayAndItem()
    {
        var history = new WarehouseStatsHistory();
        history.AddInput("$item_wood", 50);
        history.AddInput("$item_wood", 25);
        history.AddInput("$item_stone", 10);
        history.AddOutput("$item_wood", 20);
        var today = Assert.Single(history.Window(1));
        Assert.Equal(Key(0), today.Key);
        Assert.Equal(85, today.Input);
        Assert.Equal(20, today.Output);
        Assert.Equal(75, today.Inputs["$item_wood"]);
        Assert.Equal(20, today.Outputs["$item_wood"]);
        Assert.True(history.Changed);
    }

    [Theory]
    [InlineData("", 5)]
    [InlineData(" ", 5)]
    [InlineData("$item_wood", 0)]
    [InlineData("$item_wood", -3)]
    public void NothingOrNoItemIsNotCounted(string token, int amount)
    {
        var history = new WarehouseStatsHistory();
        history.AddInput(token, amount);
        Assert.Empty(history.Window(90));
        Assert.False(history.Changed);
    }

    [Fact]
    public void TheHistorySurvivesBeingSaved()
    {
        var history = new WarehouseStatsHistory();
        history.AddInput("$item_wood", 5);
        history.AddInput("odd|name,with=signs", 3);
        history.AddOutput("$item_stone", 2);
        string saved = history.Serialize();
        Assert.False(history.Changed);
        var day = Assert.Single(WarehouseStatsHistory.Parse(saved).Window(1));
        Assert.Equal(8, day.Input);
        Assert.Equal(3, day.Inputs["odd|name,with=signs"]);
        Assert.Equal(2, day.Outputs["$item_stone"]);
        Assert.Equal(saved, WarehouseStatsHistory.Parse(saved).Serialize());
    }

    [Fact]
    public void DaysOlderThanNinetyDaysAreDropped()
    {
        string saved = string.Join("\n",
            $"{Key(0)}|1|0|0|0|0||",
            $"{Key(89)}|2|0|0|0|0||",
            $"{Key(90)}|3|0|0|0|0||");
        var history = WarehouseStatsHistory.Parse(saved);
        Assert.Equal([Key(89), Key(0)], history.Window(90).Select(d => d.Key));
        Assert.Equal(Key(89), history.FirstDayKey);
        Assert.Equal(90, history.TrackedDays);
    }

    [Fact]
    public void BrokenLinesAreSkipped()
    {
        var history = WarehouseStatsHistory.Parse($"garbage\n{Key(0)}|1|2\nnot-a-day|1|0|0|0|0||\n{Key(1)}|4|0|0|0|0|%24item_wood=4,bad,=2|");
        var day = Assert.Single(history.Window(90));
        Assert.Equal(4, day.Inputs["$item_wood"]);
        Assert.Single(day.Inputs);
    }

    [Fact]
    public void WindowsCountBackFromToday()
    {
        string saved = string.Join("\n", Enumerable.Range(0, 14).Select(d => $"{Key(d)}|{d + 1}|0|0|0|0|%24item_wood={d + 1}|"));
        var history = WarehouseStatsHistory.Parse(saved);
        Assert.Equal(7, history.Window(7).Count);
        Assert.Equal(7, history.PreviousWindow(7).Count);
        Assert.Equal(Enumerable.Range(1, 7).Sum(), history.SumItems(7, input: true)["$item_wood"]);
        Assert.Empty(history.SumItems(7, input: false));
        Assert.Single(history.Window(0));
    }

    [Fact]
    public void AnEmptyHistoryTracksNoDays()
    {
        var history = WarehouseStatsHistory.Parse(null);
        Assert.Equal(0, history.TrackedDays);
        Assert.Equal(Key(0), history.FirstDayKey);
    }
}
