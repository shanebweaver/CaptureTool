using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using CaptureTool.Edit.Image.Win2D;
using CaptureTool.Edit.Image.Win2D.Drawable;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace CaptureTool.UI.Xaml.Controls.ImageCanvas;

public sealed partial class ImageCanvas : UserControl
{
    private static readonly DependencyProperty DrawablesProperty = DependencyProperty.Register(
        nameof(Drawables),
        typeof(IEnumerable<IDrawable>),
        typeof(ImageCanvas),
        new PropertyMetadata(null));

    private static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation),
        typeof(RotateFlipType),
        typeof(ImageCanvas),
        new PropertyMetadata(RotateFlipType.RotateNoneFlipNone, OnOrientationPropertyChanged));

    private static readonly DependencyProperty CanvasSizeProperty = DependencyProperty.Register(
        nameof(CanvasSize),
        typeof(Size),
        typeof(ImageCanvas),
        new PropertyMetadata(new Size(0,0), OnCanvasSizePropertyChanged));

    private static readonly DependencyProperty IsCropModeEnabledProperty = DependencyProperty.Register(
        nameof(IsCropModeEnabled),
        typeof(bool),
        typeof(ImageCanvas),
        new PropertyMetadata(false, OnIsCropModeEnabledPropertyChanged));

    private static void OnIsCropModeEnabledPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageCanvas control)
        {
            if (e.NewValue is bool isCropModeEnabled && isCropModeEnabled)
            {
                control.RootContainer.Background = new SolidColorBrush(Colors.Black);
            }
            else
            {
                control.RootContainer.Background = new SolidColorBrush(Colors.Transparent);
            }

            control.ZoomAndCenter();
        }
    }

    private static void OnOrientationPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageCanvas control)
        {
            control.UpdateDrawingCanvasSize();
            control.ZoomAndCenter();
        }
    }

    private static void OnCanvasSizePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageCanvas control)
        {
            control.UpdateDrawingCanvasSize();
            control.ZoomAndCenter();
        }
    }

    public IEnumerable<IDrawable> Drawables
    {
        get => (IEnumerable<IDrawable>)GetValue(DrawablesProperty);
        set => SetValue(DrawablesProperty, value);
    }

    public RotateFlipType Orientation
    {
        get => (RotateFlipType)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public Size CanvasSize
    {
        get => (Size)GetValue(CanvasSizeProperty);
        set => SetValue(CanvasSizeProperty, value);
    }

    public bool IsCropModeEnabled
    {
        get => (bool)GetValue(IsCropModeEnabledProperty);
        set => SetValue(IsCropModeEnabledProperty, value);
    }

    private bool _isPointerDown;
    private Windows.Foundation.Point _lastPointerPosition;

    public ImageCanvas()
    {
        InitializeComponent();
    }

    private void UpdateDrawingCanvasSize()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            // Check if orientation is turned by 90 or 270 degrees
            RotateFlipType orientation = Orientation;
            bool isTurned =
                orientation == RotateFlipType.Rotate90FlipNone ||
                orientation == RotateFlipType.Rotate90FlipX ||
                orientation == RotateFlipType.Rotate90FlipY ||
                orientation == RotateFlipType.Rotate90FlipXY;

            CanvasContainer.Height = isTurned ? CanvasSize.Width : CanvasSize.Height;
            CanvasContainer.Width = isTurned ? CanvasSize.Height : CanvasSize.Width;
            RenderCanvas.Height = isTurned ? CanvasSize.Width : CanvasSize.Height;
            RenderCanvas.Width = isTurned ? CanvasSize.Height : CanvasSize.Width;
            RenderCanvas.Invalidate();
        });
    }

    #region Zoom and Center
    private void RootContainer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ZoomAndCenter();
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
            ImageCanvasRenderOptions options = new(Orientation, CanvasSize);
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

    #region Cropping
    private bool _isResizing;
    private Windows.Foundation.Point _startPoint;
    private ResizeDirection? _resizeDirection;

    private void CropBoundary_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _startPoint = e.GetCurrentPoint(RootContainer).Position;
        _isResizing = true;

        // Determine which edge or corner is being grabbed
        var position = e.GetCurrentPoint(CropBoundary).Position;
        var tolerance = 10; // Tolerance for detecting edges
        _resizeDirection = GetResizeDirection(position, tolerance);

        (sender as UIElement)?.CapturePointer(e.Pointer);
    }

    private void CropBoundary_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_isResizing && _resizeDirection != null)
        {
            var currentPoint = e.GetCurrentPoint(RootContainer).Position;
            var deltaX = currentPoint.X - _startPoint.X;
            var deltaY = currentPoint.Y - _startPoint.Y;

            // Resize the CropBoundary based on the direction
            ResizeCropBoundary(deltaX, deltaY, _resizeDirection);

            _startPoint = currentPoint;
        }
    }

    private void CropBoundary_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isResizing = false;
        _resizeDirection = null;
        (sender as UIElement)?.ReleasePointerCapture(e.Pointer);
    }

    private ResizeDirection? GetResizeDirection(Windows.Foundation.Point position, double tolerance)
    {
        // Check if the pointer is near an edge or corner
        if (position.X <= tolerance && position.Y <= tolerance) return ResizeDirection.TopLeft;
        if (position.X >= CropBoundary.ActualWidth - tolerance && position.Y <= tolerance) return ResizeDirection.TopRight;
        if (position.X <= tolerance && position.Y >= CropBoundary.ActualHeight - tolerance) return ResizeDirection.BottomLeft;
        if (position.X >= CropBoundary.ActualWidth - tolerance && position.Y >= CropBoundary.ActualHeight - tolerance) return ResizeDirection.BottomRight;
        if (position.X <= tolerance) return ResizeDirection.Left;
        if (position.X >= CropBoundary.ActualWidth - tolerance) return ResizeDirection.Right;
        if (position.Y <= tolerance) return ResizeDirection.Top;
        if (position.Y >= CropBoundary.ActualHeight - tolerance) return ResizeDirection.Bottom;

        return null;
    }

    private enum ResizeDirection
    {
        TopLeft,
        Top,
        TopRight,
        Left,
        Right,
        BottomLeft,
        Bottom,
        BottomRight
    }

    private void ResizeCropBoundary(double deltaX, double deltaY, ResizeDirection? direction)
    {
        // Adjust the CropBoundary size and position based on the direction
        switch (direction)
        {
            case ResizeDirection.TopLeft:
                CropBoundary.Width = Math.Max(0, CropBoundary.ActualWidth - deltaX);
                CropBoundary.Height = Math.Max(0, CropBoundary.ActualHeight - deltaY);
                Canvas.SetLeft(CropBoundary, Canvas.GetLeft(CropBoundary) + deltaX);
                Canvas.SetTop(CropBoundary, Canvas.GetTop(CropBoundary) + deltaY);
                break;
            case ResizeDirection.TopRight:
                CropBoundary.Width = Math.Max(0, CropBoundary.ActualWidth + deltaX);
                CropBoundary.Height = Math.Max(0, CropBoundary.ActualHeight - deltaY);
                Canvas.SetTop(CropBoundary, Canvas.GetTop(CropBoundary) + deltaY);
                break;
            case ResizeDirection.BottomLeft:
                CropBoundary.Width = Math.Max(0, CropBoundary.ActualWidth - deltaX);
                CropBoundary.Height = Math.Max(0, CropBoundary.ActualHeight + deltaY);
                Canvas.SetLeft(CropBoundary, Canvas.GetLeft(CropBoundary) + deltaX);
                break;
            case ResizeDirection.BottomRight:
                CropBoundary.Width = Math.Max(0, CropBoundary.ActualWidth + deltaX);
                CropBoundary.Height = Math.Max(0, CropBoundary.ActualHeight + deltaY);
                break;
            case ResizeDirection.Left:
                CropBoundary.Width = Math.Max(0, CropBoundary.ActualWidth - deltaX);
                Canvas.SetLeft(CropBoundary, Canvas.GetLeft(CropBoundary) + deltaX);
                break;
            case ResizeDirection.Right:
                CropBoundary.Width = Math.Max(0, CropBoundary.ActualWidth + deltaX);
                break;
            case ResizeDirection.Top:
                CropBoundary.Height = Math.Max(0, CropBoundary.ActualHeight - deltaY);
                Canvas.SetTop(CropBoundary, Canvas.GetTop(CropBoundary) + deltaY);
                break;
            case ResizeDirection.Bottom:
                CropBoundary.Height = Math.Max(0, CropBoundary.ActualHeight + deltaY);
                break;
        }
    }
    #endregion

}
