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
using Microsoft.UI.Xaml.Shapes;
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
        new PropertyMetadata(new Size(0, 0), OnCanvasSizePropertyChanged));

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
        AttachCropAnchorEvents();
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

    private Polygon[] GetCropAnchors()
    {
        return [
            CropAnchor_TopLeft,
            CropAnchor_TopRight,
            CropAnchor_BottomLeft,
            CropAnchor_BottomRight,
            CropAnchor_Top,
            CropAnchor_Bottom,
            CropAnchor_Left,
            CropAnchor_Right
        ];
    }

    // Add pointer events for crop anchors so that they can be moved within the bounds of the crop canvas.
    // Update the cursor to the appropriate type when hovering over crop anchors.
    // Add pointer events for crop anchors so that they can be moved within the bounds of the crop canvas.
    // Update the cursor to the appropriate type when hovering over crop anchors.

    private Polygon? _activeCropAnchor = null;
    private Windows.Foundation.Point _cropAnchorLastPointerPosition;

    private void AttachCropAnchorEvents()
    {
        foreach (var anchor in GetCropAnchors())
        {
            anchor.PointerPressed += CropAnchor_PointerPressed;
            anchor.PointerMoved += CropAnchor_PointerMoved;
            anchor.PointerReleased += CropAnchor_PointerReleased;
            anchor.PointerCanceled += CropAnchor_PointerCanceled;
            anchor.PointerEntered += CropAnchor_PointerEntered;
            anchor.PointerExited += CropAnchor_PointerExited;
        }
    }

    private void CropAnchor_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Polygon anchor)
        {
            _activeCropAnchor = anchor;
            _cropAnchorLastPointerPosition = e.GetCurrentPoint(CropCanvas).Position;
            anchor.CapturePointer(e.Pointer);
            e.Handled = true;
        }
    }

    private void CropAnchor_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_activeCropAnchor != null && e.Pointer.IsInContact)
        {
            var currentPosition = e.GetCurrentPoint(CropCanvas).Position;
            double deltaX = currentPosition.X - _cropAnchorLastPointerPosition.X;
            double deltaY = currentPosition.Y - _cropAnchorLastPointerPosition.Y;

            // Get current CropBoundary position and size
            double boundaryLeft = Canvas.GetLeft(CropBoundary);
            double boundaryTop = Canvas.GetTop(CropBoundary);
            double boundaryWidth = CropBoundary.Width;
            double boundaryHeight = CropBoundary.Height;

            // Determine which anchor is being dragged and adjust CropBoundary accordingly
            if (_activeCropAnchor == CropAnchor_TopLeft)
            {
                double newLeft = Math.Max(0, boundaryLeft + deltaX);
                double newTop = Math.Max(0, boundaryTop + deltaY);
                double newWidth = Math.Max(1, boundaryWidth - (newLeft - boundaryLeft));
                double newHeight = Math.Max(1, boundaryHeight - (newTop - boundaryTop));
                if (newLeft + newWidth > CropCanvas.Width) newWidth = CropCanvas.Width - newLeft;
                if (newTop + newHeight > CropCanvas.Height) newHeight = CropCanvas.Height - newTop;
                Canvas.SetLeft(CropBoundary, newLeft);
                Canvas.SetTop(CropBoundary, newTop);
                CropBoundary.Width = newWidth;
                CropBoundary.Height = newHeight;
            }
            else if (_activeCropAnchor == CropAnchor_TopRight)
            {
                double newTop = Math.Max(0, boundaryTop + deltaY);
                double newWidth = Math.Max(1, boundaryWidth + deltaX);
                double newHeight = Math.Max(1, boundaryHeight - (newTop - boundaryTop));
                if (boundaryLeft + newWidth > CropCanvas.Width) newWidth = CropCanvas.Width - boundaryLeft;
                if (newTop + newHeight > CropCanvas.Height) newHeight = CropCanvas.Height - newTop;
                Canvas.SetTop(CropBoundary, newTop);
                CropBoundary.Width = newWidth;
                CropBoundary.Height = newHeight;
            }
            else if (_activeCropAnchor == CropAnchor_BottomLeft)
            {
                double newLeft = Math.Max(0, boundaryLeft + deltaX);
                double newWidth = Math.Max(1, boundaryWidth - (newLeft - boundaryLeft));
                double newHeight = Math.Max(1, boundaryHeight + deltaY);
                if (newLeft + newWidth > CropCanvas.Width) newWidth = CropCanvas.Width - newLeft;
                if (boundaryTop + newHeight > CropCanvas.Height) newHeight = CropCanvas.Height - boundaryTop;
                Canvas.SetLeft(CropBoundary, newLeft);
                CropBoundary.Width = newWidth;
                CropBoundary.Height = newHeight;
            }
            else if (_activeCropAnchor == CropAnchor_BottomRight)
            {
                double newWidth = Math.Max(1, boundaryWidth + deltaX);
                double newHeight = Math.Max(1, boundaryHeight + deltaY);
                if (boundaryLeft + newWidth > CropCanvas.Width) newWidth = CropCanvas.Width - boundaryLeft;
                if (boundaryTop + newHeight > CropCanvas.Height) newHeight = CropCanvas.Height - boundaryTop;
                CropBoundary.Width = newWidth;
                CropBoundary.Height = newHeight;
            }
            else if (_activeCropAnchor == CropAnchor_Top)
            {
                double newTop = Math.Max(0, boundaryTop + deltaY);
                double newHeight = Math.Max(1, boundaryHeight - (newTop - boundaryTop));
                if (newTop + newHeight > CropCanvas.Height) newHeight = CropCanvas.Height - newTop;
                Canvas.SetTop(CropBoundary, newTop);
                CropBoundary.Height = newHeight;
            }
            else if (_activeCropAnchor == CropAnchor_Bottom)
            {
                double newHeight = Math.Max(1, boundaryHeight + deltaY);
                if (boundaryTop + newHeight > CropCanvas.Height) newHeight = CropCanvas.Height - boundaryTop;
                CropBoundary.Height = newHeight;
            }
            else if (_activeCropAnchor == CropAnchor_Left)
            {
                double newLeft = Math.Max(0, boundaryLeft + deltaX);
                double newWidth = Math.Max(1, boundaryWidth - (newLeft - boundaryLeft));
                if (newLeft + newWidth > CropCanvas.Width) newWidth = CropCanvas.Width - newLeft;
                Canvas.SetLeft(CropBoundary, newLeft);
                CropBoundary.Width = newWidth;
            }
            else if (_activeCropAnchor == CropAnchor_Right)
            {
                double newWidth = Math.Max(1, boundaryWidth + deltaX);
                if (boundaryLeft + newWidth > CropCanvas.Width) newWidth = CropCanvas.Width - boundaryLeft;
                CropBoundary.Width = newWidth;
            }

            _cropAnchorLastPointerPosition = currentPosition;
            e.Handled = true;
        }
    }

    private void CropAnchor_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_activeCropAnchor != null)
        {
            _activeCropAnchor.ReleasePointerCaptures();
            _activeCropAnchor = null;
            e.Handled = true;
        }
    }

    private void CropAnchor_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (_activeCropAnchor != null)
        {
            _activeCropAnchor.ReleasePointerCaptures();
            _activeCropAnchor = null;
            e.Handled = true;
        }
    }

    private void CropAnchor_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Polygon anchor)
        {
            ProtectedCursor = GetCursorForAnchor(anchor);
        }
    }

    private void CropAnchor_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        ProtectedCursor = InputCursor.CreateFromCoreCursor(new(CoreCursorType.Arrow, 0));
    }

    private InputCursor GetCursorForAnchor(Polygon anchor)
    {
        // Set cursor type based on anchor position
        if (anchor == CropAnchor_TopLeft || anchor == CropAnchor_BottomRight)
            return InputCursor.CreateFromCoreCursor(new(CoreCursorType.SizeNorthwestSoutheast, 0));
        if (anchor == CropAnchor_TopRight || anchor == CropAnchor_BottomLeft)
            return InputCursor.CreateFromCoreCursor(new(CoreCursorType.SizeNortheastSouthwest, 0));
        if (anchor == CropAnchor_Top || anchor == CropAnchor_Bottom)
            return InputCursor.CreateFromCoreCursor(new(CoreCursorType.SizeNorthSouth, 0));
        if (anchor == CropAnchor_Left || anchor == CropAnchor_Right)
            return InputCursor.CreateFromCoreCursor(new(CoreCursorType.SizeWestEast, 0));
        return InputCursor.CreateFromCoreCursor(new(CoreCursorType.SizeAll, 0));
    }
}
