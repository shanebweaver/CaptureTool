using CaptureTool.Domain.Edit.Implementations.Windows;
using CaptureTool.Domain.Edit.Interfaces;
using CaptureTool.Domain.Edit.Interfaces.Drawable;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Drawing;
using Point = Windows.Foundation.Point;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class ImageCanvas : UserControlBase
{
    public static readonly DependencyProperty DrawablesProperty = DependencyProperty.Register(
        nameof(Drawables),
        typeof(IEnumerable<IDrawable>),
        typeof(ImageCanvas),
        new PropertyMetadata(null));

    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation),
        typeof(ImageOrientation),
        typeof(ImageCanvas),
        new PropertyMetadata(ImageOrientation.RotateNoneFlipNone, OnOrientationPropertyChanged));

    public static readonly DependencyProperty CanvasSizeProperty = DependencyProperty.Register(
        nameof(CanvasSize),
        typeof(Size),
        typeof(ImageCanvas),
        new PropertyMetadata(Size.Empty, OnCanvasSizePropertyChanged));

    public static readonly DependencyProperty IsCropModeEnabledProperty = DependencyProperty.Register(
        nameof(IsCropModeEnabled),
        typeof(bool),
        typeof(ImageCanvas),
        new PropertyMetadata(false, OnIsCropModeEnabledPropertyChanged));

    public static readonly DependencyProperty IsShapesModeEnabledProperty = DependencyProperty.Register(
        nameof(IsShapesModeEnabled),
        typeof(bool),
        typeof(ImageCanvas),
        new PropertyMetadata(false));

    public static readonly DependencyProperty CropRectProperty = DependencyProperty.Register(
       nameof(CropRect),
       typeof(Rectangle),
       typeof(ImageCanvas),
       new PropertyMetadata(Rectangle.Empty, OnCropRectPropertyChanged));

    public static readonly DependencyProperty IsAutoZoomLockedProperty = DependencyProperty.Register(
       nameof(IsAutoZoomLocked),
       typeof(bool),
       typeof(ImageCanvas),
       new PropertyMetadata(false));

    private static void OnCropRectPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageCanvas control && !control.IsCropModeEnabled)
        {
            control.RenderCanvas.Invalidate();
        }
    }

    private static void OnIsCropModeEnabledPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageCanvas control)
        {
            control.DispatcherQueue.TryEnqueue(() =>
            {
                if (e.NewValue is bool isCropModeEnabled && isCropModeEnabled)
                {
                    control.DimmedBackgroundRectangle.Visibility = Visibility.Visible;
                }
                else
                {
                    control.DimmedBackgroundRectangle.Visibility = Visibility.Collapsed;
                }
            });

            control.InvalidateCanvas();
        }
    }

    private static void OnOrientationPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageCanvas control)
        {
            control.InvalidateCanvas();
        }
    }

    private static void OnCanvasSizePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageCanvas control)
        {
            control.InvalidateCanvas();
        }
    }

    public IEnumerable<IDrawable> Drawables
    {
        get => Get<IEnumerable<IDrawable>>(DrawablesProperty);
        set => Set(DrawablesProperty, value);
    }

    public ImageOrientation Orientation
    {
        get => Get<ImageOrientation>(OrientationProperty);
        set => Set(OrientationProperty, value);
    }

    public Size CanvasSize
    {
        get => Get<Size>(CanvasSizeProperty);
        set => Set(CanvasSizeProperty, value);
    }

    public bool IsCropModeEnabled
    {
        get => Get<bool>(IsCropModeEnabledProperty);
        set => Set(IsCropModeEnabledProperty, value);
    }

    public bool IsShapesModeEnabled
    {
        get => Get<bool>(IsShapesModeEnabledProperty);
        set => Set(IsShapesModeEnabledProperty, value);
    }

    public Rectangle CropRect
    {
        get => Get<Rectangle>(CropRectProperty);
        set => Set(CropRectProperty, value);
    }

    public bool IsAutoZoomLocked
    {
        get => Get<bool>(IsAutoZoomLockedProperty);
        set => Set(IsAutoZoomLockedProperty, value);
    }

    public event EventHandler<Rectangle>? InteractionComplete;
    public event EventHandler<Rectangle>? CropRectChanged;
    public event EventHandler<(System.Numerics.Vector2 Start, System.Numerics.Vector2 End)>? ShapeDrawn;
    public event EventHandler<(double ZoomFactor, ZoomUpdateSource Source)>? ZoomFactorChanged;

    private readonly Lock _zoomUpdateLock = new Lock();

    private bool _isPointerDown;
    private Point _lastPointerPosition;
    private Point? _shapeStartPoint;

    public ImageCanvas()
    {
        InitializeComponent();
        Loaded += ImageCanvas_Loaded;
        Unloaded += ImageCanvas_Unloaded;
    }

    private void ImageCanvas_Loaded(object sender, RoutedEventArgs e)
    {
        // Wire up the ViewChanged event after the control is loaded
        if (CanvasScrollView != null)
        {
            CanvasScrollView.ViewChanged += CanvasScrollView_ViewChanged;
        }
    }

    private void ImageCanvas_Unloaded(object sender, RoutedEventArgs e)
    {
        // Clean up event subscription to prevent memory leak
        if (CanvasScrollView != null)
        {
            CanvasScrollView.ViewChanged -= CanvasScrollView_ViewChanged;
        }
    }

    private void CanvasScrollView_ViewChanged(ScrollView? sender, object args)
    {
        if (sender == null)
        {
            return;
        }

        lock (_zoomUpdateLock)
        {
            double currentZoomFactor = sender.ZoomFactor;
            // Only raise event if zoom actually changed (not just scroll position)
            if (Math.Abs(currentZoomFactor - CanvasScrollView.ZoomFactor) > 1)
            {
                ZoomFactorChanged?.Invoke(this, (currentZoomFactor, ZoomUpdateSource.CanvasGesture));
            }
        }
    }

    #region Zoom, Center, and Size
    private void RootContainer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        InvalidateCanvas();
    }

    private bool IsTurned()
    {
        ImageOrientation orientation = Orientation;
        bool isTurned =
            orientation == ImageOrientation.Rotate90FlipNone ||
            orientation == ImageOrientation.Rotate90FlipX ||
            orientation == ImageOrientation.Rotate270FlipNone ||
            orientation == ImageOrientation.Rotate270FlipX;

        return isTurned;
    }

    private Size GetImageRenderSize()
    {
        bool isTurned = IsTurned();

        double canvasWidth, canvasHeight;

        if (IsCropModeEnabled)
        {
            canvasHeight = isTurned ? CanvasSize.Width : CanvasSize.Height;
            canvasWidth = isTurned ? CanvasSize.Height : CanvasSize.Width;
        }
        else
        {
            // Use CropRect dimensions when crop mode is not enabled
            var crop = CropRect;
            canvasHeight = crop.Height;
            canvasWidth = crop.Width;

            // Fallback to CanvasSize if CropRect is empty or invalid
            if (canvasWidth <= 0 || canvasHeight <= 0)
            {
                canvasHeight = isTurned ? CanvasSize.Width : CanvasSize.Height;
                canvasWidth = isTurned ? CanvasSize.Height : CanvasSize.Width;
            }
        }

        return new((int)canvasWidth, (int)canvasHeight);
    }

    private void UpdateDrawingCanvasSize()
    {
        lock (this)
        {
            Size renderSize = GetImageRenderSize();
            int width = renderSize.Width;
            int height = renderSize.Height;

            CanvasContainer.Width = width;
            CanvasContainer.Height = height;

            CropOverlay.Width = width;
            CropOverlay.Height = height;

            RenderCanvas.Width = width;
            RenderCanvas.Height = height;
            RenderCanvas.Invalidate();
        }
    }

    private void ZoomAndCenter()
    {
        lock (this)
        {
            if (CanvasScrollView == null || RootContainer == null || CanvasSize.Width == 0 || CanvasSize.Height == 0)
            {
                return;
            }

            double containerWidth = RootContainer.ActualWidth;
            double containerHeight = RootContainer.ActualHeight;

            Size renderSize = GetImageRenderSize();
            int canvasWidth = renderSize.Width;
            int canvasHeight = renderSize.Height;

            // Add padding
            int padding = 48;
            canvasWidth += padding;
            canvasHeight += padding;

            double scaleX = containerWidth / canvasWidth;
            double scaleY = containerHeight / canvasHeight;

            // Choose the smaller scale to ensure the image fits within the container
            double targetZoomFactor = Math.Min(1, Math.Min(scaleX, scaleY));

            CanvasScrollView.ZoomTo(
                (float)targetZoomFactor,
                null,
                new(ScrollingAnimationMode.Auto)
            );

            ZoomFactorChanged?.Invoke(this, (targetZoomFactor, ZoomUpdateSource.ZoomAndCenter));
        }
    }

    public void ForceZoomAndCenter()
    {
        UpdateDrawingCanvasSize();
        ZoomAndCenter();
    }

    public void SetZoom(double zoomLevel, ZoomUpdateSource source)
    {
        lock (this)
        {
            if (CanvasScrollView == null)
            {
                return;
            }

            CanvasScrollView.ZoomTo(
                (float)zoomLevel,
                null,
                new(ScrollingAnimationMode.Auto)
            );
        }

        // For non-programmatic sources, fire the event immediately
        if (source != ZoomUpdateSource.Programmatic)
        {
            ZoomFactorChanged?.Invoke(this, (zoomLevel, source));
        }
    }
    #endregion

    #region Drawing
    public void InvalidateCanvas()
    {
        UpdateDrawingCanvasSize();
        if (!IsAutoZoomLocked)
        {
            ZoomAndCenter();
        }
    }

    public void ForceCanvasRedrawWithResources()
    {
        RenderCanvas.DpiScale = RenderCanvas.DpiScale == 1 ? 1.0001f : 1f;
        InvalidateCanvas();
    }

    private void CanvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        lock (this)
        {
            var rect = (!IsCropModeEnabled) ? CropRect : IsTurned() ? new Rectangle(0, 0, CanvasSize.Height, CanvasSize.Width) : new Rectangle(0, 0, CanvasSize.Width, CanvasSize.Height);
            ImageCanvasRenderOptions options = new(Orientation, CanvasSize, rect);
            Win2DImageCanvasRenderer.Render([.. Drawables], options, args.DrawingSession);
        }
    }

    private bool CanCreateResources() => Drawables.Any();

    private void CanvasControl_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
    {
        if (CanCreateResources())
        {
            // Create any resources needed by the Draw event handler.
            // Asynchronous work can be tracked with TrackAsyncAction:
            args.TrackAsyncAction(CreateResourcesAsync(sender).AsAsyncAction());
        }
    }

    private async Task CreateResourcesAsync(CanvasControl sender)
    {
        // Load bitmaps, create brushes, etc.
        List<Task> preparationTasks = [];

        foreach (IDrawable drawable in Drawables)
        {
            if (drawable is ImageDrawable imageDrawable)
            {
                Task prepTask = Win2DImageCanvasRenderer.PrepareAsync(imageDrawable, sender);
                preparationTasks.Add(prepTask);
            }
        }

        await Task.WhenAll(preparationTasks);
    }
    #endregion

    #region Panning
    private void RootContainer_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (IsShapesModeEnabled)
        {
            // Get position relative to the RenderCanvas
            var point = e.GetCurrentPoint(RenderCanvas);
            if (point.Properties.IsLeftButtonPressed)
            {
                _shapeStartPoint = point.Position;
                _isPointerDown = true;
                RootContainer.CapturePointer(e.Pointer);
                e.Handled = true;
                return;
            }
        }

        _isPointerDown = true;
        _lastPointerPosition = e.GetCurrentPoint(RootContainer).Position;
        RootContainer.CapturePointer(e.Pointer);
    }

    private void RootContainer_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_isPointerDown)
        {
            // In shapes mode, don't pan
            if (IsShapesModeEnabled && _shapeStartPoint.HasValue)
            {
                e.Handled = true;
                return;
            }

            var currentPosition = e.GetCurrentPoint(RootContainer).Position;
            double deltaX = _lastPointerPosition.X - currentPosition.X;
            double deltaY = _lastPointerPosition.Y - currentPosition.Y;

            if (deltaX == 0 && deltaY == 0)
            {
                return;
            }

            CanvasScrollView.ScrollBy(
                deltaX,
                deltaY,
                new(ScrollingAnimationMode.Disabled)
            );

            _lastPointerPosition = currentPosition;
        }
    }

    private void RootContainer_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (IsShapesModeEnabled && _shapeStartPoint.HasValue)
        {
            // Get end position relative to the RenderCanvas
            var endPoint = e.GetCurrentPoint(RenderCanvas).Position;

            // Convert to Vector2 and invoke event
            var start = new System.Numerics.Vector2((float)_shapeStartPoint.Value.X, (float)_shapeStartPoint.Value.Y);
            var end = new System.Numerics.Vector2((float)endPoint.X, (float)endPoint.Y);

            ShapeDrawn?.Invoke(this, (start, end));

            _shapeStartPoint = null;
            e.Handled = true;
        }

        _isPointerDown = false;
        RootContainer.ReleasePointerCaptures();
    }

    private void RootContainer_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        _isPointerDown = false;
        _shapeStartPoint = null;
        RootContainer.ReleasePointerCaptures();
    }

    private void RootContainer_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isPointerDown = false;
        _shapeStartPoint = null;
    }
    #endregion

    private void UpdateCropRect(Rectangle value)
    {
        CropRect = value;
        CropRectChanged?.Invoke(this, value);
    }

    private void CropOverlay_InteractionComplete(object sender, Rectangle e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            InteractionComplete?.Invoke(this, e);
        });
    }

    private void CropOverlay_SelectionAreaChanged(object sender, Rectangle e)
    {
        UpdateCropRect(e);
    }
}
