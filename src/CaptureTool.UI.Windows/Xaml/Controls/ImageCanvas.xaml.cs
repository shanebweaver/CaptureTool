using CaptureTool.Edit;
using CaptureTool.Edit.Drawable;
using CaptureTool.Edit.Windows;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;

using Point = Windows.Foundation.Point;

namespace CaptureTool.UI.Windows.Xaml.Controls;

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

    public static readonly DependencyProperty CropRectProperty = DependencyProperty.Register(
       nameof(CropRect),
       typeof(Rectangle),
       typeof(ImageCanvas),
       new PropertyMetadata(Rectangle.Empty, OnCropRectPropertyChanged));

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
                    control.CanvasScrollView.ZoomMode = ScrollingZoomMode.Disabled;
                }
                else
                {
                    control.CanvasScrollView.ZoomMode = ScrollingZoomMode.Enabled;
                    control.DimmedBackgroundRectangle.Visibility = Visibility.Collapsed;
                }

                control.UpdateDrawingCanvasSize();
                control.ZoomAndCenter();
            });
        }
    }

    private static void OnOrientationPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
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

    public Rectangle CropRect
    {
        get => Get<Rectangle>(CropRectProperty);
        set => Set(CropRectProperty, value);
    }

    private bool _isPointerDown;
    private Point _lastPointerPosition;

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

    private Size GetImageRenderSize()
    {
        ImageOrientation orientation = Orientation;
        bool isTurned =
            orientation == ImageOrientation.Rotate90FlipNone ||
            orientation == ImageOrientation.Rotate90FlipX ||
            orientation == ImageOrientation.Rotate270FlipNone ||
            orientation == ImageOrientation.Rotate270FlipX;

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
        }
    }
    #endregion

    #region Drawing
    public void InvalidateCanvas()
    {
        RenderCanvas.Invalidate();
    }

    private void CanvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        lock (this)
        {
            var rect = (!IsCropModeEnabled) ? CropRect : new Rectangle(0, 0, CanvasSize.Width, CanvasSize.Height);

            ImageCanvasRenderOptions options = new(Orientation, CanvasSize, rect);
            Win2DImageCanvasRenderer.Render([.. Drawables], options, args.DrawingSession);
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
