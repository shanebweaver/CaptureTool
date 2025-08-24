using CaptureTool.Capture;
using CaptureTool.Common.Sync;
using CaptureTool.Core.AppController;
using CaptureTool.FeatureManagement;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;

namespace CaptureTool.ViewModels;

public sealed partial class CaptureOverlayViewModel : ViewModelBase
{
    private readonly IAppController _appController;
    private readonly IFeatureManager _featureManager;
    private readonly List<CaptureOverlayWindowViewModel> _windowViewModels = [];

    public CaptureOverlayViewModel(
        IAppController appController,
        IFeatureManager featureManager)
    {
        _appController = appController;
        _featureManager = featureManager;
    }

    public void AddWindowViewModel(CaptureOverlayWindowViewModel newVM)
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

    public void TransitionToVideoMode(MonitorCaptureResult monitor, Rectangle area)
    {
        Trace.Assert(_featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture));

        foreach (var windowViewModel in _windowViewModels)
        {
            if (windowViewModel.Monitor.HasValue && windowViewModel.Monitor.Value.HMonitor == monitor.HMonitor)
            {
                windowViewModel.CaptureArea = area;
            }

            windowViewModel.TransitionToVideoModeCommand.Execute();
        }
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
        if (sender is CaptureOverlayWindowViewModel windowVM)
        {
            switch (e.PropertyName)
            {
                case nameof(CaptureOverlayWindowViewModel.CaptureArea):
                    OnCaptureAreaChanged(windowVM);
                    break;

                case nameof(CaptureOverlayWindowViewModel.SelectedCaptureMode):
                    OnSelectedCaptureModeChanged(windowVM);
                    break;

                case nameof(CaptureOverlayWindowViewModel.SelectedCaptureTypeIndex):
                    OnSelectedCaptureTypeIndexChanged(windowVM);
                    break;

                case nameof(CaptureOverlayWindowViewModel.ActiveCaptureMode):
                    OnActiveCaptureModeChanged(windowVM);
                    break;
            }
        }
    }

    private void OnSecondaryWindowViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is CaptureOverlayWindowViewModel windowVM)
        {
            switch (e.PropertyName)
            {
                case nameof(CaptureOverlayWindowViewModel.CaptureArea):
                    OnCaptureAreaChanged(windowVM);
                    break;
            }
        }
    }

    private void OnCaptureAreaChanged(CaptureOverlayWindowViewModel windowVM)
    {
        if (windowVM.CaptureArea.IsEmpty || !windowVM.Monitor.HasValue)
        {
            return;
        }

        SyncHelper.SetProperty(windowVM, _windowViewModels, vm => vm.CaptureArea, Rectangle.Empty);

        if (windowVM.SelectedCaptureMode == CaptureMode.Video)
        {
            Trace.Assert(_featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture));
            _appController.PrepareForVideoCapture(windowVM.Monitor.Value, windowVM.CaptureArea);
        }
    }

    private void OnSelectedCaptureModeChanged(CaptureOverlayWindowViewModel windowVM)
    {
        SyncHelper.SyncProperty(windowVM, _windowViewModels, vm => vm.SelectedCaptureModeIndex);
    }

    private void OnSelectedCaptureTypeIndexChanged(CaptureOverlayWindowViewModel windowVM)
    {
        SyncHelper.SyncProperty(windowVM, _windowViewModels, vm => vm.SelectedCaptureTypeIndex);

        if (windowVM.SelectedCaptureType == CaptureType.AllScreens)
        {
            if (windowVM.SelectedCaptureMode == CaptureMode.Image)
            {
                _appController.PerformAllScreensCapture();
            }
            else if (windowVM.SelectedCaptureMode == CaptureMode.Video)
            {
                throw new System.NotImplementedException();
            }
        }
    }

    private void OnActiveCaptureModeChanged(CaptureOverlayWindowViewModel windowVM)
    {
        SyncHelper.SyncProperty(windowVM, _windowViewModels, vm => vm.ActiveCaptureMode);
    }
}
