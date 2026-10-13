using Xunit;

namespace ModerWarehouse.Tests;

public class RequestDisplayPolicyTests
{
    [Theory]
    [InlineData(1f, 2f)]
    [InlineData(5f, 5f)]
    [InlineData(20f, 8f)]
    [InlineData(float.NaN, 8f)]
    [InlineData(float.PositiveInfinity, 8f)]
    [InlineData(float.NegativeInfinity, 8f)]
    public void RequestResultsShowBetweenTwoAndEightSeconds(float configured, float shown) =>
        Assert.Equal(shown, RequestDisplayPolicy.ClampResultSeconds(configured));
}
