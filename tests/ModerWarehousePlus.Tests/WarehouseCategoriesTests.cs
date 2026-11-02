using Xunit;
using Category = ModerWarehouse.WarehouseCategories.Category;

namespace ModerWarehouse.Tests;

public class WarehouseCategoriesTests
{
    [Theory]
    [InlineData("ore", (int)Category.Ore)]
    [InlineData("erz", (int)Category.Ore)]
    [InlineData("barren", (int)Category.Bars)]
    [InlineData("food", (int)Category.Food)]
    [InlineData("met", (int)Category.Meads)]
    [InlineData("munition", (int)Category.Ammo)]
    [InlineData("trophäen", (int)Category.Trophies)]
    [InlineData("weapons", (int)Category.Weapons)]
    [InlineData("rüstung", (int)Category.Armor)]
    [InlineData("werkzeug", (int)Category.Tools)]
    [InlineData("fisch", (int)Category.Fish)]
    [InlineData("misc", (int)Category.Misc)]
    [InlineData("sonstiges", (int)Category.Misc)]
    // The category is passed as a number: the enum is internal to the plugin.
    public void ACategoryLabelIsReadInEnglishOrGerman(string label, int category)
    {
        Assert.True(WarehouseCategories.TryParse(label, out var actual));
        Assert.Equal((Category)category, actual);
    }

    [Theory]
    [InlineData("wood")]
    [InlineData("ORE")]
    [InlineData("")]
    [InlineData(null)]
    public void AnyOtherLabelIsAnItem(string? label) => Assert.False(WarehouseCategories.TryParse(label!, out _));

    [Theory]
    [InlineData("$item_copperore", true)]
    [InlineData("$item_ironscrap", true)]
    [InlineData("$item_blackmetalscrap_old", true)]
    [InlineData("$item_blackcore", false)]
    [InlineData("$item_copper", false)]
    public void OreIsOreAndScrapButNotCores(string token, bool ore) =>
        Assert.Equal(ore, WarehouseCategories.Contains(Category.Ore, token, "Material", 0, 0));

    [Theory]
    [InlineData("$item_copper", "Material", true)]
    [InlineData("$item_flametal", "Material", true)]
    [InlineData("$item_copperore", "Material", false)]
    [InlineData("$item_iron", "OneHandedWeapon", false)]
    public void BarsAreTheSmeltedMetals(string token, string type, bool bar) =>
        Assert.Equal(bar, WarehouseCategories.Contains(Category.Bars, token, type, 0, 0));

    [Fact]
    public void FoodFeedsAndMeadsDoNot()
    {
        Assert.True(WarehouseCategories.Contains(Category.Food, "$item_cookedmeat", "Consumable", 30, 10));
        Assert.True(WarehouseCategories.Contains(Category.Food, "$item_honey", "Consumable", 0, 20));
        Assert.False(WarehouseCategories.Contains(Category.Meads, "$item_cookedmeat", "Consumable", 30, 10));
        Assert.True(WarehouseCategories.Contains(Category.Meads, "$item_mead_frostres", "Consumable", 0, 0));
        Assert.False(WarehouseCategories.Contains(Category.Food, "$item_mead_frostres", "Consumable", 0, 0));
    }

    [Theory]
    [InlineData((int)Category.Ammo, "Ammo")]
    [InlineData((int)Category.Ammo, "AmmoNonEquipable")]
    [InlineData((int)Category.Trophies, "Trophy")]
    [InlineData((int)Category.Weapons, "OneHandedWeapon")]
    [InlineData((int)Category.Weapons, "Bow")]
    [InlineData((int)Category.Weapons, "Attach_Atgeir")]
    [InlineData((int)Category.Armor, "Helmet")]
    [InlineData((int)Category.Armor, "Shield")]
    [InlineData((int)Category.Armor, "Trinket")]
    [InlineData((int)Category.Tools, "Tool")]
    [InlineData((int)Category.Tools, "Torch")]
    [InlineData((int)Category.Fish, "Fish")]
    public void OtherGroupsFollowTheItemType(int category, string type) =>
        Assert.True(WarehouseCategories.Contains((Category)category, "$item_any", type, 0, 0));

    [Theory]
    [InlineData((int)Category.Weapons, "Material")]
    [InlineData((int)Category.Armor, "OneHandedWeapon")]
    [InlineData((int)Category.Misc, "Material")]
    [InlineData((int)Category.None, "Material")]
    public void NothingElseIsInThem(int category, string? type) =>
        Assert.False(WarehouseCategories.Contains((Category)category, "$item_any", type!, 0, 0));
}
