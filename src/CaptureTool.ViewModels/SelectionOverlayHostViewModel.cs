using CaptureTool.Capture;
using CaptureTool.Common.Sync;
using CaptureTool.Core.AppController;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;

namespace CaptureTool.ViewModels;

public sealed partial class SelectionOverlayHostViewModel : ViewModelBase
{
    private readonly IAppController _appController;
    private readonly List<SelectionOverlayWindowViewModel> _windowViewModels = [];

    public SelectionOverlayHostViewModel(
        IAppController appController)
    {
        _appController = appController;
    }

    public void AddWindowViewModel(SelectionOverlayWindowViewModel newVM)
    {
        if (newVM.IsPrimary)
        {
            newVM.PropertyChanged += OnPrimaryWindowViewModelPropertyChanged;
        }
        else
        {
            newVM.PropertyChanged += OnSecondaryWindowViewModelPropertyChanged;
        }
        _windowViewModels.Add(newVM);
    }

    public void Unload()
    {
        foreach (var windowViewModel in _windowViewModels)
        {
            if (windowViewModel.IsPrimary)
            {
                windowViewModel.PropertyChanged -= OnPrimaryWindowViewModelPropertyChanged;
            }
            else
            {
                windowViewModel.PropertyChanged -= OnSecondaryWindowViewModelPropertyChanged;
            }
        }
        _windowViewModels.Clear();
    }

    private void OnPrimaryWindowViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is SelectionOverlayWindowViewModel windowVM)
        {
            switch (e.PropertyName)
            {
                case nameof(SelectionOverlayWindowViewModel.CaptureArea):
                    OnPrimaryCaptureAreaChanged(windowVM);
                    break;

                case nameof(SelectionOverlayWindowViewModel.SelectedCaptureModeIndex):
                    OnSelectedCaptureModeIndexChanged(windowVM);
                    break;

                case nameof(SelectionOverlayWindowViewModel.SelectedCaptureTypeIndex):
                    OnSelectedCaptureTypeIndexChanged(windowVM);
                    break;
            }
        }
    }

    private void OnSecondaryWindowViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is SelectionOverlayWindowViewModel windowVM)
        {
            switch (e.PropertyName)
            {
                case nameof(SelectionOverlayWindowViewModel.CaptureArea):
                    OnSecondaryCaptureAreaChanged(windowVM);
                    break;
            }
        }
    }

    private void OnPrimaryCaptureAreaChanged(SelectionOverlayWindowViewModel windowVM)
    {
        if (windowVM.CaptureArea.IsEmpty || !windowVM.Monitor.HasValue)
        {
            return;
        }

        SyncHelper.SetProperty(windowVM, _windowViewModels, vm => vm.CaptureArea, Rectangle.Empty);
    }

    private void OnSecondaryCaptureAreaChanged(SelectionOverlayWindowViewModel windowVM)
    {
        if (windowVM.CaptureArea.IsEmpty || !windowVM.Monitor.HasValue)
        {
            return;
        }

        SyncHelper.SetProperty(windowVM, _windowViewModels, vm => vm.CaptureArea, Rectangle.Empty);
    }

    private void OnSelectedCaptureModeIndexChanged(SelectionOverlayWindowViewModel windowVM)
    {
        SyncHelper.SyncProperty(windowVM, _windowViewModels, vm => vm.SelectedCaptureModeIndex);
    }

    private void OnSelectedCaptureTypeIndexChanged(SelectionOverlayWindowViewModel windowVM)
    {
        SyncHelper.SyncProperty(windowVM, _windowViewModels, vm => vm.SelectedCaptureTypeIndex);

        if (windowVM.SelectedCaptureType.CaptureType == CaptureType.AllScreens)
        {
            if (windowVM.SelectedCaptureMode.CaptureMode == CaptureMode.Image)
            {
                _appController.PerformAllScreensCapture();
            }
            else if (windowVM.SelectedCaptureMode.CaptureMode == CaptureMode.Video)
            {
                throw new System.NotImplementedException();
            }
        }
    }
}
