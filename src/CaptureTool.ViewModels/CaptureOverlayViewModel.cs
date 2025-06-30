using CaptureTool.Core.AppController;
using System.Collections.Generic;
using System.Drawing;

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
                _appController.RequestCapture(windowVM.Monitor, windowVM.CaptureArea);
                break;
            }
        }
    }

    private void CaptureOverlayWindowViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CaptureOverlayWindowViewModel.CaptureArea) && sender is CaptureOverlayWindowViewModel windowVM && !windowVM.CaptureArea.IsEmpty)
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
