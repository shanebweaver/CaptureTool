using CaptureTool.Common;
using CaptureTool.Common.Sync;
using CaptureTool.Core.AppController;
using CaptureTool.Core.Navigation;
using CaptureTool.Domains.Capture.Interfaces;
using System.ComponentModel;
using System.Drawing;

namespace CaptureTool.ViewModels;

public sealed partial class SelectionOverlayHostViewModel : ViewModelBase
{
    private readonly List<SelectionOverlayWindowViewModel> _windowViewModels = [];

    private readonly IAppNavigation _appNavigation;
    private readonly IAppController _appController;
    private readonly IImageCaptureHandler _imageCaptureHandler;

    public event EventHandler? AllScreensCaptureRequested;

    public SelectionOverlayHostViewModel(
        IAppNavigation appNavigation,
        IAppController appController,
        IImageCaptureHandler imageCaptureHandler)
    {
        _appNavigation = appNavigation;
        _appController = appController;
        _imageCaptureHandler = imageCaptureHandler;
    }

    public void AddWindowViewModel(SelectionOverlayWindowViewModel newVM, bool isPrimary = false)
    {
        if (isPrimary)
        {
            newVM.PropertyChanged += OnPrimaryWindowViewModelPropertyChanged;
        }
        else
        {
            newVM.PropertyChanged += OnSecondaryWindowViewModelPropertyChanged;
        }
        _windowViewModels.Add(newVM);
    }

    public override void Dispose()
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

        CaptureType? selectedCaptureType = windowVM.GetSelectedCaptureType();
        if (selectedCaptureType == CaptureType.AllScreens)
        {
            if (windowVM.SelectedCaptureMode.CaptureMode == CaptureMode.Image)
            {
                AllScreensCaptureRequested?.Invoke(this, EventArgs.Empty);

                //ImageFile image = _imageCaptureHandler.PerformAllScreensCapture(); // TODO: Use MultiMonitor campture instead and pass ion the monitors from the host...
                //_appNavigation.GoToImageEdit(image);
            }
            else if (windowVM.SelectedCaptureMode.CaptureMode == CaptureMode.Video)
            {
            }
        }
    }
}
