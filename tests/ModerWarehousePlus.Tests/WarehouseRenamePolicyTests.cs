using Xunit;

namespace ModerWarehouse.Tests;

public class WarehouseRenamePolicyTests
{
    [Fact]
    public void ARememberedDifferentIdIsTheOneRenamedFrom()
    {
        Assert.True(WarehouseRenamePolicy.TryResolvePreviousId(" main ", "harbour", out string previous));
        Assert.Equal("main", previous);
    }

    [Theory]
    [InlineData("main", "MAIN")]
    [InlineData("", "main")]
    [InlineData(null, "main")]
    [InlineData("main", " ")]
    [InlineData("main", null)]
    public void TheSameIdOrNoIdIsNoRename(string? remembered, string? current)
    {
        Assert.False(WarehouseRenamePolicy.TryResolvePreviousId(remembered, current, out string previous));
        Assert.Equal(string.Empty, previous);
    }

    [Theory]
    [InlineData("main", "harbour", false, true)]
    [InlineData("main", "harbour", true, false)]
    [InlineData("main", "Main", false, false)]
    [InlineData("", "harbour", false, false)]
    public void ARenameIsAppliedOnlyToAFreeNewId(string previous, string current, bool targetExists, bool applies) =>
        Assert.Equal(applies, WarehouseRenamePolicy.CanApplyRename(previous, current, targetExists));

    [Theory]
    [InlineData("main", true, true)]
    [InlineData("main", false, false)]
    [InlineData(" ", true, false)]
    public void OnlyARenameOntoATakenIdIsLogged(string previous, bool targetExists, bool logged) =>
        Assert.Equal(logged, WarehouseRenamePolicy.ShouldLogRenameConflict(previous, targetExists));
}
