using Microsoft.UI.Xaml.Controls;

namespace CaptureTool.UI.Windows.Xaml.Controls;

public sealed partial class CanvasSelectionOverlay : Control
{
    // Crop:
    // SelectionArea (crop rect)
    // Resizable with anchors = true
    // Drawable selection area = false
    // Movable selection area = true
    // Anchor shape = crop anchors

    // Image selection overlay:
    // SelectionArea (image rect)
    // Resizable with anchors = false
    // Drawable selection area = true
    // Movable selection area = false

    // Video selection overlay:
    // SelectionArea (video rect)
    // Resizable with anchors = true
    // Drawable selection area = false
    // Movable selection area = true
    // Anchor shape = video anchors

    public CanvasSelectionOverlay()
    {
        DefaultStyleKey = typeof(CanvasSelectionOverlay);
    }
}
