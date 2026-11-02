using Xunit;

namespace ModerWarehouse.Tests;

public class WarehouseDuplicatePolicyTests
{
    [Fact]
    public void TheCanonicalInputComesFirst()
    {
        Assert.True(WarehouseDuplicatePolicy.Compare(true, "b", false, "a") < 0);
        Assert.True(WarehouseDuplicatePolicy.Compare(false, "a", true, "b") > 0);
    }

    [Fact]
    public void ThenTheStableIdInOrdinalOrder()
    {
        Assert.True(WarehouseDuplicatePolicy.Compare(false, "a", false, "b") < 0);
        Assert.True(WarehouseDuplicatePolicy.Compare(false, "B", false, "a") < 0);
        Assert.Equal(0, WarehouseDuplicatePolicy.Compare(false, null, false, ""));
    }
}
