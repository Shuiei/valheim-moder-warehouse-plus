using Xunit;

namespace ModerWarehouse.Tests;

public class WarehouseDistancePolicyTests
{
    [Theory]
    [InlineData(209.9f, 210f, true)]
    [InlineData(210f, 210f, false)]
    [InlineData(500f, 210f, false)]
    public void AnInputCloserThanTheMinimumIsTooClose(float distance, float minimum, bool tooClose) =>
        Assert.Equal(tooClose, WarehouseDistancePolicy.IsTooClose(distance, minimum));
}
