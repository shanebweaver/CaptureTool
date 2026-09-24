using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Tests.Analysis.Memory;

[TestClass]
public sealed class CaptureMemoryTextNormalizerTests
{
    [TestMethod]
    [DataRow("Cafe\u0301", "café", "Cafe\u0301")]
    [DataRow("The ﬁle is ready", "file", "ﬁle")]
    [DataRow("🔎 Ｓｅｔｔｉｎｇｓ", "settings", "Ｓｅｔｔｉｎｇｓ")]
    [DataRow("A settings panel", "setti", "settings")]
    [DataRow("A settings panel", "setings", "settings")]
    [DataRow("portal.contoso.com", "toso", "contoso")]
    public void HighlightRanges_PointToOriginalTextElements(string text, string query, string expected)
    {
        CaptureTextMatch match = CaptureMemoryTextNormalizer.FindMatches(text, query);
        Assert.IsTrue(match.IsMatch);
        var range = match.Ranges.Single();
        Assert.AreEqual(expected, text.Substring(range.Start, range.Length));
    }

    [TestMethod]
    public void RepeatedWords_AreAllHighlighted_WithoutRelaxingRetrievalRules()
    {
        var repeated = CaptureMemoryTextNormalizer.FindMatches("launch, launch! launching", "launch");
        Assert.HasCount(2, repeated.Ranges);
        Assert.IsFalse(CaptureMemoryTextNormalizer.FindMatches("settings panel", "settngs panle").IsMatch);
        Assert.IsFalse(CaptureMemoryTextNormalizer.FindMatches("cat", "cut").IsMatch);
    }

    [TestMethod]
    public void TruncatedSnippet_HighlightsVisibleTerms_WithoutClaimingAFullMatch()
    {
        string source = "Alpha " + new string('x', 300) + " beta";
        string snippet = CaptureMemoryTextNormalizer.CreateSafeSnippet(source, "alpha beta", 100);
        var visible = CaptureMemoryTextNormalizer.FindMatches(snippet, "alpha beta", includePartialHighlights: true);
        Assert.IsFalse(visible.IsMatch);
        Assert.AreEqual("Alpha", snippet.Substring(visible.Ranges.Single().Start, visible.Ranges.Single().Length));
        Assert.IsLessThanOrEqualTo(100, snippet.Length);
    }

    [TestMethod]
    public void InvalidUnicodeOrEmptyQuery_ProducesNoHighlightRanges()
    {
        Assert.IsFalse(CaptureMemoryTextNormalizer.FindMatches("\ud800", "text").IsMatch);
        Assert.IsEmpty(CaptureMemoryTextNormalizer.FindMatches("settings", " ").Ranges);
    }
}
