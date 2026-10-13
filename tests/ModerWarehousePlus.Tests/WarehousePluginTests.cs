using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using Xunit;

namespace ModerWarehouse.Tests;

[Collection("Game state")]
public sealed class WarehousePluginTests : IDisposable
{
    private const string OriginalGuid = "ch.cris.valheim.moderwarehouse";

    private readonly TestBepInEx _bepInEx = new();

    private readonly List<string> _log = [];

    public void Dispose() => _bepInEx.Dispose();

    private void CopyOriginalSettings(ConfigFile file) => WarehousePlugin.CopyOriginalSettings(file, _log.Add, x => _log.Add("warning: " + x));

    private string OwnSettings => _bepInEx.Config(WarehousePlugin.Guid + ".cfg");

    private string OriginalSettings => _bepInEx.Config(OriginalGuid + ".cfg");

    [Fact]
    public void ItHasItsOwnIdAndRefusesToLoadBesideTheOriginal()
    {
        var plugin = typeof(WarehousePlugin).GetCustomAttribute<BepInPlugin>()!;
        Assert.Equal("Tie.ModerWarehousePlus", plugin.GUID);
        Assert.Equal(WarehousePlugin.Version, plugin.Version.ToString());
        Assert.Contains(typeof(WarehousePlugin).GetCustomAttributes<BepInIncompatibility>(), x => x.IncompatibilityGUID == OriginalGuid);
    }

    [Fact]
    public void TheOriginalSettingsAreCopiedOnTheFirstStart()
    {
        File.WriteAllText(OriginalSettings, "[Warehouse]\nRadius = 22\n");
        var file = new ConfigFile(OwnSettings, saveOnInit: false);
        CopyOriginalSettings(file);
        Assert.Equal(22f, file.Bind("Warehouse", "Radius", 100f).Value);
        Assert.True(File.Exists(OriginalSettings));
        Assert.Equal("Settings copied from ch.cris.valheim.moderwarehouse.cfg to Tie.ModerWarehousePlus.cfg.", Assert.Single(_log));
    }

    [Fact]
    public void ItsOwnSettingsAreNeverReplaced()
    {
        File.WriteAllText(OriginalSettings, "[Warehouse]\nRadius = 22\n");
        File.WriteAllText(OwnSettings, "[Warehouse]\nRadius = 60\n");
        var file = new ConfigFile(OwnSettings, saveOnInit: false);
        CopyOriginalSettings(file);
        Assert.Equal(60f, file.Bind("Warehouse", "Radius", 100f).Value);
        Assert.Empty(_log);
    }

    [Fact]
    public void WithoutTheOriginalThereIsNothingToCopy()
    {
        var file = new ConfigFile(OwnSettings, saveOnInit: false);
        CopyOriginalSettings(file);
        Assert.False(File.Exists(OwnSettings));
    }

    [Fact]
    public void SettingsKeptInAnotherFileAreLeftAlone()
    {
        // ServersideQoL's UnifiedConfig keeps every mod's settings in its own file.
        File.WriteAllText(OriginalSettings, "[Warehouse]\nRadius = 22\n");
        string unified = _bepInEx.Config("ArgusMagnus.ServersideQoL.cfg");
        CopyOriginalSettings(new ConfigFile(unified, saveOnInit: false));
        Assert.Empty(_log);
        Assert.False(File.Exists(unified));
        Assert.False(File.Exists(OwnSettings));
    }

    [Fact]
    public void CopyIfMissingCopiesOnceIntoANewFolder()
    {
        string source = _bepInEx.Plugins(Path.Combine("ModerWarehouse", "ModerWarehouse-stock-cache.tsv"));
        string destination = _bepInEx.Config(Path.Combine("ModerWarehousePlus", "ModerWarehouse-stock-cache.tsv"));
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        File.WriteAllText(source, "old");
        Assert.True(WarehousePlugin.CopyIfMissing(source, destination));
        Assert.Equal("old", File.ReadAllText(destination));
        File.WriteAllText(source, "newer");
        Assert.False(WarehousePlugin.CopyIfMissing(source, destination));
        Assert.Equal("old", File.ReadAllText(destination));
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void CopyIfMissingWithoutSourceDoesNothing() =>
        Assert.False(WarehousePlugin.CopyIfMissing(_bepInEx.Config("missing.tsv"), _bepInEx.Config("copy.tsv")));
}
