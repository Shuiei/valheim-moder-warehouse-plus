using Xunit;

namespace ModerWarehouse.Tests;

public class WarehouseScheduleTests
{
    private static readonly DateTime Now = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AWarehouseRunsWhenItsTimeHasCome()
    {
        Assert.True(WarehouseSchedule.CanRun(Now, Now, Now.AddMinutes(-1), force: false, woke: false, dirty: false));
        Assert.True(WarehouseSchedule.CanRun(Now, Now.AddSeconds(-1), Now.AddMinutes(-1), force: false, woke: false, dirty: false));
    }

    [Fact]
    public void ItWaitsForItsTimeOtherwise() =>
        Assert.False(WarehouseSchedule.CanRun(Now, Now.AddSeconds(10), Now.AddMinutes(-1), force: false, woke: false, dirty: false));

    [Fact]
    public void AForcedOrWokenWarehouseRunsAtOnce()
    {
        Assert.True(WarehouseSchedule.CanRun(Now, Now.AddMinutes(5), Now, force: true, woke: false, dirty: false));
        Assert.True(WarehouseSchedule.CanRun(Now, Now.AddMinutes(5), Now, force: false, woke: true, dirty: false));
    }

    [Fact]
    public void AChangedWarehouseRunsASecondAfterItsLastRun()
    {
        Assert.True(WarehouseSchedule.CanRun(Now, Now.AddMinutes(5), Now.AddSeconds(-1), force: false, woke: false, dirty: true));
        Assert.False(WarehouseSchedule.CanRun(Now, Now.AddMinutes(5), Now.AddMilliseconds(-500), force: false, woke: false, dirty: true));
    }
}
