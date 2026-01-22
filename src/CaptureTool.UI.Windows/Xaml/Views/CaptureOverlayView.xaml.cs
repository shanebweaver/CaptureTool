using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Themes;
using CaptureTool.Application.Implementations.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;

namespace CaptureTool.UI.Windows.Xaml.Views;

public sealed partial class CaptureOverlayView : CaptureOverlayViewBase
{
    private readonly MonitorCaptureResult _monitor;
    private readonly System.Drawing.Rectangle _area;
    private SpriteVisual? _shadowVisual;
    private DropShadow? _shadow;

    public CaptureOverlayView(MonitorCaptureResult monitor, System.Drawing.Rectangle area)
    {
        _monitor = monitor;
        _area = area;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        InitializeComponent();

        DispatcherQueue.TryEnqueue(() =>
        {
            AddToolbarShadow();
            UpdateRequestedAppTheme();
        });
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Load(new CaptureOverlayViewModelOptions(_monitor, _area));
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;

        ViewModel.Dispose();

        CleanupCompositionResources();
    }

    private void CleanupCompositionResources()
    {
        try
        {
            if (_shadowVisual != null)
            {
                _shadowVisual.Shadow = null;
                _shadowVisual.Dispose();
                _shadowVisual = null;
            }

            if (_shadow != null)
            {
                _shadow.Dispose();
                _shadow = null;
            }
        }
        catch { }
    }

    private void AddToolbarShadow()
    {
        try
        {
            Compositor? compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;

            _shadow = compositor.CreateDropShadow();
            _shadow.Color = Colors.Black;
            _shadow.BlurRadius = 12;
            _shadow.Opacity = 0.3f;
            _shadow.Offset = new Vector3(0, 4, 0);

            _shadowVisual = compositor.CreateSpriteVisual();
            _shadowVisual.Shadow = _shadow;
            _shadowVisual.Size = new Vector2((float)Toolbar.ActualWidth, (float)Toolbar.ActualHeight);

            ElementCompositionPreview.SetElementChildVisual(ToolbarHost, _shadowVisual);

            Toolbar.SizeChanged += Toolbar_SizeChanged;
        }
        catch { }
    }

    private void Toolbar_SizeChanged(object s, SizeChangedEventArgs e)
    {
        if (_shadowVisual != null)
        {
            _shadowVisual.Size = new Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
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
