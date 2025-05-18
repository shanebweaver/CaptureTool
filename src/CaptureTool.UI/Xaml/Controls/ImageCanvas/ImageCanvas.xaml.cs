using CaptureTool.Edit.Image.Win2D;
using CaptureTool.Edit.Image.Win2D.Drawable;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;

namespace CaptureTool.UI.Xaml.Controls.ImageCanvas;

public sealed partial class ImageCanvas : UserControlBase
{
    public static readonly DependencyProperty DrawablesProperty = DependencyProperty.Register(
        nameof(Drawables),
        typeof(IEnumerable<IDrawable>),
        typeof(ImageCanvas),
        new PropertyMetadata(null));

    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation),
        typeof(RotateFlipType),
        typeof(ImageCanvas),
        new PropertyMetadata(RotateFlipType.RotateNoneFlipNone, OnOrientationPropertyChanged));

    public static readonly DependencyProperty CanvasSizeProperty = DependencyProperty.Register(
        nameof(CanvasSize),
        typeof(Size),
        typeof(ImageCanvas),
        new PropertyMetadata(new Size(0, 0), OnCanvasSizePropertyChanged));

    public static readonly DependencyProperty IsCropModeEnabledProperty = DependencyProperty.Register(
        nameof(IsCropModeEnabled),
        typeof(bool),
        typeof(ImageCanvas),
        new PropertyMetadata(false, OnIsCropModeEnabledPropertyChanged));

    public static readonly DependencyProperty CropRectProperty = DependencyProperty.Register(
       nameof(CropRect),
       typeof(Windows.Foundation.Rect),
       typeof(ImageCanvas),
       new PropertyMetadata(Windows.Foundation.Rect.Empty, OnCropRectPropertyChanged));

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
                    control.RootContainer.Background = new SolidColorBrush(Colors.Black);
                }
                else
                {
                    control.RootContainer.Background = new SolidColorBrush(Colors.Transparent);
                }

                control.UpdateDrawingCanvasSize();
                control.ZoomAndCenter();
            });
        }
    }

    private static Windows.Foundation.Rect GetOrientedRect(
        Windows.Foundation.Rect cropRect,
        Size canvasSize,
        RotateFlipType oldOrientation,
        RotateFlipType newOrientation)
    {
        // Convert CropRect to normalized coordinates (0-1)
        double normX = cropRect.X / canvasSize.Width;
        double normY = cropRect.Y / canvasSize.Height;
        double normW = cropRect.Width / canvasSize.Width;
        double normH = cropRect.Height / canvasSize.Height;

        // Helper to rotate/flip normalized rect
        static (double x, double y, double w, double h) TransformRect(
            double x, double y, double w, double h,
            RotateFlipType from, RotateFlipType to)
        {
            // First, undo the old orientation (to get to "upright" space)
            (x, y, w, h) = ApplyOrientationInverse(x, y, w, h, from);
            // Then, apply the new orientation
            (x, y, w, h) = ApplyOrientation(x, y, w, h, to);
            return (x, y, w, h);
        }

        // Undo orientation: bring rect to upright
        static (double x, double y, double w, double h) ApplyOrientationInverse(
            double x, double y, double w, double h, RotateFlipType orientation)
        {
            // Only handle 90/180/270 and flips
            switch (orientation)
            {
                case RotateFlipType.Rotate90FlipNone:
                    return (y, 1 - x - w, h, w);
                case RotateFlipType.Rotate180FlipNone:
                    return (1 - x - w, 1 - y - h, w, h);
                case RotateFlipType.Rotate270FlipNone:
                    return (1 - y - h, x, h, w);
                case RotateFlipType.RotateNoneFlipX:
                    return (1 - x - w, y, w, h);
                case RotateFlipType.RotateNoneFlipY:
                    return (x, 1 - y - h, w, h);
                // Add more cases as needed
                default:
                    return (x, y, w, h);
            }
        }

        // Apply orientation: from upright to new orientation
        static (double x, double y, double w, double h) ApplyOrientation(
            double x, double y, double w, double h, RotateFlipType orientation)
        {
            switch (orientation)
            {
                case RotateFlipType.Rotate90FlipNone:
                    return (1 - y - h, x, h, w);
                case RotateFlipType.Rotate180FlipNone:
                    return (1 - x - w, 1 - y - h, w, h);
                case RotateFlipType.Rotate270FlipNone:
                    return (y, 1 - x - w, h, w);
                case RotateFlipType.RotateNoneFlipX:
                    return (1 - x - w, y, w, h);
                case RotateFlipType.RotateNoneFlipY:
                    return (x, 1 - y - h, w, h);
                // Add more cases as needed
                default:
                    return (x, y, w, h);
            }
        }

        (double newNormX, double newNormY, double newNormW, double newNormH) =
            TransformRect(normX, normY, normW, normH, oldOrientation, newOrientation);

        // Clamp to [0,1]
        newNormX = Math.Max(0, Math.Min(1, newNormX));
        newNormY = Math.Max(0, Math.Min(1, newNormY));
        newNormW = Math.Max(0, Math.Min(1 - newNormX, newNormW));
        newNormH = Math.Max(0, Math.Min(1 - newNormY, newNormH));

        // Convert back to pixel coordinates
        return new Windows.Foundation.Rect(
            newNormX * canvasSize.Width,
            newNormY * canvasSize.Height,
            newNormW * canvasSize.Width,
            newNormH * canvasSize.Height
        );
    }

    private static void OnOrientationPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageCanvas control)
        {
            control.DispatcherQueue.TryEnqueue(() =>
            {

                // Orient the CropRect based on the new orientation
                if (e.OldValue is RotateFlipType oldOrientation &&
                    e.NewValue is RotateFlipType newOrientation &&
                    control.CropRect != Windows.Foundation.Rect.Empty &&
                    control.CanvasSize.Width > 0 && control.CanvasSize.Height > 0)
                {
                    control.UpdateDrawingCanvasSize();

                    var crop = control.CropRect;
                    var canvasSize = control.CanvasSize;
                    control.CropRect = GetOrientedRect(crop, canvasSize, oldOrientation, newOrientation);
                }

                control.UpdateDrawingCanvasSize();
                control.ZoomAndCenter();
            });
        }
    }

    private static void OnCanvasSizePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageCanvas control)
        {
            control.DispatcherQueue.TryEnqueue(() =>
            {
                control.UpdateDrawingCanvasSize();
                control.ZoomAndCenter();
            });
        }
    }

    public IEnumerable<IDrawable> Drawables
    {
        get => Get<IEnumerable<IDrawable>>(DrawablesProperty);
        set => Set(DrawablesProperty, value);
    }

    public RotateFlipType Orientation
    {
        get => Get<RotateFlipType>(OrientationProperty);
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

    public Windows.Foundation.Rect CropRect
    {
        get => Get<Windows.Foundation.Rect>(CropRectProperty);
        set => Set(CropRectProperty, value);
    }

    private bool _isPointerDown;
    private Windows.Foundation.Point _lastPointerPosition;

    public ImageCanvas()
    {
        InitializeComponent();
    }

    #region Zoom, Center, and Size
    private void RootContainer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateDrawingCanvasSize();
            ZoomAndCenter();
        });
    }

    private void UpdateDrawingCanvasSize()
    {
        lock (this)
        {
            RotateFlipType orientation = Orientation;
            bool isTurned =
                orientation == RotateFlipType.Rotate90FlipNone ||
                orientation == RotateFlipType.Rotate90FlipX ||
                orientation == RotateFlipType.Rotate90FlipY ||
                orientation == RotateFlipType.Rotate90FlipXY;

            double width, height;

            if (IsCropModeEnabled)
            {
                height = isTurned ? CanvasSize.Width : CanvasSize.Height;
                width = isTurned ? CanvasSize.Height : CanvasSize.Width;
            }
            else
            {
                // Use CropRect dimensions when crop mode is not enabled
                var crop = CropRect;
                height = isTurned ? crop.Width : crop.Height;
                width = isTurned ? crop.Height : crop.Width;

                // Fallback to CanvasSize if CropRect is empty or invalid
                if (width <= 0 || height <= 0)
                {
                    height = isTurned ? CanvasSize.Width : CanvasSize.Height;
                    width = isTurned ? CanvasSize.Height : CanvasSize.Width;
                }
            }

            CanvasContainer.Height = height;
            CanvasContainer.Width = width;

            CropOverlay.Height = height;
            CropOverlay.Width = width;

            RenderCanvas.Height = height;
            RenderCanvas.Width = width;
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

            bool isTurned =
                Orientation == RotateFlipType.Rotate90FlipNone ||
                Orientation == RotateFlipType.Rotate90FlipX ||
                Orientation == RotateFlipType.Rotate90FlipY ||
                Orientation == RotateFlipType.Rotate90FlipXY;

            double containerWidth = RootContainer.ActualWidth;
            double containerHeight = RootContainer.ActualHeight;

            double canvasWidth = isTurned ? CanvasSize.Height : CanvasSize.Width;
            double canvasHeight = isTurned ? CanvasSize.Width : CanvasSize.Height;

            double padding = 48;
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
        }
    }
    #endregion

    #region Drawing
    private void CanvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        lock (this)
        {
            var rect = (!IsCropModeEnabled) ? CropRect : new Windows.Foundation.Rect(0, 0, CanvasSize.Width, CanvasSize.Height);

            ImageCanvasRenderOptions options = new(Orientation, CanvasSize, rect);
            ImageCanvasRenderer.Render([.. Drawables], options, args.DrawingSession);
        }
    }

    private void CanvasControl_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
    {
        // Create any resources needed by the Draw event handler.

        // Asynchronous work can be tracked with TrackAsyncAction:
        args.TrackAsyncAction(CreateResourcesAsync(sender).AsAsyncAction());
    }

    private async Task CreateResourcesAsync(CanvasControl sender)
    {
        // Load bitmaps, create brushes, etc.
        List<Task> preparationTasks = [];

        foreach (IDrawable drawable in Drawables)
        {
            if (drawable is ImageDrawable imageDrawable)
            {
                Task prepTask = imageDrawable.PrepareAsync(sender);
                preparationTasks.Add(prepTask);
            }
        }

        await Task.WhenAll(preparationTasks);
    }
    #endregion

    #region Panning
    private void RootContainer_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isPointerDown = true;
        _lastPointerPosition = e.GetCurrentPoint(RootContainer).Position;
        RootContainer.CapturePointer(e.Pointer);
    }

    private void RootContainer_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_isPointerDown)
        {
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
        _isPointerDown = false;
        RootContainer.ReleasePointerCaptures();
    }

    private void RootContainer_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        _isPointerDown = false;
        RootContainer.ReleasePointerCaptures();
    }

    private void RootContainer_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isPointerDown = false;
    }
    #endregion
}
