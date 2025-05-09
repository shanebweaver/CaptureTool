using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using CaptureTool.Edit.Image.Win2D;
using CaptureTool.Edit.Image.Win2D.Drawable;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Core;

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
                //control.RootContainer.Background = new SolidColorBrush(Colors.Black);
            }
            else
            {
                //control.RootContainer.Background = new SolidColorBrush(Colors.Transparent);
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

            double height = isTurned ? CanvasSize.Width : CanvasSize.Height;
            double width = isTurned ? CanvasSize.Height : CanvasSize.Width;

            CanvasContainer.Height = height;
            CanvasContainer.Width = width;
            
            CropCanvas.Height = height;
            CropCanvas.Width = width;

            CropBoundary.Height = height;
            CropBoundary.Width = width;
            Canvas.SetLeft(CropBoundary, 0);
            Canvas.SetTop(CropBoundary, 0);

            RenderCanvas.Height = height;
            RenderCanvas.Width = width;
            RenderCanvas.Invalidate();
        });
    }

    #region Zoom and Center
    private void RootContainer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateDrawingCanvasSize();
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
    //private const int _tolerance = 12;
    //private bool _isResizing;
    //private bool _isDragging;
    //private Windows.Foundation.Point _startPoint;
    //private ResizeDirection? _resizeDirection;

    //private void CropCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    //{
    //    _isResizing = false;
    //    _isDragging = false;
    //    _startPoint = e.GetCurrentPoint(RootContainer).Position;

    //    var position = e.GetCurrentPoint(CropBoundary).Position;

    //    // Check if the pointer is outside the CropBoundary  
    //    if (position.X < 0 - _tolerance || position.Y < 0 - _tolerance || position.X > CropBoundary.ActualWidth + _tolerance || position.Y > CropBoundary.ActualHeight + _tolerance)
    //    {
    //        return;
    //    }

    //    _resizeDirection = GetResizeDirection(position, _tolerance);

    //    if (_resizeDirection == null)
    //    {
    //        _isDragging = true;
    //    }
    //    else
    //    {
    //        _isResizing = true;
    //    }

    //   (sender as UIElement)?.CapturePointer(e.Pointer);
    //}

    //private void CropCanvas_PointerEntered(object sender, PointerRoutedEventArgs e)
    //{
    //    UpdateCursor(e.GetCurrentPoint(CropBoundary).Position);
    //}

    //private void CropCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    //{
    //    if (_isDragging)
    //    {
    //        var currentPoint = e.GetCurrentPoint(RootContainer).Position;

    //        // Adjust deltaX and deltaY based on the current zoom factor
    //        double zoomFactor = CanvasScrollView.ZoomFactor;
    //        var deltaX = (currentPoint.X - _startPoint.X) / zoomFactor;
    //        var deltaY = (currentPoint.Y - _startPoint.Y) / zoomFactor;

    //        // Translate the CropBoundary
    //        var newLeft = Math.Max(0, Math.Min(Canvas.GetLeft(CropBoundary) + deltaX, CanvasSize.Width - CropBoundary.ActualWidth));
    //        var newTop = Math.Max(0, Math.Min(Canvas.GetTop(CropBoundary) + deltaY, CanvasSize.Height - CropBoundary.ActualHeight));

    //        Canvas.SetLeft(CropBoundary, newLeft);
    //        Canvas.SetTop(CropBoundary, newTop);

    //        _startPoint = currentPoint;
    //    }
    //    else if (_isResizing)
    //    {
    //        var currentPoint = e.GetCurrentPoint(RootContainer).Position;

    //        // Adjust deltaX and deltaY based on the current zoom factor
    //        double zoomFactor = CanvasScrollView.ZoomFactor;
    //        var deltaX = (currentPoint.X - _startPoint.X) / zoomFactor;
    //        var deltaY = (currentPoint.Y - _startPoint.Y) / zoomFactor;

    //        ResizeCropBoundary(deltaX, deltaY, _resizeDirection);

    //        _startPoint = currentPoint;
    //    }
    //    else
    //    {
    //        UpdateCursor(e.GetCurrentPoint(CropBoundary).Position);
    //    }
    //}


    //private void CropCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
    //{
    //    ProtectedCursor = InputCursor.CreateFromCoreCursor(new CoreCursor(CoreCursorType.Arrow, 0));
    //}

    //private void CropCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    //{
    //    _isResizing = false;
    //    _isDragging = false;
    //    _resizeDirection = null;
    //    (sender as UIElement)?.ReleasePointerCapture(e.Pointer);
    //}

    //private void UpdateCursor(Windows.Foundation.Point position)
    //{
    //    var direction = GetResizeDirection(position, _tolerance);

    //    CoreCursorType cursorType;

    //    if (direction != null)
    //    {
    //        cursorType = direction switch
    //        {
    //            ResizeDirection.TopLeft or ResizeDirection.BottomRight => CoreCursorType.SizeNorthwestSoutheast,
    //            ResizeDirection.TopRight or ResizeDirection.BottomLeft => CoreCursorType.SizeNortheastSouthwest,
    //            ResizeDirection.Top or ResizeDirection.Bottom => CoreCursorType.SizeNorthSouth,
    //            ResizeDirection.Left or ResizeDirection.Right => CoreCursorType.SizeWestEast,
    //            _ => CoreCursorType.Arrow
    //        };
    //    }
    //    else if (IsCursorInsideBoundary(position, _tolerance))
    //    {
    //        cursorType = CoreCursorType.SizeAll; // Move cursor
    //    }
    //    else
    //    {
    //        cursorType = CoreCursorType.Arrow; // Normal pointer
    //    }

    //    // Set the cursor on the CropBoundary
    //    ProtectedCursor = InputCursor.CreateFromCoreCursor(new CoreCursor(cursorType, 0));
    //}

    //private bool IsCursorInsideBoundary(Windows.Foundation.Point position, double tolerance)
    //{
    //    return position.X >= -tolerance &&
    //           position.Y >= -tolerance &&
    //           position.X <= CropBoundary.ActualWidth + tolerance &&
    //           position.Y <= CropBoundary.ActualHeight + tolerance;
    //}

    //private ResizeDirection? GetResizeDirection(Windows.Foundation.Point position, double tolerance)
    //{
    //    // Check if the pointer is near an edge or corner within the tolerance
    //    bool isNearLeft = position.X >= -tolerance && position.X <= tolerance;
    //    bool isNearRight = position.X >= CropBoundary.ActualWidth - tolerance && position.X <= CropBoundary.ActualWidth + tolerance;
    //    bool isNearTop = position.Y >= -tolerance && position.Y <= tolerance;
    //    bool isNearBottom = position.Y >= CropBoundary.ActualHeight - tolerance && position.Y <= CropBoundary.ActualHeight + tolerance;

    //    if (isNearLeft && isNearTop) return ResizeDirection.TopLeft;
    //    if (isNearRight && isNearTop) return ResizeDirection.TopRight;
    //    if (isNearLeft && isNearBottom) return ResizeDirection.BottomLeft;
    //    if (isNearRight && isNearBottom) return ResizeDirection.BottomRight;
    //    if (isNearLeft) return ResizeDirection.Left;
    //    if (isNearRight) return ResizeDirection.Right;
    //    if (isNearTop) return ResizeDirection.Top;
    //    if (isNearBottom) return ResizeDirection.Bottom;

    //    // Return null if the position is not within the tolerance of any border
    //    return null;
    //}

    //private enum ResizeDirection
    //{
    //    TopLeft,
    //    Top,
    //    TopRight,
    //    Left,
    //    Right,
    //    BottomLeft,
    //    Bottom,
    //    BottomRight
    //}

    //private void ResizeCropBoundary(double deltaX, double deltaY, ResizeDirection? direction)
    //{
    //    switch (direction)
    //    {
    //        case ResizeDirection.TopLeft:
    //            CropBoundary.Width = Math.Max(0, Math.Min(CropBoundary.ActualWidth - deltaX, CanvasSize.Width - Math.Max(0, Canvas.GetLeft(CropBoundary) + deltaX)));
    //            CropBoundary.Height = Math.Max(0, Math.Min(CropBoundary.ActualHeight - deltaY, CanvasSize.Height - Math.Max(0, Canvas.GetTop(CropBoundary) + deltaY)));
    //            Canvas.SetLeft(CropBoundary, Math.Max(0, Math.Min(Canvas.GetLeft(CropBoundary) + deltaX, CanvasSize.Width - CropBoundary.Width)));
    //            Canvas.SetTop(CropBoundary, Math.Max(0, Math.Min(Canvas.GetTop(CropBoundary) + deltaY, CanvasSize.Height - CropBoundary.Height)));
    //            break;
    //        case ResizeDirection.TopRight:
    //            CropBoundary.Width = Math.Max(0, Math.Min(CropBoundary.ActualWidth + deltaX, CanvasSize.Width - Canvas.GetLeft(CropBoundary)));
    //            CropBoundary.Height = Math.Max(0, Math.Min(CropBoundary.ActualHeight - deltaY, CanvasSize.Height - Math.Max(0, Canvas.GetTop(CropBoundary) + deltaY)));
    //            Canvas.SetTop(CropBoundary, Math.Max(0, Math.Min(Canvas.GetTop(CropBoundary) + deltaY, CanvasSize.Height - CropBoundary.Height)));
    //            break;
    //        case ResizeDirection.BottomLeft:
    //            CropBoundary.Width = Math.Max(0, Math.Min(CropBoundary.ActualWidth - deltaX, CanvasSize.Width - Math.Max(0, Canvas.GetLeft(CropBoundary) + deltaX)));
    //            CropBoundary.Height = Math.Max(0, Math.Min(CropBoundary.ActualHeight + deltaY, CanvasSize.Height - Canvas.GetTop(CropBoundary)));
    //            Canvas.SetLeft(CropBoundary, Math.Max(0, Math.Min(Canvas.GetLeft(CropBoundary) + deltaX, CanvasSize.Width - CropBoundary.Width)));
    //            break;
    //        case ResizeDirection.BottomRight:
    //            CropBoundary.Width = Math.Max(0, Math.Min(CropBoundary.ActualWidth + deltaX, CanvasSize.Width - Canvas.GetLeft(CropBoundary)));
    //            CropBoundary.Height = Math.Max(0, Math.Min(CropBoundary.ActualHeight + deltaY, CanvasSize.Height - Canvas.GetTop(CropBoundary)));
    //            break;
    //        case ResizeDirection.Left:
    //            CropBoundary.Width = Math.Max(0, Math.Min(CropBoundary.ActualWidth - deltaX, CanvasSize.Width - Math.Max(0, Canvas.GetLeft(CropBoundary) + deltaX)));
    //            Canvas.SetLeft(CropBoundary, Math.Max(0, Math.Min(Canvas.GetLeft(CropBoundary) + deltaX, CanvasSize.Width - CropBoundary.Width)));
    //            break;
    //        case ResizeDirection.Right:
    //            CropBoundary.Width = Math.Max(0, Math.Min(CropBoundary.ActualWidth + deltaX, CanvasSize.Width - Canvas.GetLeft(CropBoundary)));
    //            break;
    //        case ResizeDirection.Top:
    //            CropBoundary.Height = Math.Max(0, Math.Min(CropBoundary.ActualHeight - deltaY, CanvasSize.Height - Math.Max(0, Canvas.GetTop(CropBoundary) + deltaY)));
    //            Canvas.SetTop(CropBoundary, Math.Max(0, Math.Min(Canvas.GetTop(CropBoundary) + deltaY, CanvasSize.Height - CropBoundary.Height)));
    //            break;
    //        case ResizeDirection.Bottom:
    //            CropBoundary.Height = Math.Max(0, Math.Min(CropBoundary.ActualHeight + deltaY, CanvasSize.Height - Canvas.GetTop(CropBoundary)));
    //            break;
    //    }
    //}
    #endregion
}
