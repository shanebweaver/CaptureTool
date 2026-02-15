using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Implementations.ViewModels;
using System.ComponentModel;
using System.Drawing;

namespace CaptureTool.Application.Implementations.ViewModels;

public sealed partial class SelectionOverlayHostViewModel : ViewModelBase, ISelectionOverlayHostViewModel
{
    private readonly List<ISelectionOverlayWindowViewModel> _windowViewModels = [];

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
        // Subscribe to the new source-aware events
        newVM.CaptureModeIndexChanged += OnCaptureModeIndexChanged;
        newVM.CaptureTypeIndexChanged += OnCaptureTypeIndexChanged;

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
            windowViewModel.CaptureModeIndexChanged -= OnCaptureModeIndexChanged;
            windowViewModel.CaptureTypeIndexChanged -= OnCaptureTypeIndexChanged;

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
        if (sender is ISelectionOverlayWindowViewModel windowVM)
        {
            switch (e.PropertyName)
            {
                case nameof(ISelectionOverlayWindowViewModel.CaptureArea):
                    OnPrimaryCaptureAreaChanged(windowVM);
                    break;
            }
        }
    }

    private void OnSecondaryWindowViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
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

    private void OnCaptureModeIndexChanged(object? sender, (int Index, SelectionUpdateSource Source) args)
    {
        if (sender is not ISelectionOverlayWindowViewModel windowVM)
            return;

        // State machine: Handle capture mode changes based on source
        switch (args.Source)
        {
            case SelectionUpdateSource.UserInteraction:
                // User initiated the change, propagate to other windows
                foreach (var target in _windowViewModels)
                {
                    if (ReferenceEquals(target, windowVM))
                        continue;

                    target.UpdateSelectedCaptureModeCommand.Execute((args.Index, SelectionUpdateSource.Propagation));
                }
                break;

            case SelectionUpdateSource.Propagation:
                // Already propagated from another window, don't re-propagate
                break;

            case SelectionUpdateSource.Programmatic:
                // Programmatic update (load, etc.), don't propagate
                break;
        }
    }

    private void OnCaptureTypeIndexChanged(object? sender, (int Index, SelectionUpdateSource Source) args)
    {
        if (sender is not ISelectionOverlayWindowViewModel windowVM)
            return;

        // State machine: Handle capture type changes based on source
        switch (args.Source)
        {
            case SelectionUpdateSource.UserInteraction:
                // User initiated the change, propagate to other windows
                foreach (var target in _windowViewModels)
                {
                    if (ReferenceEquals(target, windowVM))
                        continue;

                    target.UpdateSelectedCaptureTypeCommand.Execute((args.Index, SelectionUpdateSource.Propagation));
                }

                // Handle AllScreens capture request
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
                break;

            case SelectionUpdateSource.Propagation:
                // Already propagated from another window, don't re-propagate
                break;

            case SelectionUpdateSource.Programmatic:
                // Programmatic update (load, etc.), don't propagate
                break;
        }
    }

    private void OnPrimaryCaptureAreaChanged(ISelectionOverlayWindowViewModel windowVM)
    {
        if (windowVM.CaptureArea.IsEmpty || !windowVM.Monitor.HasValue)
        {
            return;
        }

        foreach (var target in _windowViewModels)
        {
            if (ReferenceEquals(target, windowVM))
                continue;

            target.UpdateCaptureAreaCommand.Execute(Rectangle.Empty);
        }
    }

    private void OnSecondaryCaptureAreaChanged(ISelectionOverlayWindowViewModel windowVM)
    {
        if (windowVM.CaptureArea.IsEmpty || !windowVM.Monitor.HasValue)
        {
            return;
        }

        foreach (var target in _windowViewModels)
        {
            if (ReferenceEquals(target, windowVM))
                continue;

            target.UpdateCaptureAreaCommand.Execute(Rectangle.Empty);
        }
    }
}
