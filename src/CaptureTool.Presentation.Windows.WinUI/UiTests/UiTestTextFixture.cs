using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Application.Edit.Image.TextExtraction;
using CaptureTool.Domain.Analysis.Payloads;
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CaptureTool.Presentation.Windows.WinUI.UiTests;

/// <summary>Word boxes measured by the fixture generator against the actual image typography.</summary>
internal sealed class UiTestTextFixture
{
    private static readonly Lazy<UiTestTextFixture?> Loaded = new(() => UiTestLaunchOptions.TextFixturePath is { } path
        ? JsonSerializer.Deserialize(File.ReadAllText(path), UiTestTextFixtureJsonContext.Default.UiTestTextFixture) : null);
    public static UiTestTextFixture? Current => Loaded.Value;
    public int Width { get; init; }
    public int Height { get; init; }
    public FixtureWord[] Words { get; init; } = [];

    public IReadOnlyList<RecognizedText> SavedText() => Words.Select(word =>
    {
        double x = word.X / Width, y = word.Y / Height;
        return new RecognizedText(word.Text, new(x, y, Math.Min(1 - x, word.Width / Width), Math.Min(1 - y, word.Height / Height)),
            lineIndex: word.LineIndex, wordIndex: word.WordIndex);
    }).ToArray();

    public RecognizedTextDocument EditorText(Size size) => new RecognizedTextDocumentBuilder().Build(size,
        Words.Select(word => new RecognizedTextRegion(word.Text,
            new((float)(word.X / Width * size.Width), (float)(word.Y / Height * size.Height),
                (float)(word.Width / Width * size.Width), (float)(word.Height / Height * size.Height)), word.LineIndex, word.WordIndex)).ToArray(), []);

    public sealed record FixtureWord(string Text, double X, double Y, double Width, double Height, int LineIndex, int WordIndex);
}

[JsonSerializable(typeof(UiTestTextFixture))]
internal sealed partial class UiTestTextFixtureJsonContext : JsonSerializerContext;
