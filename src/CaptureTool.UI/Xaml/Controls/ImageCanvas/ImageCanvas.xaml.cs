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
    private enum CursorContext { None, Anchor, Boundary }
    private CursorContext _currentCursorContext = CursorContext.None;
    private bool _isCropBoundaryDragging = false;
    private Windows.Foundation.Point _cropBoundaryLastPointerPosition;

    private Thickness _cropOffsets = new(0, 0, 0, 0); // left, top, right, bottom

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

        CropBoundary.PointerPressed += CropBoundary_PointerPressed;
        CropBoundary.PointerMoved += CropBoundary_PointerMoved;
        CropBoundary.PointerReleased += CropBoundary_PointerReleased;
        CropBoundary.PointerCanceled += CropBoundary_PointerCanceled;

        CropBoundary.PointerEntered += CropBoundary_PointerEntered;
        CropBoundary.PointerExited += CropBoundary_PointerExited;
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

            double canvasWidth = CropCanvas.Width;
            double canvasHeight = CropCanvas.Height;

            // Copy current offsets
            double left = _cropOffsets.Left;
            double top = _cropOffsets.Top;
            double right = _cropOffsets.Right;
            double bottom = _cropOffsets.Bottom;

            // Update offsets based on which anchor is being dragged
            if (_activeCropAnchor == CropAnchor_TopLeft)
            {
                left = Math.Clamp(left + deltaX, 0, canvasWidth - right - 1);
                top = Math.Clamp(top + deltaY, 0, canvasHeight - bottom - 1);
            }
            else if (_activeCropAnchor == CropAnchor_TopRight)
            {
                right = Math.Clamp(right - deltaX, 0, canvasWidth - left - 1);
                top = Math.Clamp(top + deltaY, 0, canvasHeight - bottom - 1);
            }
            else if (_activeCropAnchor == CropAnchor_BottomLeft)
            {
                left = Math.Clamp(left + deltaX, 0, canvasWidth - right - 1);
                bottom = Math.Clamp(bottom - deltaY, 0, canvasHeight - top - 1);
            }
            else if (_activeCropAnchor == CropAnchor_BottomRight)
            {
                right = Math.Clamp(right - deltaX, 0, canvasWidth - left - 1);
                bottom = Math.Clamp(bottom - deltaY, 0, canvasHeight - top - 1);
            }
            else if (_activeCropAnchor == CropAnchor_Top)
            {
                top = Math.Clamp(top + deltaY, 0, canvasHeight - bottom - 1);
            }
            else if (_activeCropAnchor == CropAnchor_Bottom)
            {
                bottom = Math.Clamp(bottom - deltaY, 0, canvasHeight - top - 1);
            }
            else if (_activeCropAnchor == CropAnchor_Left)
            {
                left = Math.Clamp(left + deltaX, 0, canvasWidth - right - 1);
            }
            else if (_activeCropAnchor == CropAnchor_Right)
            {
                right = Math.Clamp(right - deltaX, 0, canvasWidth - left - 1);
            }

            // Update offsets and apply to CropBoundary
            _cropOffsets = new Thickness(left, top, right, bottom);
            CropBoundary.BorderThickness = _cropOffsets;

            _cropAnchorLastPointerPosition = currentPosition;
            e.Handled = true;

            // Optionally: Update anchor visuals here if needed
            UpdateCropAnchorPositions();
        }
    }

    private void UpdateCropAnchorPositions()
    {
        double left = _cropOffsets.Left;
        double top = _cropOffsets.Top;
        double right = CropCanvas.Width - _cropOffsets.Right;
        double bottom = CropCanvas.Height - _cropOffsets.Bottom;
        double centerX = (left + right) / 2;
        double centerY = (top + bottom) / 2;

        Canvas.SetLeft(CropAnchor_TopLeft, left - CropAnchor_TopLeft.Width / 2);
        Canvas.SetTop(CropAnchor_TopLeft, top - CropAnchor_TopLeft.Height / 2);

        Canvas.SetLeft(CropAnchor_TopRight, right - CropAnchor_TopRight.Width / 2);
        Canvas.SetTop(CropAnchor_TopRight, top - CropAnchor_TopRight.Height / 2);

        Canvas.SetLeft(CropAnchor_BottomLeft, left - CropAnchor_BottomLeft.Width / 2);
        Canvas.SetTop(CropAnchor_BottomLeft, bottom - CropAnchor_BottomLeft.Height / 2);

        Canvas.SetLeft(CropAnchor_BottomRight, right - CropAnchor_BottomRight.Width / 2);
        Canvas.SetTop(CropAnchor_BottomRight, bottom - CropAnchor_BottomRight.Height / 2);

        Canvas.SetLeft(CropAnchor_Top, centerX - CropAnchor_Top.Width / 2);
        Canvas.SetTop(CropAnchor_Top, top - CropAnchor_Top.Height / 2);

        Canvas.SetLeft(CropAnchor_Bottom, centerX - CropAnchor_Bottom.Width / 2);
        Canvas.SetTop(CropAnchor_Bottom, bottom - CropAnchor_Bottom.Height / 2);

        Canvas.SetLeft(CropAnchor_Left, left - CropAnchor_Left.Width / 2);
        Canvas.SetTop(CropAnchor_Left, centerY - CropAnchor_Left.Height / 2);

        Canvas.SetLeft(CropAnchor_Right, right - CropAnchor_Right.Width / 2);
        Canvas.SetTop(CropAnchor_Right, centerY - CropAnchor_Right.Height / 2);
    }

    private void CropAnchor_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_activeCropAnchor != null)
        {
            _activeCropAnchor.ReleasePointerCaptures();
            _activeCropAnchor = null;
            _currentCursorContext = CursorContext.None;
            ProtectedCursor = InputCursor.CreateFromCoreCursor(new(CoreCursorType.Arrow, 0));
            e.Handled = true;
        }
    }

    private void CropAnchor_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (_activeCropAnchor != null)
        {
            _activeCropAnchor.ReleasePointerCaptures();
            _activeCropAnchor = null;
            _currentCursorContext = CursorContext.None;
            ProtectedCursor = InputCursor.CreateFromCoreCursor(new(CoreCursorType.Arrow, 0));
            e.Handled = true;
        }
    }

    private void CropAnchor_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Polygon anchor)
        {
            _currentCursorContext = CursorContext.Anchor;
            ProtectedCursor = GetCursorForAnchor(anchor);
        }
    }

    private void CropAnchor_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (_activeCropAnchor == null)
        {
            _currentCursorContext = CursorContext.None;
            // If pointer is also over boundary, set boundary cursor
            var pointerPos = e.GetCurrentPoint(CropCanvas).Position;
            if (IsPointerOverBoundary(pointerPos))
            {
                _currentCursorContext = CursorContext.Boundary;
                ProtectedCursor = InputCursor.CreateFromCoreCursor(new(CoreCursorType.SizeAll, 0));
            }
            else
            {
                ProtectedCursor = InputCursor.CreateFromCoreCursor(new(CoreCursorType.Arrow, 0));
            }
        }
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
        return InputCursor.CreateFromCoreCursor(new(CoreCursorType.Arrow, 0));
    }
    private void CropBoundary_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_activeCropAnchor == null)
        {
            var pointerPos = e.GetCurrentPoint(CropCanvas).Position;
            if (IsPointerOverCropArea(pointerPos) && !IsPointerOverAnyAnchor(pointerPos))
            {
                _isCropBoundaryDragging = true;
                _cropBoundaryLastPointerPosition = pointerPos;
                CropBoundary.CapturePointer(e.Pointer);
                e.Handled = true;
            }
        }
    }

    private void CropBoundary_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var pointerPos = e.GetCurrentPoint(CropCanvas).Position;
        if (_isCropBoundaryDragging && e.Pointer.IsInContact)
        {
            double deltaX = pointerPos.X - _cropBoundaryLastPointerPosition.X;
            double deltaY = pointerPos.Y - _cropBoundaryLastPointerPosition.Y;

            double canvasWidth = CropCanvas.Width;
            double canvasHeight = CropCanvas.Height;

            double cropWidth = canvasWidth - _cropOffsets.Left - _cropOffsets.Right;
            double cropHeight = canvasHeight - _cropOffsets.Top - _cropOffsets.Bottom;

            double newLeft = _cropOffsets.Left + deltaX;
            double newTop = _cropOffsets.Top + deltaY;

            newLeft = Math.Clamp(newLeft, 0, canvasWidth - cropWidth);
            newTop = Math.Clamp(newTop, 0, canvasHeight - cropHeight);

            double newRight = canvasWidth - cropWidth - newLeft;
            double newBottom = canvasHeight - cropHeight - newTop;

            _cropOffsets = new Thickness(newLeft, newTop, newRight, newBottom);
            CropBoundary.BorderThickness = _cropOffsets;

            _cropBoundaryLastPointerPosition = pointerPos;
            e.Handled = true;

            UpdateCropAnchorPositions();
        }
        else if (_currentCursorContext != CursorContext.Anchor)
        {
            // Update cursor on hover
            if (IsPointerOverCropArea(pointerPos) && !IsPointerOverAnyAnchor(pointerPos))
            {
                if (_currentCursorContext != CursorContext.Boundary)
                {
                    _currentCursorContext = CursorContext.Boundary;
                    ProtectedCursor = InputCursor.CreateFromCoreCursor(new(CoreCursorType.SizeAll, 0));
                }
            }
            else
            {
                if (_currentCursorContext != CursorContext.None)
                {
                    _currentCursorContext = CursorContext.None;
                    ProtectedCursor = InputCursor.CreateFromCoreCursor(new(CoreCursorType.Arrow, 0));
                }
            }
        }
    }

    private void CropBoundary_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isCropBoundaryDragging)
        {
            _isCropBoundaryDragging = false;
            CropBoundary.ReleasePointerCaptures();

            var pointerPos = e.GetCurrentPoint(CropCanvas).Position;
            if (IsPointerOverAnyAnchor(pointerPos))
            {
                // Let anchor logic handle cursor
            }
            else if (IsPointerOverCropArea(pointerPos))
            {
                _currentCursorContext = CursorContext.Boundary;
                ProtectedCursor = InputCursor.CreateFromCoreCursor(new(CoreCursorType.SizeAll, 0));
            }
            else
            {
                _currentCursorContext = CursorContext.None;
                ProtectedCursor = InputCursor.CreateFromCoreCursor(new(CoreCursorType.Arrow, 0));
            }

            e.Handled = true;
        }
    }

    private void CropBoundary_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (_isCropBoundaryDragging)
        {
            _isCropBoundaryDragging = false;
            CropBoundary.ReleasePointerCaptures();
            _currentCursorContext = CursorContext.None;
            ProtectedCursor = InputCursor.CreateFromCoreCursor(new(CoreCursorType.Arrow, 0));
            e.Handled = true;
        }
    }

    private void CropBoundary_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (_currentCursorContext != CursorContext.Anchor)
        {
            var pointerPos = e.GetCurrentPoint(CropCanvas).Position;
            if (IsPointerOverCropArea(pointerPos) && !IsPointerOverAnyAnchor(pointerPos))
            {
                _currentCursorContext = CursorContext.Boundary;
                ProtectedCursor = InputCursor.CreateFromCoreCursor(new(CoreCursorType.SizeAll, 0));
            }
            else
            {
                _currentCursorContext = CursorContext.None;
                ProtectedCursor = InputCursor.CreateFromCoreCursor(new(CoreCursorType.Arrow, 0));
            }
        }
    }

    private void CropBoundary_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (_currentCursorContext == CursorContext.Boundary)
        {
            _currentCursorContext = CursorContext.None;
            ProtectedCursor = InputCursor.CreateFromCoreCursor(new(CoreCursorType.Arrow, 0));
        }
    }

    private bool IsPointerOverBoundary(Windows.Foundation.Point pointerPos)
    {
        // You may want to check if pointer is inside the crop area but not over any anchor
        // For most cases, just return true if inside CropBoundary
        // If you want to be more precise, check anchor bounds and exclude them
        return true;
    }

    private bool IsPointerOverCropContent(Windows.Foundation.Point pos)
    {
        double left = _cropOffsets.Left;
        double top = _cropOffsets.Top;
        double right = CropCanvas.Width - _cropOffsets.Right;
        double bottom = CropCanvas.Height - _cropOffsets.Bottom;

        // Define a border thickness threshold (in pixels) for the border area
        double borderThickness = Math.Max(
            Math.Max(CropBoundary.BorderThickness.Left, CropBoundary.BorderThickness.Right),
            Math.Max(CropBoundary.BorderThickness.Top, CropBoundary.BorderThickness.Bottom)
        );
        // You may want to use a fixed value if your border is always 1px, e.g. double borderThickness = 1;

        // The content area is strictly inside the crop area, minus the border thickness
        double contentLeft = left + borderThickness;
        double contentTop = top + borderThickness;
        double contentRight = right - borderThickness;
        double contentBottom = bottom - borderThickness;

        return pos.X > contentLeft && pos.X < contentRight && pos.Y > contentTop && pos.Y < contentBottom;
    }

    private bool IsPointerOverCropArea(Windows.Foundation.Point pos)
    {
        double left = _cropOffsets.Left;
        double top = _cropOffsets.Top;
        double right = CropCanvas.Width - _cropOffsets.Right;
        double bottom = CropCanvas.Height - _cropOffsets.Bottom;

        // Check if inside the crop area rectangle
        return pos.X > left && pos.X < right && pos.Y > top && pos.Y < bottom;
    }

    private bool IsPointerOverAnyAnchor(Windows.Foundation.Point pos)
    {
        foreach (var anchor in GetCropAnchors())
        {
            var anchorLeft = Canvas.GetLeft(anchor);
            var anchorTop = Canvas.GetTop(anchor);
            var anchorRight = anchorLeft + anchor.Width;
            var anchorBottom = anchorTop + anchor.Height;
            if (pos.X >= anchorLeft && pos.X <= anchorRight &&
                pos.Y >= anchorTop && pos.Y <= anchorBottom)
            {
                return true;
            }
        }
        return false;
    }
}
