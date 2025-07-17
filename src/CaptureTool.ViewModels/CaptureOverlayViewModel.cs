using CaptureTool.Capture;
using CaptureTool.Core.AppController;
using System.Collections.Generic;
using System.Drawing;

namespace CaptureTool.ViewModels;

public sealed partial class CaptureOverlayViewModel : ViewModelBase
{
    private readonly IAppController _appController;
    private readonly List<CaptureOverlayWindowViewModel> _windowViewModels;

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

    public void Close()
    {
        foreach (var windowViewModel in _windowViewModels)
        {
            windowViewModel.CaptureRequested -= CaptureOverlayWindowViewModel_CaptureRequested;
            windowViewModel.PropertyChanged -= CaptureOverlayWindowViewModel_PropertyChanged;
            windowViewModel.Close();
        }
        _windowViewModels.Clear();
    }

    private void CaptureOverlayWindowViewModel_CaptureRequested(object? sender, System.EventArgs e)
    {
        foreach (var windowVM in _windowViewModels)
        {
            var captureArea = windowVM.CaptureArea;
            if (windowVM.Monitor is MonitorCaptureResult monitor && captureArea != Rectangle.Empty)
            {
                _appController.PerformCapture(monitor, captureArea);
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
            else if (e.PropertyName == nameof(CaptureOverlayWindowViewModel.SelectedCaptureModeIndex))
            {
                foreach (var windowViewModel in _windowViewModels)
                {
                    if (windowViewModel == windowVM)
                    {
                        continue;
                    }

                    windowViewModel.SelectedCaptureModeIndex = windowVM.SelectedCaptureModeIndex;
                }
            }
            else if (e.PropertyName == nameof(CaptureOverlayWindowViewModel.SelectedCaptureTypeIndex))
            {
                foreach (var windowViewModel in _windowViewModels)
                {
                    if (windowViewModel == windowVM)
                    {
                        continue;
                    }

                    windowViewModel.SelectedCaptureTypeIndex = windowVM.SelectedCaptureTypeIndex;
                }
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
