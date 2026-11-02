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

    [Theory]
    [InlineData("WOOD2", "WOOD", 2)]
    [InlineData("Holz 13", "Holz", 13)]
    [InlineData(" Fine wood99 ", "Fine wood", 99)]
    public void ANumberedLabelNamesItsItemAndPriority(string text, string label, int priority)
    {
        Assert.True(WarehouseContainerPolicy.TryParseNumberedLabel(text, out string actualLabel, out int actualPriority));
        Assert.Equal(label, actualLabel);
        Assert.Equal(priority, actualPriority);
    }

    [Theory]
    [InlineData("WOOD")]
    [InlineData("WOOD0")]
    [InlineData("WOOD100")]
    [InlineData("42")]
    [InlineData("")]
    [InlineData(null)]
    public void OtherLabelsAreNotNumbered(string? text) =>
        Assert.False(WarehouseContainerPolicy.TryParseNumberedLabel(text!, out _, out _));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    [InlineData(99, 99)]
    [InlineData(100, 1)]
    public void AnUnnumberedOrOddPriorityCountsAsTheFirst(int stored, int effective) =>
        Assert.Equal(effective, WarehouseContainerPolicy.EffectiveFillPriority(stored));

    [Theory]
    [InlineData("...", true)]
    [InlineData(" ... ", true)]
    [InlineData("..", false)]
    [InlineData("Wood", false)]
    [InlineData(null, false)]
    public void ThreeDotsReleaseAChest(string? text, bool reset) =>
        Assert.Equal(reset, WarehouseContainerPolicy.IsExplicitLabelReset(text!));
}
