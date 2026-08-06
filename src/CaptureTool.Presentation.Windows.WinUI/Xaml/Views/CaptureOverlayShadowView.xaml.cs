using CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Views;

internal sealed class CaptureOverlayShadowInitializationEventArgs(bool succeeded) : EventArgs
{
    public bool Succeeded { get; } = succeeded;
}

internal sealed partial class CaptureOverlayShadowView : UserControlBase, IDisposable
{
    internal const double ShadowPaddingDips = 16;
    private const float ToolbarCornerRadiusDips = 4;
    private const float ShadowBlurRadiusDips = 12;
    private const float ShadowVerticalOffsetDips = 4;

    private SpriteVisual? _shadowVisual;
    private DropShadow? _shadow;
    private CompositionColorBrush? _casterFillBrush;
    private CompositionMaskBrush? _casterMaskBrush;
    private CompositionBrush? _casterAlphaMask;
    private XamlRoot? _observedXamlRoot;
    private Vector2 _casterOffsetPixels;
    private Vector2 _casterSizePixels;
    private double _toolbarRasterizationScale = 1;
    private bool _initializationCompleted;
    private bool _disposed;

    internal event EventHandler<CaptureOverlayShadowInitializationEventArgs>? InitializationCompleted;

    public CaptureOverlayShadowView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ShadowHost.SizeChanged += ShadowHost_SizeChanged;
    }

    internal bool QueueInitialization()
    {
        if (_disposed)
        {
            return false;
        }

        if (_initializationCompleted)
        {
            return true;
        }

        return DispatcherQueue.TryEnqueue(() =>
        {
            if (IsLoaded)
            {
                InitializeShadow();
            }
        });
    }

    internal void UpdateCaster(
        int offsetXPixels,
        int offsetYPixels,
        int widthPixels,
        int heightPixels,
        double toolbarRasterizationScale)
    {
        if (_disposed ||
            offsetXPixels < 0 ||
            offsetYPixels < 0 ||
            widthPixels <= 0 ||
            heightPixels <= 0 ||
            !double.IsFinite(toolbarRasterizationScale) ||
            toolbarRasterizationScale <= 0)
        {
            return;
        }

        _casterOffsetPixels = new Vector2(offsetXPixels, offsetYPixels);
        _casterSizePixels = new Vector2(widthPixels, heightPixels);
        _toolbarRasterizationScale = toolbarRasterizationScale;
        ApplyCasterLayout();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ObserveXamlRoot();
        ApplyCasterLayout();
        InitializeShadow();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopObservingXamlRoot();
        CleanupCompositionResources();
    }

    private void ObserveXamlRoot()
    {
        XamlRoot? xamlRoot = XamlRoot;
        if (ReferenceEquals(_observedXamlRoot, xamlRoot))
        {
            return;
        }

        StopObservingXamlRoot();
        _observedXamlRoot = xamlRoot;
        if (_observedXamlRoot != null)
        {
            _observedXamlRoot.Changed += XamlRoot_Changed;
        }
    }

    private void StopObservingXamlRoot()
    {
        if (_observedXamlRoot != null)
        {
            _observedXamlRoot.Changed -= XamlRoot_Changed;
            _observedXamlRoot = null;
        }
    }

    private void XamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        ApplyCasterLayout();
    }

    private void ShadowHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyCasterLayout();
    }

    private void InitializeShadow()
    {
        if (_disposed || _initializationCompleted)
        {
            return;
        }

        bool succeeded = false;
        try
        {
            Compositor compositor = ElementCompositionPreview.GetElementVisual(ShadowCaster).Compositor;

            _casterAlphaMask = ShadowCaster.GetAlphaMask();
            _casterFillBrush = compositor.CreateColorBrush(Colors.White);
            _casterMaskBrush = compositor.CreateMaskBrush();
            _casterMaskBrush.Source = _casterFillBrush;
            _casterMaskBrush.Mask = _casterAlphaMask;

            _shadow = compositor.CreateDropShadow();
            _shadow.Color = Colors.Black;
            _shadow.Opacity = 0.35f;
            _shadow.SourcePolicy = CompositionDropShadowSourcePolicy.Default;

            _shadowVisual = compositor.CreateSpriteVisual();
            _shadowVisual.RelativeSizeAdjustment = Vector2.One;
            _shadowVisual.Brush = _casterMaskBrush;
            _shadowVisual.Shadow = _shadow;

            ElementCompositionPreview.SetElementChildVisual(ShadowCaster, _shadowVisual);
            ApplyShadowEffects();
            succeeded = true;
        }
        catch
        {
            CleanupCompositionResources();
        }

        _initializationCompleted = true;
        InitializationCompleted?.Invoke(
            this,
            new CaptureOverlayShadowInitializationEventArgs(succeeded));
    }

    private void ApplyCasterLayout()
    {
        if (ShadowHost.ActualWidth <= 0 || ShadowHost.ActualHeight <= 0)
        {
            return;
        }

        float physicalHostWidth = _casterSizePixels.X + (2 * _casterOffsetPixels.X);
        float physicalHostHeight = _casterSizePixels.Y + (2 * _casterOffsetPixels.Y);
        if (physicalHostWidth <= 0 || physicalHostHeight <= 0)
        {
            return;
        }

        double hostUnitsPerPixelX = ShadowHost.ActualWidth / physicalHostWidth;
        double hostUnitsPerPixelY = ShadowHost.ActualHeight / physicalHostHeight;
        double insetX = _casterOffsetPixels.X * hostUnitsPerPixelX;
        double insetY = _casterOffsetPixels.Y * hostUnitsPerPixelY;

        ShadowCaster.Margin = new Thickness(insetX, insetY, 0, 0);
        ShadowCaster.Width = Math.Max(0, ShadowHost.ActualWidth - (2 * insetX));
        ShadowCaster.Height = Math.Max(0, ShadowHost.ActualHeight - (2 * insetY));

        double physicalCornerRadius = ToolbarCornerRadiusDips * _toolbarRasterizationScale;
        ShadowCaster.RadiusX = physicalCornerRadius * hostUnitsPerPixelX;
        ShadowCaster.RadiusY = physicalCornerRadius * hostUnitsPerPixelY;
        ApplyShadowEffects();
    }

    private void ApplyShadowEffects()
    {
        if (_shadow == null || ShadowHost.ActualWidth <= 0 || ShadowHost.ActualHeight <= 0)
        {
            return;
        }

        float physicalHostWidth = _casterSizePixels.X + (2 * _casterOffsetPixels.X);
        float physicalHostHeight = _casterSizePixels.Y + (2 * _casterOffsetPixels.Y);
        if (physicalHostWidth <= 0 || physicalHostHeight <= 0)
        {
            return;
        }

        float hostUnitsPerPixelX = (float)(ShadowHost.ActualWidth / physicalHostWidth);
        float hostUnitsPerPixelY = (float)(ShadowHost.ActualHeight / physicalHostHeight);
        float averageHostUnitsPerPixel = (hostUnitsPerPixelX + hostUnitsPerPixelY) / 2;

        _shadow.BlurRadius =
            ShadowBlurRadiusDips *
            (float)_toolbarRasterizationScale *
            averageHostUnitsPerPixel;
        _shadow.Offset = new Vector3(
            0,
            ShadowVerticalOffsetDips *
                (float)_toolbarRasterizationScale *
                hostUnitsPerPixelY,
            0);
    }

    private void CleanupCompositionResources()
    {
        try
        {
            ElementCompositionPreview.SetElementChildVisual(ShadowCaster, null);
        }
        catch { }

        SpriteVisual? shadowVisual = _shadowVisual;
        _shadowVisual = null;
        try
        {
            if (shadowVisual != null)
            {
                shadowVisual.Shadow = null;
                shadowVisual.Brush = null;
                shadowVisual.Dispose();
            }
        }
        catch { }

        DropShadow? shadow = _shadow;
        _shadow = null;
        try
        {
            shadow?.Dispose();
        }
        catch { }

        CompositionMaskBrush? casterMaskBrush = _casterMaskBrush;
        _casterMaskBrush = null;
        try
        {
            if (casterMaskBrush != null)
            {
                casterMaskBrush.Source = null;
                casterMaskBrush.Mask = null;
                casterMaskBrush.Dispose();
            }
        }
        catch { }

        CompositionBrush? casterAlphaMask = _casterAlphaMask;
        _casterAlphaMask = null;
        try
        {
            casterAlphaMask?.Dispose();
        }
        catch { }

        CompositionColorBrush? casterFillBrush = _casterFillBrush;
        _casterFillBrush = null;
        try
        {
            casterFillBrush?.Dispose();
        }
        catch
        {
            // Native composition teardown is best effort during overlay shutdown.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        ShadowHost.SizeChanged -= ShadowHost_SizeChanged;
        StopObservingXamlRoot();
        InitializationCompleted = null;
        CleanupCompositionResources();
        GC.SuppressFinalize(this);
    }
}
