using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Loading;
using CaptureTool.Infrastructure.Interfaces.Themes;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System.ComponentModel;
using System.Drawing;
using Windows.UI.Core;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Views;

public sealed partial class SelectionOverlayWindowView : SelectionOverlayWindowViewBase
{
    public SelectionOverlayWindowView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateRequestedAppTheme();
        });
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Start fade-in animation
        FadeInStoryboard.Begin();

        ViewModel.CaptureOptionsUpdated += ViewModel_CaptureOptionsUpdated;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        SelectionOverlay.SelectionComplete += SelectionOverlay_SelectionComplete;

        if (ViewModel.IsLoaded)
        {
            SelectionOverlay.WindowRects = ViewModel.MonitorWindows;
            SelectionOverlay.SelectionRect = ViewModel.CaptureArea;

            CaptureType? selectedCaptureType = ViewModel.GetSelectedCaptureType();
            if (selectedCaptureType != null)
            {
                SelectionOverlay.CaptureType = selectedCaptureType.Value;
            }

            // Subscribe to toolbar events only after ViewModel is fully loaded
            if (ViewModel.IsPrimary)
            {
                SelectionToolbar.CaptureModeSelectionChanged += SelectionToolbar_CaptureModeSelectionChanged;
                SelectionToolbar.CaptureTypeSelectionChanged += SelectionToolbar_CaptureTypeSelectionChanged;
            }
        }
        else
        {
            // Subscribe when loading completes
            ViewModel.LoadStateChanged += OnViewModelLoadStateChanged;
        }

        SetFocus();
    }

    private void OnViewModelLoadStateChanged(object? sender, LoadState e)
    {
        if (e == LoadState.Loaded && ViewModel.IsPrimary)
        {
            SelectionToolbar.CaptureModeSelectionChanged += SelectionToolbar_CaptureModeSelectionChanged;
            SelectionToolbar.CaptureTypeSelectionChanged += SelectionToolbar_CaptureTypeSelectionChanged;
            ViewModel.LoadStateChanged -= OnViewModelLoadStateChanged;
        }
    }

    private void ViewModel_CaptureOptionsUpdated(object? sender, CaptureOptions e)
    {
        var selectedCaptureType = ViewModel.GetSelectedCaptureType();
        if (selectedCaptureType != null)
        {
            SelectionOverlay.CaptureType = selectedCaptureType.Value;
        }

        SelectionOverlay.UpdateSelectionRect(Rectangle.Empty);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;

        ViewModel.CaptureOptionsUpdated -= ViewModel_CaptureOptionsUpdated;
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        ViewModel.LoadStateChanged -= OnViewModelLoadStateChanged;
        SelectionOverlay.SelectionComplete -= SelectionOverlay_SelectionComplete;

        if (ViewModel.IsPrimary)
        {
            SelectionToolbar.CaptureModeSelectionChanged -= SelectionToolbar_CaptureModeSelectionChanged;
            SelectionToolbar.CaptureTypeSelectionChanged -= SelectionToolbar_CaptureTypeSelectionChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ISelectionOverlayWindowViewModel.SelectedCaptureTypeIndex):
                var selectedCaptureType = ViewModel.GetSelectedCaptureType();
                if (selectedCaptureType != null)
                {
                    SelectionOverlay.CaptureType = selectedCaptureType.Value;
                }
                break;
            case nameof(ISelectionOverlayWindowViewModel.CaptureArea):
                SelectionOverlay.SelectionRect = ViewModel.CaptureArea;
                break;
            case nameof(ISelectionOverlayWindowViewModel.MonitorWindows):
                SelectionOverlay.WindowRects = ViewModel.MonitorWindows;
                break;
        }
    }

    private void UpdateRequestedAppTheme()
    {
        object theme = ViewModel.CurrentAppTheme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            AppTheme.SystemDefault => ConvertToElementTheme(ViewModel.DefaultAppTheme),
            _ => DependencyProperty.UnsetValue
        };

        RootPanel.SetValue(RequestedThemeProperty, theme);
    }

    private static ElementTheme ConvertToElementTheme(AppTheme appTheme)
    {
        return appTheme switch
        {
            AppTheme.SystemDefault => ElementTheme.Default,
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }

    private void SelectionOverlay_SelectionComplete(object? _, Rectangle captureArea)
    {
        ViewModel.UpdateCaptureAreaCommand.Execute(captureArea);

        if (captureArea.Height >= 40 && captureArea.Width >= 40)
        {
            ViewModel.RequestCaptureCommand.Execute();
        }
    }

    private void SelectionToolbar_CaptureModeSelectionChanged(object? sender, int e)
    {
        ViewModel.UpdateSelectedCaptureModeCommand.Execute((e, SelectionUpdateSource.UserInteraction));
    }

    private void SelectionToolbar_CaptureTypeSelectionChanged(object? sender, int e)
    {
        ViewModel.UpdateSelectedCaptureTypeCommand.Execute((e, SelectionUpdateSource.UserInteraction));
    }

    private void SelectionOverlayContainer_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        ProtectedCursor = InputCursor.CreateFromCoreCursor(new CoreCursor(CoreCursorType.Cross, 1));
    }

    private void ToolbarContainer_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        ProtectedCursor = InputCursor.CreateFromCoreCursor(new CoreCursor(0, 1));
    }

    public void SetFocus()
    {
        try
        {
            if (ViewModel.IsPrimary)
            {
                _ = SelectionToolbar.Focus(FocusState.Programmatic);
            }
            else
            {
                _ = SecretEscapeButton.Focus(FocusState.Programmatic);
            }
        }
        catch { }
    }
}
