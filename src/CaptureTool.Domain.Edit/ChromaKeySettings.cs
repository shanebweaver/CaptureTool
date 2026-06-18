using System.Drawing;

namespace CaptureTool.Domain.Edit;

public readonly record struct ChromaKeySettings(
    int SelectedColorOptionIndex,
    Color Color,
    int Tolerance,
    int Desaturation)
{
    public static ChromaKeySettings Default { get; } = new(0, Color.Empty, 30, 0);
}
