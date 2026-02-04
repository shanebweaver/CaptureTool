namespace CaptureTool.Application.Interfaces.ViewModels;

/// <summary>
/// Tracks the source of capture mode/type updates to prevent event loops and enable intelligent propagation.
/// </summary>
public enum SelectionUpdateSource
{
    /// <summary>
    /// Update initiated by user interaction with the toolbar on the primary window.
    /// </summary>
    UserInteraction,

    /// <summary>
    /// Update propagated from another window by the host ViewModel.
    /// </summary>
    Propagation,

    /// <summary>
    /// Programmatic update during initialization or load.
    /// </summary>
    Programmatic
}
