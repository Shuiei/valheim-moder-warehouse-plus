using Xunit;

namespace ModerWarehouse.Tests;

public class WarehouseDuplicatePolicyTests
{
    [Fact]
    public void TheCanonicalInputComesFirst()
    {
        Assert.True(WarehouseDuplicatePolicy.Compare(true, 0, "b", false, 9, "a") < 0);
        Assert.True(WarehouseDuplicatePolicy.Compare(false, 9, "a", true, 0, "b") > 0);
    }

    [Fact]
    public void ThenTheOneWithMoreInfrastructure() =>
        Assert.True(WarehouseDuplicatePolicy.Compare(false, 5, "z", false, 2, "a") < 0);

    [Fact]
    public void ThenTheStableIdInOrdinalOrder()
    {
        Assert.True(WarehouseDuplicatePolicy.Compare(false, 1, "a", false, 1, "b") < 0);
        Assert.True(WarehouseDuplicatePolicy.Compare(false, 1, "B", false, 1, "a") < 0);
        Assert.Equal(0, WarehouseDuplicatePolicy.Compare(false, 1, null, false, 1, ""));
    }
}
