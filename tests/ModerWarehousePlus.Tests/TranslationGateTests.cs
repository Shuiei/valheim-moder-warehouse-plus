using Xunit;

namespace ModerWarehouse.Tests;

public class TranslationGateTests
{
    [Theory]
    [InlineData("en", true)]
    [InlineData("fra", true)]
    [InlineData("pt_BR", true)]
    [InlineData("sr-Latn", true)]
    [InlineData(" es ", true)]
    [InlineData("e", false)]
    [InlineData("english", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void LanguageCodesAreTwoOrThreeLettersWithOptionalSubtags(string? code, bool valid) =>
        Assert.Equal(valid, TranslationGate.IsValidLanguageCode(code));

    [Fact]
    public void MissingAndBlankStringsAreReportedButAllowed()
    {
        var report = TranslationGate.Validate(
            new Dictionary<string, string> { ["a"] = "A", ["b"] = "B", ["c"] = "C" },
            new Dictionary<string, string> { ["a"] = "x", ["b"] = " " });
        Assert.Equal(["b", "c"], report.Missing);
        Assert.True(report.IsValid);
    }

    [Fact]
    public void UnknownKeysAreReported()
    {
        var report = TranslationGate.Validate(
            new Dictionary<string, string> { ["a"] = "A" },
            new Dictionary<string, string> { ["a"] = "x", ["zz"] = "y", ["b"] = "z" });
        Assert.Equal(["b", "zz"], report.Unknown);
        Assert.True(report.IsValid);
    }

    [Fact]
    public void PlaceholdersMayMoveAndChangeFormatButNotDisappear()
    {
        var reference = new Dictionary<string, string> { ["moved"] = "{0} x{1}", ["formatted"] = "{0}", ["lost"] = "{0} of {1}" };
        var report = TranslationGate.Validate(reference,
            new Dictionary<string, string> { ["moved"] = "{1}x {0}", ["formatted"] = "{0:N0}", ["lost"] = "{0}" });
        Assert.Equal(["lost"], report.InvalidPlaceholders);
        Assert.False(report.IsValid);
    }

    [Fact]
    public void NoDictionaryIsAnEmptyReport()
    {
        var report = TranslationGate.Validate(null, new Dictionary<string, string>());
        Assert.Empty(report.Missing);
        Assert.True(report.IsValid);
    }
}
