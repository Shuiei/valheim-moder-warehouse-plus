using Xunit;

namespace ModerWarehouse.Tests;

public class SpatialLookupTests
{
    private sealed record Point(string Name, float X, float Y, float Z);

    private static SpatialLookup<Point> Lookup(params Point[] points) => new(points, p => p.X, p => p.Y, p => p.Z);

    [Fact]
    public void TheNearestPointInRangeIsFound()
    {
        var lookup = Lookup(new Point("far", 5, 0, 0), new Point("near", 1, 0, 0), new Point("other", 0, 3, 0));
        Assert.Equal("near", lookup.Nearest(0, 0, 0, 9f)!.Name);
    }

    [Fact]
    public void NothingOutOfRangeIsFound() => Assert.Null(Lookup(new Point("far", 5, 0, 0)).Nearest(0, 0, 0, 4f));

    [Fact]
    public void ThePointOnTheRangeIsIn() => Assert.Equal("edge", Lookup(new Point("edge", 2, 0, 0)).Nearest(0, 0, 0, 4f)!.Name);

    [Fact]
    public void PointsInNeighbouringCellsAreFound() =>
        Assert.Equal("across", Lookup(new Point("across", 15.9f, 0, 0)).Nearest(16.1f, 0, 0, 1f)!.Name);

    [Fact]
    public void ALargeRangeSearchesEverything() =>
        Assert.Equal("far", Lookup(new Point("far", 150, 0, -20)).Nearest(0, 0, 0, 200f * 200f)!.Name);

    [Fact]
    public void ExcludedPointsAreSkipped()
    {
        var lookup = Lookup(new Point("near", 1, 0, 0), new Point("next", 2, 0, 0));
        Assert.Equal("next", lookup.Nearest(0, 0, 0, 9f, p => p.Name != "near")!.Name);
    }

    [Fact]
    public void ATieGoesToTheFirstOrOnRequestTheLast()
    {
        var lookup = Lookup(new Point("first", 1, 0, 0), new Point("second", -1, 0, 0));
        Assert.Equal("first", lookup.Nearest(0, 0, 0, 4f)!.Name);
        Assert.Equal("second", lookup.Nearest(0, 0, 0, 4f, preferLastOnTie: true)!.Name);
        Assert.Equal("first", lookup.Nearest(0, 0, 0, 5000f)!.Name);
        Assert.Equal("second", lookup.Nearest(0, 0, 0, 5000f, preferLastOnTie: true)!.Name);
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    public void ANegativeOrUndefinedRangeFindsNothing(float range2) => Assert.Null(Lookup(new Point("here", 0, 0, 0)).Nearest(0, 0, 0, range2));
}
