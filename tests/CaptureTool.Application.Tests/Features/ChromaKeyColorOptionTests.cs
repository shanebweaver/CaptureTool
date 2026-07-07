using CaptureTool.Application.Abstractions.Features.ImageEdit.ChromaKey;
using System.Drawing;

namespace CaptureTool.Application.Tests.Features;

[TestClass]
public sealed class ChromaKeyColorOptionTests
{
    [TestMethod]
    public void Empty_ReturnsEmptyOptionWithoutHexString()
    {
        ChromaKeyColorOption option = ChromaKeyColorOption.Empty;

        Assert.IsTrue(option.IsEmpty);
        Assert.AreEqual(Color.Empty, option.Color);
        Assert.AreEqual(string.Empty, option.HexString);
    }

    [TestMethod]
    public void Constructor_WithColor_CreatesUppercaseRgbHexString()
    {
        var color = Color.FromArgb(1, 10, 188);
        var option = new ChromaKeyColorOption(color);

        Assert.IsFalse(option.IsEmpty);
        Assert.AreEqual(color, option.Color);
        Assert.AreEqual("#010ABC", option.HexString);
    }
}
