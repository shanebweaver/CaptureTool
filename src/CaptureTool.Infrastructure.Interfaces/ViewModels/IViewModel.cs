using CaptureTool.Infrastructure.Interfaces.Loading;
using System.ComponentModel;

namespace CaptureTool.Infrastructure.Interfaces.ViewModels;

/// <summary>
/// Base interface for all ViewModels in the application.
/// </summary>
public interface IViewModel : INotifyPropertyChanged, IDisposable
{
    /// <summary>
    /// Event raised when the load state changes.
    /// </summary>
    event EventHandler<LoadState>? LoadStateChanged;

    /// <summary>
    /// Gets the current load state of the ViewModel.
    /// </summary>
    LoadState LoadState { get; }

    /// <summary>
    /// Gets whether the ViewModel is loaded.
    /// </summary>
    bool IsLoaded { get; }

    /// <summary>
    /// Gets whether the ViewModel is currently loading.
    /// </summary>
    bool IsLoading { get; }

    /// <summary>
    /// Gets whether the ViewModel is ready to load (in Initial state).
    /// </summary>
    bool IsReadyToLoad { get; }
}
