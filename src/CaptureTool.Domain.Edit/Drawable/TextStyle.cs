using System.Drawing;

namespace CaptureTool.Domain.Edit.Drawable;

public readonly record struct TextStyle(
    Color FontColor,
    Color BackgroundColor,
    string FontFamily,
    int FontSize);
