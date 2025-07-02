using CaptureTool.Core.AppController;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels;

public sealed partial class CaptureOverlayViewModel : ViewModelBase
{
    private readonly IAppController _appController;
    private readonly List<CaptureOverlayWindowViewModel> _windowViewModels;

    private bool _showOptions;
    public bool ShowOptions
    {
        get => _showOptions;
        set => Set(ref _showOptions, value);
    }

    public CaptureOverlayViewModel(
        IAppController appController)
    {
        _appController = appController;
        _windowViewModels = [];
    }

    public void AddWindowViewModel(CaptureOverlayWindowViewModel newVM)
    {
        _windowViewModels.Add(newVM);

        newVM.CaptureRequested += CaptureOverlayWindowViewModel_CaptureRequested;
        newVM.PropertyChanged += CaptureOverlayWindowViewModel_PropertyChanged;
    }

    private void CaptureOverlayWindowViewModel_CaptureRequested(object? sender, System.EventArgs e)
    {
        foreach (var windowVM in _windowViewModels)
        {
            if (windowVM.CaptureArea != Rectangle.Empty && windowVM.Monitor != null)
            {
                _appController.RequestCapture(windowVM.Monitor.HMonitor, windowVM.CaptureArea);
                break;
            }
        }
    }

    private void CaptureOverlayWindowViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is CaptureOverlayWindowViewModel windowVM)
        {
            if (e.PropertyName == nameof(CaptureOverlayWindowViewModel.CaptureArea))
            {
                OnCaptureAreaChanged(windowVM);
            }
            else if (e.PropertyName == nameof(CaptureOverlayWindowViewModel.IsActive))
            {
                OnIsActiveChanged();
            }
        }
    }

    private async void OnIsActiveChanged()
    {
        // Check for an active monitor
        bool hasActiveMonitor() => _windowViewModels.FirstOrDefault((vm) => vm.IsActive)?.Monitor != null;
        if (!hasActiveMonitor())
        {
            // Wait and check again. If no active, dismiss the overlay.
            // The machine has 50 milliseconds to assign a new active monitor or the capture overlay will be closed.
            // If the machine is too slow, the capture overlay will close when selecting from any non-primary other monitors.
            await Task.Delay(50);

            if (!hasActiveMonitor())
            {
                // Cleanup, but don't restore focus to main window.
                // This is to ensure Alt+Tab allows focus to go to the new app.
                _appController.CleanupCaptureOverlays();
            }
        }
    }

    private void OnCaptureAreaChanged(CaptureOverlayWindowViewModel windowVM)
    {
        if (!windowVM.CaptureArea.IsEmpty)
        {
            foreach (var tempWindowVM in _windowViewModels)
            {
                if (tempWindowVM != windowVM)
                {
                    tempWindowVM.CaptureArea = Rectangle.Empty;
                }
            }
        }
    }
}
