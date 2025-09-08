using CaptureTool.Capture;
using CaptureTool.Services.Themes;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;

namespace CaptureTool.UI.Windows.Xaml.Views;

public sealed partial class CaptureOverlayView : CaptureOverlayViewBase
{
    public CaptureOverlayView(MonitorCaptureResult monitor, System.Drawing.Rectangle area)
    {
        InitializeComponent();
        ViewModel.Load((monitor, area));
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        DispatcherQueue.TryEnqueue(() =>
        {
            AddToolbarShadow();
            UpdateRequestedAppTheme();
        });
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {

    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Unloaded -= OnUnloaded;
        ViewModel.Unload();
    }

    private void AddToolbarShadow()
    {
        Compositor? compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;

        DropShadow? shadow = compositor.CreateDropShadow();
        shadow.Color = Colors.Black;
        shadow.BlurRadius = 12;
        shadow.Opacity = 0.3f;
        shadow.Offset = new Vector3(0, 4, 0);

        SpriteVisual shadowVisual = compositor.CreateSpriteVisual();
        shadowVisual.Shadow = shadow;
        shadowVisual.Size = new Vector2((float)Toolbar.ActualWidth, (float)Toolbar.ActualHeight);

        ElementCompositionPreview.SetElementChildVisual(ToolbarHost, shadowVisual);

        Toolbar.SizeChanged += (s, e) =>
        {
            shadowVisual.Size = new Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
        };
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
