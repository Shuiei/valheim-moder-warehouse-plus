using ServersideQoL;
using Xunit;

namespace ModerWarehouse.Tests;

/// <summary>
/// The IN sign: it makes a warehouse, gets a help sign beside it, and is checked against the
/// world's other warehouses (unique names, minimum distance).
/// </summary>
[Collection("Game state")]
public sealed class InputSignTests : IDisposable
{
    private readonly TestWorld _world = new();

    public void Dispose() => _world.Dispose();

    /// <summary>An IN chest at x, z and its IN sign, as ServersideQoL hands it to the warehouse
    /// twice, the help sign's wait apart.</summary>
    private ServersideQoLZDO Input(string marker, float x, float z = 0)
    {
        _world.Chest(x, z);
        ServersideQoLZDO sign = _world.Sign(marker, x, z + 1);
        Settle(sign);
        return sign;
    }

    private void Settle(ServersideQoLZDO sign)
    {
        _world.Process(sign);
        _world.Advance(TimeSpan.FromSeconds(6));
        _world.Process(sign);
    }

    private string Help(ServersideQoLZDO input) =>
        _world.PlainTextOf(_world.HelpSignOf(input) ?? throw new Xunit.Sdk.XunitException("No help sign:\n" + _world.Describe()));

    [Fact]
    public void TheHelpSignAppearsAMomentAfterTheInSign()
    {
        _world.Chest(0, 0);
        ServersideQoLZDO input = _world.Sign("[WH:main:IN]", 0, 1);

        _world.Process(input);
        Assert.Null(_world.HelpSignOf(input));

        _world.Advance(TimeSpan.FromSeconds(6));
        _world.Process(input);
        ServersideQoLZDO help = Assert.IsType<ServersideQoLZDO>(_world.HelpSignOf(input));
        Assert.True(_world.IsPlacedByMod(help));
        Assert.True(_world.IsRemovable(help));
        Assert.Equal(1.2f, help.ZDO.GetPosition().x, 0.01f);
        Assert.StartsWith("WAREHOUSE HELP\nIN: DEPOSIT ALL", Help(input), StringComparison.Ordinal);
    }

    [Fact]
    public void AfterACycleTheHelpSignShowsTheWarehouseAndItsRadius()
    {
        ServersideQoLZDO input = Input("[WH:main:IN] R=60", 0);

        _world.Run("main");

        Assert.StartsWith("WAREHOUSE MAIN\nRADIUS 60 M\nDISTANCE OK", Help(input), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("R=5", 10)]
    [InlineData("R=500", 100)]
    [InlineData("RADIUS=42,5", 42.5f)]
    public void TheRadiusIsKeptBetweenTheMinimumAndTheServersRadius(string payload, float radius)
    {
        ServersideQoLZDO input = Input("[WH:main:IN] " + payload, 0);

        Assert.Equal(radius, input.ZDO.GetFloat("ModerWarehouse.InputRadius", 0));
    }

    [Fact]
    public void ASecondWarehouseWithTheSameNameIsLocked()
    {
        ServersideQoLZDO first = Input("[WH:main:IN]", 0);
        _world.Run("main");
        ServersideQoLZDO second = Input("[WH:main:IN]", 300);
        _world.Run("main");

        Assert.Contains("DISTANCE OK", Help(first), StringComparison.Ordinal);
        Assert.StartsWith("WAREHOUSE LOCKED\nNAME ALREADY EXISTS", Help(second), StringComparison.Ordinal);
        Assert.Contains(_world.Log, l => l == "Warning: IN 'main' rejected: warehouse name already exists.");
    }

    [Fact]
    public void ASecondWarehouseTooCloseIsLocked()
    {
        ServersideQoLZDO first = Input("[WH:main:IN]", 0);
        _world.Run("main");
        ServersideQoLZDO depot = Input("[WH:depot:IN]", 100);
        _world.Run("depot");

        Assert.Contains("DISTANCE OK", Help(first), StringComparison.Ordinal);
        Assert.StartsWith("WAREHOUSE LOCKED\nTOO CLOSE TO MAIN: 100 M (MIN. 210 M)", Help(depot), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AWarehouseFarEnoughAwayIsFine()
    {
        Input("[WH:main:IN]", 0);
        _world.Run("main");
        ServersideQoLZDO depot = Input("[WH:depot:IN]", 300);
        _world.Run("depot");

        Assert.Contains("DISTANCE OK", Help(depot), StringComparison.Ordinal);
    }

    [Fact]
    public void AnInSignWithoutItsChestIsLocked()
    {
        ServersideQoLZDO lonely = _world.Sign("[WH:main:IN]", 0, 0);
        Settle(lonely);
        _world.Run("main");

        Assert.StartsWith("WAREHOUSE LOCKED\nNO INPUT CHEST WITHIN 3 M", Help(lonely), StringComparison.Ordinal);
    }

    [Fact]
    public void ALockedWarehouseSortsNothing()
    {
        Input("[WH:main:IN]", 0);
        _world.Run("main");
        Chest input = _world.Chest(100, 0);
        _world.Sign("[WH:depot:IN]", 100, 1);
        Chest wood = _world.Chest(105, 0);
        _world.Sign("Wood", 105, 0.5f);
        input.Add(TestItems.Make(_world.Items.Wood, 10));

        _world.Run("depot");

        Assert.Equal(10, input.Count(_world.Items.Wood));
        Assert.Empty(wood.Items);
    }

    [Fact]
    public void TheHelpSignIsInTheWarehousesLanguage()
    {
        ServersideQoLZDO input = Input("[WH:main:IN]\nLANG=DE", 0);

        Assert.StartsWith("LAGERHILFE\nIN: ALLES REIN", Help(input), StringComparison.Ordinal);
    }
}
