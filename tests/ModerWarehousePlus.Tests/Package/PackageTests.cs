using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace ModerWarehouse.Tests;

/// <summary>
/// The package's files (thunderstore/) agree with the plugin: the version, the changelog, the
/// dependencies it is built against and the names the mod page gives.
/// </summary>
public class PackageTests
{
    private static string Package(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "package", name));

    private static JsonElement Manifest => JsonDocument.Parse(Package("manifest.json")).RootElement;

    [Fact]
    public void TheManifestTakesThePluginsVersionWhenBuilt() =>
        Assert.Equal("@VERSION@", Manifest.GetProperty("version_number").GetString());

    [Fact]
    public void TheChangelogHasASectionForThePluginsVersion() =>
        Assert.Matches(new Regex($"^## {Regex.Escape(WarehousePlugin.Version)}$", RegexOptions.Multiline), Package("CHANGELOG.md"));

    [Fact]
    public void TheManifestFollowsThunderstoresRules()
    {
        Assert.Equal("ModerWarehousePlus", Manifest.GetProperty("name").GetString());
        Assert.InRange(Manifest.GetProperty("description").GetString()!.Length, 1, 250);
        Assert.All(Manifest.GetProperty("dependencies").EnumerateArray(), d => Assert.Matches(@"^[A-Za-z0-9_]+-[A-Za-z0-9_]+-\d+\.\d+\.\d+$", d.GetString()));
    }

    [Fact]
    public void ItDependsOnAtLeastTheServersideQoLItIsBuiltAgainst()
    {
        string dependency = Manifest.GetProperty("dependencies").EnumerateArray().Select(d => d.GetString()!)
            .Single(d => d.StartsWith("ArgusMagnus-ServersideQoL-", StringComparison.Ordinal));
        var required = System.Version.Parse(dependency["ArgusMagnus-ServersideQoL-".Length..]);
        var builtAgainst = typeof(ServersideQoL.ServersideQoLPluginBase).Assembly.GetName().Version!;
        Assert.True(required >= new System.Version(builtAgainst.Major, builtAgainst.Minor, builtAgainst.Build),
            $"The manifest asks for ServersideQoL {required}, the plugin is built against {builtAgainst}.");
    }

    [Fact]
    public void TheModPageNamesTheSettingsFileAndIdsTheCodeUses()
    {
        string readme = Package("README.md");
        Assert.Contains($"`{WarehousePlugin.Guid}.cfg`", readme, StringComparison.Ordinal);
        Assert.Contains("`ch.cris.valheim.moderwarehouse.cfg`", readme, StringComparison.Ordinal);
        Assert.Contains("`BepInEx/config/ModerWarehousePlus/`", readme, StringComparison.Ordinal);
    }
}
