using Xunit;

namespace ModerWarehouse.Tests;

public class GlobalScanPolicyTests
{
    private static readonly DateTime Now = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TheTopologyIsCheckedAgainWhenAnInMarkerIsNewOrChanged()
    {
        Assert.True(GlobalScanPolicy.ShouldValidateTopology(unknown: true, signatureChanged: false, Now, Now));
        Assert.True(GlobalScanPolicy.ShouldValidateTopology(unknown: false, signatureChanged: true, Now, Now));
    }

    [Fact]
    public void OtherwiseEveryThirtyMinutes()
    {
        Assert.False(GlobalScanPolicy.ShouldValidateTopology(false, false, Now, Now.AddMinutes(-29)));
        Assert.True(GlobalScanPolicy.ShouldValidateTopology(false, false, Now, Now.AddMinutes(-30)));
    }

    [Fact]
    public void OtherWarehousesStockIsReadAtMostEveryTenSeconds()
    {
        Assert.False(GlobalScanPolicy.ShouldRefreshWorldStock(Now, Now.AddSeconds(-9)));
        Assert.True(GlobalScanPolicy.ShouldRefreshWorldStock(Now, Now.AddSeconds(-10)));
        Assert.True(GlobalScanPolicy.ShouldRefreshWorldStock(Now, DateTime.MinValue));
    }
}
