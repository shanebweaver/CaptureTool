using System.Collections.Generic;
using System.Drawing;

namespace CaptureTool.ViewModels;

public sealed partial class CaptureOverlayViewModel : ViewModelBase
{
    private readonly List<CaptureOverlayWindowViewModel> _windowViewModels = [];

    public CaptureOverlayViewModel()
    {
    }

    public void AddWindowViewModel(CaptureOverlayWindowViewModel newVM)
    {
        if (newVM.IsPrimary)
        {
            newVM.PropertyChanged += CaptureOverlayWindowViewModel_PropertyChanged;
        }
        _windowViewModels.Add(newVM);
    }

    public void Unload()
    {
        foreach (var windowViewModel in _windowViewModels)
        {
            windowViewModel.PropertyChanged -= CaptureOverlayWindowViewModel_PropertyChanged;
        }
        _windowViewModels.Clear();
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
