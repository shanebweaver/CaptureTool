using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Domain.Capture;
using CaptureTool.Presentation.Features.CaptureOverlay;
using CaptureTool.Presentation.Windows.WinUI.Capture;
using Microsoft.UI.Xaml;
using System.ComponentModel;
using Windows.Foundation;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Views;

internal readonly record struct CaptureOverlaySurfaceMetrics(
    double Width,
    double Height,
    double RasterizationScale,
    bool ShowsToolbar);

public sealed partial class CaptureOverlayView : CaptureOverlayViewBase
{
    private readonly NewCaptureArgs _captureArgs;
    private readonly WinUICaptureDiscardConfirmationService _captureDiscardConfirmationService;
    private CaptureOverlaySurfaceMetrics _lastPublishedMetrics;
    private XamlRoot? _observedXamlRoot;
    private double _lastToolbarWidth;
    private bool _surfaceUpdateQueued;
    private bool _isLoaded;
    private bool _lastPublishedMetricsWereValid;
    private bool _unloaded;

    internal event EventHandler<CaptureOverlaySurfaceMetrics>? SurfaceMetricsChanged;
    internal event EventHandler? SurfaceMetricsInvalidated;

    public CaptureOverlayView(NewCaptureArgs captureArgs)
    {
        _captureArgs = captureArgs;
        _captureDiscardConfirmationService = App.Current.ServiceProvider.GetService<WinUICaptureDiscardConfirmationService>();

        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        Toolbar.SizeChanged += OnSurfaceElementSizeChanged;
        Toolbar.LayoutUpdated += OnSurfaceElementLayoutUpdated;
        RecordingErrorInfoBar.SizeChanged += OnSurfaceElementSizeChanged;
        RecordingErrorInfoBar.LayoutUpdated += OnSurfaceElementLayoutUpdated;

        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateRequestedAppTheme();
        });
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _unloaded = false;
        _captureDiscardConfirmationService.XamlRoot = RootPanel.XamlRoot;
        _captureDiscardConfirmationService.DialogHostBounds = _captureArgs.Monitor.MonitorBounds;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        AttachXamlRoot();
        ViewModel.Load(new CaptureOverlayViewModelOptions(_captureArgs));
        _isLoaded = true;
        QueueSurfaceMetricsUpdate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _unloaded = true;
        _isLoaded = false;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        Toolbar.SizeChanged -= OnSurfaceElementSizeChanged;
        Toolbar.LayoutUpdated -= OnSurfaceElementLayoutUpdated;
        RecordingErrorInfoBar.SizeChanged -= OnSurfaceElementSizeChanged;
        RecordingErrorInfoBar.LayoutUpdated -= OnSurfaceElementLayoutUpdated;
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        DetachXamlRoot();

        _captureDiscardConfirmationService.XamlRoot = null;
        _captureDiscardConfirmationService.DialogHostBounds = null;

        ViewModel.Dispose();
    }

    internal bool TryGetCurrentSurfaceMetrics(out CaptureOverlaySurfaceMetrics metrics)
    {
        try
        {
            return TryCalculateCurrentSurfaceMetrics(out metrics);
        }
        catch
        {
            // The XAML island can be between layout passes while it is hidden or
            // shutting down. The host keeps its last valid bounds in that case.
            metrics = default;
            return false;
        }
    }

    private bool TryCalculateCurrentSurfaceMetrics(out CaptureOverlaySurfaceMetrics metrics)
    {
        if (!_isLoaded)
        {
            metrics = default;
            return false;
        }

        double fallbackScale = _captureArgs.Monitor.Scale;
        double scale = RootPanel.XamlRoot?.RasterizationScale ?? fallbackScale;
        if (!double.IsFinite(scale) || scale <= 0)
        {
            metrics = default;
            return false;
        }

        bool showsToolbar = !ViewModel.HasRecordingError;
        Size toolbarSize = GetToolbarSize(scale);
        if (showsToolbar && IsPositive(toolbarSize.Width) && IsPositive(toolbarSize.Height))
        {
            _lastToolbarWidth = toolbarSize.Width;
        }

        double width = toolbarSize.Width;
        double height = toolbarSize.Height;

        if (!showsToolbar && IsPositive(_lastToolbarWidth))
        {
            width = _lastToolbarWidth;
            RecordingErrorInfoBar.Width = width;
            RecordingErrorInfoBar.Measure(new Size(width, double.PositiveInfinity));
            height = Math.Max(RecordingErrorInfoBar.ActualHeight, RecordingErrorInfoBar.DesiredSize.Height);

            // The binding and InfoBar template can complete on the next layout pass.
            // Hide the shadow immediately by retaining a usable foreground height.
            if (!IsPositive(height))
            {
                height = toolbarSize.Height;
            }
        }

        if (!IsPositive(width) || !IsPositive(height))
        {
            metrics = default;
            return false;
        }

        metrics = new(width, height, scale, showsToolbar);
        return true;
    }

    private Size GetToolbarSize(double scale)
    {
        double availableWidth = _captureArgs.Monitor.MonitorBounds.Width / scale;
        Size desiredSize = Toolbar.MeasureNaturalSize();
        double width = Math.Min(desiredSize.Width, availableWidth);
        double height = desiredSize.Height;

        if (IsPositive(width) && IsPositive(height))
        {
            return new Size(width, height);
        }

        return new Size(Toolbar.ActualWidth, Toolbar.ActualHeight);
    }

    private void OnSurfaceElementSizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueSurfaceMetricsUpdate();
    }

    private void OnSurfaceElementLayoutUpdated(object? sender, object e)
    {
        QueueSurfaceMetricsUpdate();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CaptureOverlayViewModel.HasRecordingError) &&
            ViewModel.HasRecordingError)
        {
            PublishSurfaceMetrics();
        }

        QueueSurfaceMetricsUpdate();
    }

    private void AttachXamlRoot()
    {
        XamlRoot? xamlRoot = RootPanel.XamlRoot;
        if (ReferenceEquals(_observedXamlRoot, xamlRoot))
        {
            return;
        }

        DetachXamlRoot();
        _observedXamlRoot = xamlRoot;
        if (_observedXamlRoot != null)
        {
            _observedXamlRoot.Changed += XamlRoot_Changed;
        }
    }

    private void DetachXamlRoot()
    {
        if (_observedXamlRoot != null)
        {
            _observedXamlRoot.Changed -= XamlRoot_Changed;
            _observedXamlRoot = null;
        }
    }

    private void XamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        QueueSurfaceMetricsUpdate();
    }

    private void QueueSurfaceMetricsUpdate()
    {
        if (_unloaded || _surfaceUpdateQueued)
        {
            return;
        }

        _surfaceUpdateQueued = true;
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            _surfaceUpdateQueued = false;
            PublishSurfaceMetrics();
        }))
        {
            _surfaceUpdateQueued = false;
        }
    }

    private void PublishSurfaceMetrics()
    {
        if (_unloaded)
        {
            return;
        }

        if (!TryGetCurrentSurfaceMetrics(out CaptureOverlaySurfaceMetrics metrics))
        {
            if (_lastPublishedMetricsWereValid)
            {
                _lastPublishedMetricsWereValid = false;
                SurfaceMetricsInvalidated?.Invoke(this, EventArgs.Empty);
            }
            return;
        }

        if (_lastPublishedMetricsWereValid && metrics == _lastPublishedMetrics)
        {
            return;
        }

        _lastPublishedMetricsWereValid = true;
        _lastPublishedMetrics = metrics;
        SurfaceMetricsChanged?.Invoke(this, metrics);
    }

    private static bool IsPositive(double value) => double.IsFinite(value) && value > 0;

    private void UpdateRequestedAppTheme()
    {
        object theme = ViewModel.CurrentAppTheme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            AppTheme.SystemDefault => ConvertToElementTheme(ViewModel.DefaultAppTheme),
            _ => DependencyProperty.UnsetValue
        };

        RootPanel.SetValue(FrameworkElement.RequestedThemeProperty, theme);
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
}
