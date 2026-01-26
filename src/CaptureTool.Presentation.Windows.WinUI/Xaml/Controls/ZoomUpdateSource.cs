namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

/// <summary>
/// Tracks the source of zoom updates to prevent event loops and enable intelligent propagation.
/// </summary>
public enum ZoomUpdateSource
{
    /// <summary>
    /// Zoom initiated by user dragging the zoom slider.
    /// </summary>
    Slider,

    /// <summary>
    /// Zoom initiated by user gesture on canvas (CTRL+MouseWheel).
    /// </summary>
    CanvasGesture,

    /// <summary>
    /// Zoom calculated and applied by the Zoom and Center button.
    /// </summary>
    ZoomAndCenter,

    /// <summary>
    /// Programmatic zoom update (initial load, etc.).
    /// </summary>
    Programmatic
}
