using Xunit;

namespace ModerWarehouse.Tests;

public class SleepDisplayPolicyTests
{
    [Fact]
    public void TheLabelIsFollowedByZsUpToTheCells()
    {
        var layout = SleepDisplayPolicy.Create("SLEEP", 8);
        Assert.Equal("SLEEPZZZ", layout.Text);
        Assert.Equal(5, layout.LabelLength);
    }

    [Fact]
    public void ATranslationEndingInZsDoesNotCountThemAsLabel()
    {
        var layout = SleepDisplayPolicy.Create(" Schlafzz ", 8);
        Assert.Equal("SchlafZZ", layout.Text);
        Assert.Equal(6, layout.LabelLength);
    }

    [Fact]
    public void ALongLabelIsCutToTheCells()
    {
        var layout = SleepDisplayPolicy.Create("VERYLONGWORD", 4);
        Assert.Equal("VERY", layout.Text);
        Assert.Equal(4, layout.LabelLength);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NoCellsShowNothing(int cells)
    {
        var layout = SleepDisplayPolicy.Create("SLEEP", cells);
        Assert.Equal(string.Empty, layout.Text);
        Assert.Equal(0, layout.LabelLength);
    }

    [Fact]
    public void NoLabelShowsOnlyZs() => Assert.Equal("ZZZ", SleepDisplayPolicy.Create(null, 3).Text);
}
