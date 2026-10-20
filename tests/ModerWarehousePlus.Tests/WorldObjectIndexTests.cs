using System.Runtime.CompilerServices;
using Xunit;

namespace ModerWarehouse.Tests;

public class WorldObjectIndexTests
{
    private readonly List<ZDO> _world = [];

    // What each object is: a chest, a sign, or anything else (missing).
    private readonly Dictionary<ZDO, string> _kinds = [];

    private readonly WorldObjectIndex _index;

    private uint _nextId = 1;

    public WorldObjectIndexTests() =>
        _index = new WorldObjectIndex(() => _world, _world.Contains, z => Kind(z) == "chest", z => Kind(z) == "sign");

    private string? Kind(ZDO zdo) => _kinds.TryGetValue(zdo, out string? kind) ? kind : null;

    /// <summary>An object in the world, of a kind (null: not known yet, as one a client sends).</summary>
    private ZDO Add(string? kind, uint? id = null)
    {
        // Only its id matters here (made without the engine its constructor calls).
        var zdo = (ZDO)RuntimeHelpers.GetUninitializedObject(typeof(ZDO));
        zdo.m_uid = new ZDOID(1, id ?? _nextId++);
        if (kind != null)
        {
            _kinds[zdo] = kind;
        }
        _world.Add(zdo);
        return zdo;
    }

    /// <summary>An object the game creates: in the world, and the index is told.</summary>
    private ZDO Create(string? kind)
    {
        ZDO zdo = Add(kind);
        _index.Created(zdo);
        return zdo;
    }

    private (List<ZDO> Chests, List<ZDO> Signs) Read()
    {
        List<ZDO> chests = [], signs = [];
        _index.Read(chests, signs);
        return (chests, signs);
    }

    [Fact]
    public void TheFirstReadFindsTheWorldsChestsAndSigns()
    {
        ZDO chest = Add("chest"), sign = Add("sign");
        Add("tree");

        var (chests, signs) = Read();

        Assert.Equal([chest], chests);
        Assert.Equal([sign], signs);
    }

    [Fact]
    public void AnObjectCreatedLaterIsFound()
    {
        Read();
        ZDO chest = Create("chest");

        Assert.Equal([chest], Read().Chests);
    }

    [Fact]
    public void AnObjectIsSortedWhenReadSoItsPrefabCanComeAfterItIsCreated()
    {
        Read();
        ZDO sign = Create(null);
        _kinds[sign] = "sign";

        Assert.Equal([sign], Read().Signs);
    }

    [Fact]
    public void ADestroyedObjectIsGone()
    {
        ZDO chest = Add("chest");
        Read();

        _index.Destroyed(chest);

        Assert.Empty(Read().Chests);
    }

    [Fact]
    public void AnObjectNoLongerInTheWorldIsDroppedEvenUntold()
    {
        ZDO chest = Add("chest");
        Read();

        _world.Remove(chest);

        Assert.Empty(Read().Chests);
    }

    [Fact]
    public void AnObjectThatIsNoLongerAChestIsDropped()
    {
        ZDO chest = Add("chest");
        Read();

        _kinds.Remove(chest);

        Assert.Empty(Read().Chests);
    }

    [Fact]
    public void ObjectsComeInIdOrder()
    {
        ZDO later = Add("chest", 20), earlier = Add("chest", 10);

        Assert.Equal([earlier, later], Read().Chests);
    }

    [Fact]
    public void AnObjectCreatedBeforeTheFirstReadIsFoundOnce()
    {
        ZDO chest = Create("chest");

        Assert.Equal([chest], Read().Chests);
    }

    [Fact]
    public void WithoutHearingOfCreationsEveryReadWalksTheWorld()
    {
        _index.FollowsCreations = false;
        Read();
        ZDO chest = Add("chest");

        Assert.Equal([chest], Read().Chests);
    }
}
