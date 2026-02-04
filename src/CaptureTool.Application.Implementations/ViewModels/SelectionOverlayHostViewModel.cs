using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Implementations.ViewModels;
using System.ComponentModel;
using System.Drawing;

namespace CaptureTool.Application.Implementations.ViewModels;

public sealed partial class SelectionOverlayHostViewModel : ViewModelBase, ISelectionOverlayHostViewModel
{
    private readonly List<ISelectionOverlayWindowViewModel> _windowViewModels = [];
    private bool _isPropagatingChanges;

    public event EventHandler? AllScreensCaptureRequested;

    public void UpdateOptions(CaptureOptions options)
    {
        foreach (var windowVM in _windowViewModels)
        {
            windowVM.UpdateCaptureOptionsCommand.Execute(options);
        }
    }

    public void AddWindowViewModel(ISelectionOverlayWindowViewModel newVM, bool isPrimary = false)
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
        // Unregister event handlers first
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

            // Dispose each ViewModel to release their Monitor references
            windowViewModel.Dispose();
        }

        _windowViewModels.Clear();

        base.Dispose();
    }

    private void OnPrimaryWindowViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Prevent propagation cycles by ignoring property changes that we triggered
        if (_isPropagatingChanges)
        {
            return;
        }

        if (sender is ISelectionOverlayWindowViewModel windowVM)
        {
            switch (e.PropertyName)
            {
                case nameof(ISelectionOverlayWindowViewModel.CaptureArea):
                    OnPrimaryCaptureAreaChanged(windowVM);
                    break;

                case nameof(ISelectionOverlayWindowViewModel.SelectedCaptureModeIndex):
                    OnSelectedCaptureModeIndexChanged(windowVM);
                    break;

                case nameof(ISelectionOverlayWindowViewModel.SelectedCaptureTypeIndex):
                    OnSelectedCaptureTypeIndexChanged(windowVM);
                    break;
            }
        }
    }

    private void OnSecondaryWindowViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Prevent propagation cycles by ignoring property changes that we triggered
        if (_isPropagatingChanges)
        {
            return;
        }

        if (sender is ISelectionOverlayWindowViewModel windowVM)
        {
            switch (e.PropertyName)
            {
                case nameof(ISelectionOverlayWindowViewModel.CaptureArea):
                    OnSecondaryCaptureAreaChanged(windowVM);
                    break;
            }
        }
    }

    private void OnPrimaryCaptureAreaChanged(ISelectionOverlayWindowViewModel windowVM)
    {
        if (windowVM.CaptureArea.IsEmpty || !windowVM.Monitor.HasValue)
        {
            return;
        }

        _isPropagatingChanges = true;
        try
        {
            foreach (var target in _windowViewModels)
            {
                if (ReferenceEquals(target, windowVM))
                    continue;

                target.UpdateCaptureAreaCommand.Execute(Rectangle.Empty);
            }
        }
        finally
        {
            _isPropagatingChanges = false;
        }
    }

    private void OnSecondaryCaptureAreaChanged(ISelectionOverlayWindowViewModel windowVM)
    {
        if (windowVM.CaptureArea.IsEmpty || !windowVM.Monitor.HasValue)
        {
            return;
        }

        _isPropagatingChanges = true;
        try
        {
            foreach (var target in _windowViewModels)
            {
                if (ReferenceEquals(target, windowVM))
                    continue;

                target.UpdateCaptureAreaCommand.Execute(Rectangle.Empty);
            }
        }
        finally
        {
            _isPropagatingChanges = false;
        }
    }

    private void OnSelectedCaptureModeIndexChanged(ISelectionOverlayWindowViewModel windowVM)
    {
        var selectedIndex = windowVM.SelectedCaptureModeIndex;
        _isPropagatingChanges = true;
        try
        {
            foreach (var target in _windowViewModels)
            {
                if (ReferenceEquals(target, windowVM))
                    continue;

                target.UpdateSelectedCaptureModeCommand.Execute(selectedIndex);
            }
        }
        finally
        {
            _isPropagatingChanges = false;
        }
    }

    private void OnSelectedCaptureTypeIndexChanged(ISelectionOverlayWindowViewModel windowVM)
    {
        var selectedIndex = windowVM.SelectedCaptureTypeIndex;
        _isPropagatingChanges = true;
        try
        {
            foreach (var target in _windowViewModels)
            {
                if (ReferenceEquals(target, windowVM))
                    continue;

                target.UpdateSelectedCaptureTypeCommand.Execute(selectedIndex);
            }
        }
        finally
        {
            _isPropagatingChanges = false;
        }

        if (windowVM.GetSelectedCaptureType() == CaptureType.AllScreens)
        {
            switch (windowVM.GetSelectedCaptureMode())
            {
                case CaptureMode.Image:
                    AllScreensCaptureRequested?.Invoke(this, EventArgs.Empty);
                    break;

                case CaptureMode.Video:
                    break;
            }
        }
    }
}
