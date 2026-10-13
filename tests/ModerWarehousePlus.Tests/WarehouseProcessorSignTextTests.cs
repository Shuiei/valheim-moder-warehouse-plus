using Xunit;

namespace ModerWarehouse.Tests;

/// <summary>
/// How a sign's text is read: warehouse markers, labels, blank and ContainerSigns signs (WarehouseProcessor).
/// </summary>
[Collection("Item names")]
public class WarehouseProcessorSignTextTests
{
    [Theory]
    [InlineData("[WH:main:IN]", "main", "IN", "")]
    [InlineData("  [wh:Harbour_2:req]", "Harbour_2", "req", "")]
    [InlineData("[WH:main:IN] R=60\nLANG=DE", "main", "IN", " R=60\nLANG=DE")]
    [InlineData("[WH:a-b:STATS]", "a-b", "STATS", "")]
    public void AMarkerNamesAWarehouseAndARole(string text, string id, string role, string payload)
    {
        var match = WarehouseProcessor.Marker.Match(text);
        Assert.True(match.Success);
        Assert.Equal(id, match.Groups["id"].Value);
        Assert.Equal(role, match.Groups["role"].Value);
        Assert.Equal(payload, match.Groups["payload"].Value);
    }

    [Theory]
    [InlineData("[WH:main:FOO]")]
    [InlineData("[WH:ma in:IN]")]
    [InlineData("Wood [WH:main:IN]")]
    [InlineData("[WH::IN]")]
    public void AnythingElseIsNoMarker(string text) => Assert.DoesNotMatch(WarehouseProcessor.Marker, text);

    [Fact]
    public void TheMaintenanceMarkersStandAlone()
    {
        Assert.Matches(WarehouseProcessor.IgnoreMarker, " [wh:ignore] ");
        Assert.Matches(WarehouseProcessor.DeleteMarker, "[WH:DELETE]");
        Assert.Matches(WarehouseProcessor.CleanMarker, "[WH:CLEAN]");
        Assert.DoesNotMatch(WarehouseProcessor.IgnoreMarker, "[WH:IGNORE] please");
    }

    [Theory]
    [InlineData("<color=white>Wood", "Wood")]
    [InlineData("<i ls></i>Wood 5<br>Stone 2<i le></i>", "Wood 5Stone 2")]
    [InlineData(null, "")]
    public void PlainTextHasNoTags(string? text, string plain) => Assert.Equal(plain, WarehouseProcessor.PlainText(text));

    [Theory]
    [InlineData("<i ls></i>Wood 5<i le></i>", true)]
    [InlineData("Chest <i ls></i><i le></i>", true)]
    [InlineData("Wood 5", false)]
    [InlineData(null, false)]
    public void AContainerSignsListIsRecognised(string? text, bool isList) =>
        Assert.Equal(isList, WarehouseProcessor.IsContainerSignsList(text));

    [Theory]
    [InlineData("...", true)]
    [InlineData("•", true)]
    [InlineData("", true)]
    [InlineData("  - ", true)]
    [InlineData(null, true)]
    [InlineData("Wood", false)]
    [InlineData("7", false)]
    public void ABlankSignHasNoLetterOrDigit(string? text, bool blank) => Assert.Equal(blank, WarehouseProcessor.IsBlankLabel(text));

    [Theory]
    [InlineData("<color=white>Wood", true)]
    [InlineData("Fine wood 20", true)]
    [InlineData("<color=white>...", false)]
    [InlineData("•", false)]
    [InlineData("<i ls></i>Wood 5<i le></i>", false)]
    [InlineData("[WH:main:REQ]", false)]
    [InlineData("[WH:IGNORE]", false)]
    [InlineData("[WH:DELETE]", false)]
    [InlineData("[WH:CLEAN]", false)]
    public void ALabelNamesWhatTheChestHolds(string text, bool isLabel) => Assert.Equal(isLabel, WarehouseProcessor.IsLabelText(text));

    [Theory]
    [InlineData("Mead: Frost resistance!", "meadfrostresistance")]
    [InlineData("<color=red>Holz", "holz")]
    [InlineData("Öl 2", "öl2")]
    [InlineData(null, "")]
    public void NamesAreComparedAsLowerCaseLettersAndDigits(string? name, string normalized) =>
        Assert.Equal(normalized, WarehouseProcessor.Normalize(name));

    [Fact]
    public void AFrozenContentsLineMeansItsItem()
    {
        using var items = new KnownItems("Wood", "Fine wood", "Iron nails 2");
        Assert.True(WarehouseProcessor.TryStripContentCount("Wood 521", out string name));
        Assert.Equal("Wood", name);
        Assert.True(WarehouseProcessor.TryStripContentCount(" Fine wood 20 ", out name));
        Assert.Equal("Fine wood", name);
    }

    [Theory]
    [InlineData("Wood")]
    [InlineData("Unknown thing 5")]
    [InlineData("Iron nails 2")]
    [InlineData("5")]
    public void OnlyAKnownItemWithACountIsStripped(string text)
    {
        using var items = new KnownItems("Wood", "Iron nails 2");
        Assert.False(WarehouseProcessor.TryStripContentCount(text, out string name));
        Assert.Null(name);
    }

    /// <summary>
    /// Item names known for the test (WarehouseProcessor.AliasOwners, filled from the game's items in
    /// the game), put back as they were afterwards.
    /// </summary>
    private sealed class KnownItems : IDisposable
    {
        private readonly Dictionary<string, HashSet<string>> _saved = new(WarehouseProcessor.AliasOwners);

        public KnownItems(params string[] names)
        {
            WarehouseProcessor.AliasOwners.Clear();
            foreach (string name in names)
            {
                WarehouseProcessor.AliasOwners[WarehouseProcessor.Normalize(name)] = ["$item_" + WarehouseProcessor.Normalize(name)];
            }
        }

        public void Dispose()
        {
            WarehouseProcessor.AliasOwners.Clear();
            foreach (var pair in _saved)
            {
                WarehouseProcessor.AliasOwners[pair.Key] = pair.Value;
            }
        }
    }
}

[CollectionDefinition("Item names", DisableParallelization = true)]
public sealed class ItemNamesCollection;
