using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI.Composition;
using Microsoft.UI.System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

/// <summary>A non-interactive busy border placed over an ordinary button in the same Grid cell.</summary>
public sealed partial class AuroraEdge : UserControl
{
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive), typeof(bool), typeof(AuroraEdge), new PropertyMetadata(false, OnIsActiveChanged));

    private const float HaloPadding = 12;
    private readonly UISettings _uiSettings = new();
    private ThemeSettings? _themeSettings;
    private readonly List<CompositionObject> _resources = [];
    private ContainerVisual? _scene;
    private LayerVisual? _halo;
    private ShapeVisual? _edge;
    private ShapeVisual? _haloShape;
    private CompositionRoundedRectangleGeometry? _geometry;
    private CompositionLinearGradientBrush? _gradient;
    private CompositionScopedBatch? _fade;
    private bool _moving;
    private bool _observingSettings;
    private bool _compositionUnavailable;
    private bool IsHighContrast => _themeSettings?.HighContrast == true;

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public AuroraEdge()
    {
        InitializeComponent();
        Visibility = Visibility.Collapsed;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (_, _) => UpdateSize();
        ActualThemeChanged += (_, _) => UpdateAppearance();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AuroraEdgeAutomationPeer(this);

    private static void OnIsActiveChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((AuroraEdge)sender).UpdateState();

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (!_observingSettings)
        {
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
                _uiSettings.AnimationsEnabledChanged += OnAnimationsChanged;
            _themeSettings = ThemeSettings.CreateForWindowId(XamlRoot.ContentIslandEnvironment.AppWindowId);
            _themeSettings.Changed += OnContrastChanged;
            _observingSettings = true;
        }
        UpdateState();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (_observingSettings)
        {
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
                _uiSettings.AnimationsEnabledChanged -= OnAnimationsChanged;
            if (_themeSettings != null) _themeSettings.Changed -= OnContrastChanged;
            _themeSettings = null;
            _observingSettings = false;
        }
        CancelFade();
        StopMotion();
        ElementCompositionPreview.SetElementChildVisual(CompositionHost, null);
        for (int i = _resources.Count - 1; i >= 0; i--) _resources[i].Dispose();
        _resources.Clear();
        _scene = null; _halo = null; _edge = null; _haloShape = null; _geometry = null; _gradient = null;
        _compositionUnavailable = false;
    }

    private void OnAnimationsChanged(UISettings sender, UISettingsAnimationsEnabledChangedEventArgs args) =>
        DispatcherQueue.TryEnqueue(() => { if (IsLoaded) UpdateState(); });

    private void OnContrastChanged(ThemeSettings sender, object args) =>
        DispatcherQueue.TryEnqueue(() => { if (IsLoaded) UpdateState(); });

    private void UpdateState()
    {
        CancelFade();
        if (IsActive)
        {
            Visibility = Visibility.Visible;
            if (!IsLoaded) return;
            EnsureComposition();
            UpdateAppearance();
            UpdateSize();
            if (_scene != null)
            {
                if (_uiSettings.AnimationsEnabled && !IsHighContrast) AnimateOpacity(1);
                else _scene.Opacity = 1;
            }
            FrameworkElementAutomationPeer.FromElement(this)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
        else if (_scene != null && Visibility == Visibility.Visible && IsLoaded &&
                 _uiSettings.AnimationsEnabled && !IsHighContrast)
        {
            _fade = _scene.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            _fade.Completed += OnFadeCompleted;
            AnimateOpacity(0);
            _fade.End();
        }
        else Hide();
    }

    private void OnFadeCompleted(object sender, CompositionBatchCompletedEventArgs args)
    {
        if (!ReferenceEquals(sender, _fade)) return;
        CancelFade();
        if (!IsActive) Hide();
    }

    private void Hide()
    {
        StopMotion();
        if (_scene != null) _scene.Opacity = 0;
        Visibility = Visibility.Collapsed;
    }

    private void CancelFade()
    {
        if (_fade != null)
        {
            _fade.Completed -= OnFadeCompleted;
            _fade.Dispose();
            _fade = null;
        }
        _scene?.StopAnimation(nameof(Visual.Opacity));
    }

    private void EnsureComposition()
    {
        if (_scene != null || _compositionUnavailable || IsHighContrast) return;
        try
        {
            var compositor = ElementCompositionPreview.GetElementVisual(CompositionHost).Compositor;
            _scene = Keep(compositor.CreateContainerVisual());
            _scene.Offset = new(-HaloPadding, -HaloPadding, 0);
            _scene.Opacity = 0;
            _gradient = Keep(compositor.CreateLinearGradientBrush());
            _gradient.MappingMode = CompositionMappingMode.Relative;
            for (int i = 0; i < 5; i++)
                _gradient.ColorStops.Add(Keep(compositor.CreateColorGradientStop(i / 4f, Microsoft.UI.Colors.Transparent)));
            _geometry = Keep(compositor.CreateRoundedRectangleGeometry());
            _geometry.Offset = new(HaloPadding + .75f);
            _geometry.CornerRadius = new(3.25f);
            _edge = CreateOutline(compositor, 1.5f);
            _haloShape = CreateOutline(compositor, 4);
            _halo = Keep(compositor.CreateLayerVisual());
            using var blur = new GaussianBlurEffect
            {
                Source = new CompositionEffectSourceParameter("Backdrop"),
                BlurAmount = 4,
                BorderMode = EffectBorderMode.Soft,
                Optimization = EffectOptimization.Balanced
            };
            var factory = Keep(compositor.CreateEffectFactory(blur));
            _halo.Effect = Keep(factory.CreateBrush());
            _halo.Children.InsertAtTop(_haloShape);
            _scene.Children.InsertAtTop(_halo);
            _scene.Children.InsertAtTop(_edge);
            ElementCompositionPreview.SetElementChildVisual(CompositionHost, _scene);
        }
        catch (Exception error)
        {
            // Decoration must never prevent a button from working on an unsupported renderer.
            System.Diagnostics.Debug.WriteLine($"Aurora edge unavailable: {error.Message}");
            ElementCompositionPreview.SetElementChildVisual(CompositionHost, null);
            for (int i = _resources.Count - 1; i >= 0; i--) _resources[i].Dispose();
            _resources.Clear();
            _scene = null; _halo = null; _edge = null; _haloShape = null; _geometry = null; _gradient = null;
            _compositionUnavailable = true;
        }
    }

    private T Keep<T>(T resource) where T : CompositionObject
    {
        _resources.Add(resource);
        return resource;
    }

    private ShapeVisual CreateOutline(Compositor compositor, float thickness)
    {
        var shape = Keep(compositor.CreateSpriteShape(_geometry));
        shape.StrokeBrush = _gradient;
        shape.StrokeThickness = thickness;
        var visual = Keep(compositor.CreateShapeVisual());
        visual.Shapes.Add(shape);
        return visual;
    }

    private void UpdateSize()
    {
        if (_scene == null || _geometry == null) return;
        Vector2 size = new((float)ActualWidth, (float)ActualHeight);
        _geometry.Size = Vector2.Max(Vector2.Zero, size - new Vector2(1.5f));
        var padded = size + new Vector2(HaloPadding * 2);
        _scene.Size = _edge!.Size = _halo!.Size = _haloShape!.Size = padded;
    }

    private void UpdateAppearance()
    {
        bool contrast = IsHighContrast;
        ContrastEdge.Visibility = contrast ? Visibility.Visible : Visibility.Collapsed;
        StaticEdge.Visibility = !contrast && _scene == null ? Visibility.Visible : Visibility.Collapsed;
        if (_scene == null || _gradient == null) return;
        _scene.IsVisible = !contrast;
        bool dark = ActualTheme == ElementTheme.Dark;
        Color cyan = dark ? Color.FromArgb(255, 88, 215, 239) : Color.FromArgb(255, 18, 157, 209);
        Color violet = dark ? Color.FromArgb(255, 175, 149, 255) : Color.FromArgb(255, 132, 96, 212);
        Color pink = dark ? Color.FromArgb(255, 243, 142, 203) : Color.FromArgb(255, 207, 104, 175);
        Color[] colors = [cyan, violet, pink, violet, cyan];
        for (int i = 0; i < colors.Length; i++) _gradient.ColorStops[i].Color = colors[i];
        _halo!.Opacity = dark ? .4f : .3f;
        if (IsActive && IsLoaded && _uiSettings.AnimationsEnabled && !contrast) StartMotion();
        else StopMotion();
    }

    private void StartMotion()
    {
        if (_moving || _gradient == null) return;
        AnimatePoint(nameof(CompositionLinearGradientBrush.StartPoint), new(-.75f, -.25f), new(0, -.25f));
        AnimatePoint(nameof(CompositionLinearGradientBrush.EndPoint), new(1.25f, 1.25f), new(2, 1.25f));
        _moving = true;
    }

    private void AnimatePoint(string property, Vector2 from, Vector2 to)
    {
        var compositor = _gradient!.Compositor;
        using var animation = compositor.CreateVector2KeyFrameAnimation();
        using var ease = compositor.CreateCubicBezierEasingFunction(new(.42f, 0), new(.58f, 1));
        animation.InsertKeyFrame(0, from);
        animation.InsertKeyFrame(.5f, to, ease);
        animation.InsertKeyFrame(1, from, ease);
        animation.Duration = TimeSpan.FromSeconds(4);
        animation.IterationBehavior = AnimationIterationBehavior.Forever;
        _gradient.StartAnimation(property, animation);
    }

    private void StopMotion()
    {
        if (_gradient != null)
        {
            _gradient.StopAnimation(nameof(CompositionLinearGradientBrush.StartPoint));
            _gradient.StopAnimation(nameof(CompositionLinearGradientBrush.EndPoint));
            _gradient.StartPoint = Vector2.Zero;
            _gradient.EndPoint = Vector2.One;
        }
        _moving = false;
    }

    private void AnimateOpacity(float target)
    {
        using var animation = _scene!.Compositor.CreateScalarKeyFrameAnimation();
        animation.InsertExpressionKeyFrame(0, "this.StartingValue");
        animation.InsertKeyFrame(1, target);
        animation.Duration = TimeSpan.FromMilliseconds(180);
        _scene.StartAnimation(nameof(Visual.Opacity), animation);
    }

    private sealed partial class AuroraEdgeAutomationPeer(AuroraEdge owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(AuroraEdge);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;
    }
}
