using Xunit;

namespace ModerWarehouse.Tests;

[Collection("Game state")]
public sealed class WarehouseLocalizationTests : IDisposable
{
    private readonly TestBepInEx _bepInEx = new();

    private readonly List<string> _log = [];

    private string LocalizationFolder => _bepInEx.Config(Path.Combine("ModerWarehouse", "localization"));

    public void Dispose() => _bepInEx.Dispose();

    [Fact]
    public void GermanTranslatesEveryEnglishTextWithTheSamePlaceholders()
    {
        var report = TranslationGate.Validate(WarehouseLocalization.English(), WarehouseLocalization.German());
        Assert.Empty(report.Missing);
        Assert.Empty(report.Unknown);
        Assert.Empty(report.InvalidPlaceholders);
    }

    [Theory]
    [InlineData("German", "de")]
    [InlineData("deutsch", "de")]
    [InlineData("DE", "de")]
    [InlineData("de_CH", "de")]
    [InlineData("English", "en")]
    [InlineData("fr", "en")]
    [InlineData("", "en")]
    [InlineData(null, "en")]
    public void LanguagesResolveByCodeOrNameAndFallBackToEnglish(string? requested, string language)
    {
        WarehouseLocalization.Initialize(_log.Add);
        Assert.Equal(language, WarehouseLocalization.Resolve(requested));
    }

    [Fact]
    public void TextsFallBackToEnglishThenToTheirKey()
    {
        WarehouseLocalization.Initialize(_log.Add);
        Assert.Equal("NEU", WarehouseLocalization.Text("de", "stats.new"));
        Assert.Equal("NEW", WarehouseLocalization.Text("xx", "stats.new"));
        Assert.Equal("[no.such.key]", WarehouseLocalization.Text("de", "no.such.key"));
        Assert.Equal("FERTIG Holz x5", WarehouseLocalization.Format("de", "request.done", "Holz", 5));
    }

    [Fact]
    public void StatusTextsAreRecognisedInEveryLanguage()
    {
        WarehouseLocalization.Initialize(_log.Add);
        Assert.True(WarehouseLocalization.StartsWithAnyTranslation("DONE Wood x5", "request.done"));
        Assert.True(WarehouseLocalization.StartsWithAnyTranslation("  fertig Holz x5", "request.done"));
        Assert.False(WarehouseLocalization.StartsWithAnyTranslation("DONEWood", "request.done"));
        Assert.False(WarehouseLocalization.StartsWithAnyTranslation("Wood 100", "request.done"));
        Assert.False(WarehouseLocalization.StartsWithAnyTranslation("", "request.done"));
    }

    [Fact]
    public void ATranslationTemplateIsWrittenForTranslators()
    {
        WarehouseLocalization.Initialize(_log.Add);
        string template = File.ReadAllText(Path.Combine(LocalizationFolder, "TranslationTemplate.json"));
        Assert.Contains("\"request.done\"", template);
        Assert.Contains("2 language(s) available: de, en.", Assert.Single(_log));
    }

    [Fact]
    public void ATranslationFileAddsALanguage()
    {
        Directory.CreateDirectory(LocalizationFolder);
        File.WriteAllText(Path.Combine(LocalizationFolder, "fr.json"),
            """{ "language": "fr", "language_name": "Français", "strings": { "request.done": "FAIT {0} x{1}", "made.up": "x" } }""");
        WarehouseLocalization.Initialize(_log.Add);
        Assert.Equal("fr", WarehouseLocalization.Resolve("Français"));
        Assert.Equal("FAIT Bois x5", WarehouseLocalization.Format("fr", "request.done", "Bois", 5));
        Assert.Equal("NEW", WarehouseLocalization.Text("fr", "stats.new"));
        Assert.Contains(_log, line => line.StartsWith("Loaded warehouse translation 'fr'", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("""{ "language": "fr", "language_name": "Français", "strings": { "request.done": "FAIT {0}" } }""", "placeholder mismatch")]
    [InlineData("""{ "language": "de", "language_name": "Deutsch", "strings": { "request.done": "X {0} {1}" } }""", "built in")]
    [InlineData("""{ "language": "french", "language_name": "Français", "strings": { "a": "b" } }""", "not a valid language code")]
    [InlineData("""{ "language": "fr", "strings": { "a": "b" } }""", "'language_name' is required")]
    [InlineData("""{ "language": "fr", "language_name": "Français" }""", "non-empty 'strings'")]
    public void ABadTranslationFileIsIgnoredWithItsReason(string json, string reason)
    {
        Directory.CreateDirectory(LocalizationFolder);
        File.WriteAllText(Path.Combine(LocalizationFolder, "bad.json"), json);
        WarehouseLocalization.Initialize(_log.Add);
        Assert.Contains(_log, line => line.StartsWith("Ignored invalid warehouse translation 'bad.json'", StringComparison.Ordinal) && line.Contains(reason, StringComparison.Ordinal));
        Assert.Equal("en", WarehouseLocalization.Resolve("fr"));
    }
}
