using Xunit;

namespace ModerWarehouse.Tests;

public class WarehouseContainerPolicyTests
{
    [Theory]
    [InlineData("piece_chest_private", true)]
    [InlineData("Piece_Chest_Private", true)]
    [InlineData("personal_chest", true)]
    [InlineData("piece_chest_wood", false)]
    [InlineData("piece_chest_blackmetal", false)]
    [InlineData(null, false)]
    public void PrivateChestsAreRecognisedByName(string? prefab, bool isPrivate) =>
        Assert.Equal(isPrivate, WarehouseContainerPolicy.IsPrivatePrefab(prefab));

    [Fact]
    public void ASignedOrdinaryChestIsManaged() =>
        Assert.True(WarehouseContainerPolicy.CanManage(endpoint: false, ignored: false, isPrivate: false, inUse: false, hasStorageSign: true));

    [Theory]
    [InlineData(true, false, false, false, true)]
    [InlineData(false, true, false, false, true)]
    [InlineData(false, false, true, false, true)]
    [InlineData(false, false, false, true, true)]
    [InlineData(false, false, false, false, false)]
    public void IOChestsIgnoredPrivateOpenOrUnsignedChestsAreNotManaged(bool endpoint, bool ignored, bool isPrivate, bool inUse, bool hasStorageSign) =>
        Assert.False(WarehouseContainerPolicy.CanManage(endpoint, ignored, isPrivate, inUse, hasStorageSign));

    [Theory]
    // source, wanted, max stack, room in matching stacks, empty slots -> moved
    [InlineData(10, 100, 50, 0, 1, 10)]
    [InlineData(80, 30, 50, 0, 1, 30)]
    [InlineData(80, 100, 50, 5, 0, 5)]
    [InlineData(80, 100, 50, 5, 1, 55)]
    [InlineData(80, 100, 50, 0, 0, 0)]
    [InlineData(10, 10, 0, 0, 3, 3)]
    [InlineData(-5, 10, 50, 0, 1, 0)]
    [InlineData(10, -5, 50, 0, 1, 0)]
    [InlineData(10, 10, 50, -5, -1, 0)]
    public void TransferAmountIsLimitedBySourceWantedAndRoom(int source, int wanted, int maxStack, int matchingSpace, int emptySlots, int moved) =>
        Assert.Equal(moved, WarehouseContainerPolicy.TransferAmount(source, wanted, maxStack, matchingSpace, emptySlots));
}
