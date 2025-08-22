using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;
using Rectangle = System.Drawing.Rectangle;

namespace CaptureTool.UI.Windows.Xaml.Controls;

public sealed partial class CropOverlay : UserControlBase
{
    private enum DragMode { None, Move, Resize }

    private static readonly Size MinCropRectSize = new(50, 50);

    private readonly Dictionary<FrameworkElement, Action<double, double>> _anchorDragHandlers = [];

    private readonly Dictionary<FrameworkElement, CoreCursorType> _anchorCursors = [];
    private Point _lastPointerPosition;
    private FrameworkElement? _activeAnchor;
    private DragMode _dragMode = DragMode.None;
    private Rectangle _oldCropRect = Rectangle.Empty;

    public static readonly DependencyProperty CropRectProperty = DependencyProperty.Register(
        nameof(CropRect),
        typeof(Rectangle),
        typeof(CropOverlay),
        new PropertyMetadata(new Rectangle(), OnCropRectChanged));

    public Rectangle CropRect
    {
        get => (Rectangle)GetValue(CropRectProperty);
        set => SetValue(CropRectProperty, value);
    }

    public event EventHandler<Rectangle>? InteractionComplete;

    public CropOverlay()
    {
        InitializeComponent();
        SizeChanged += CropOverlay_SizeChanged;
        Loaded += (_, _) => AttachEventHandlers();
    }

    private void CropOverlay_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        double height = e.NewSize.Height;
        double width = e.NewSize.Width;

        CropCanvas.Height = height;
        CropCanvas.Width = width;

        CropBoundary.Height = CropCanvas.Height;
        CropBoundary.Width = CropCanvas.Width;

        UpdateCropBoundary();
    }

    private static void OnCropRectChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CropOverlay overlay)
        {
            overlay.UpdateCropBoundary();
        }
    }

    private void UpdateCropBoundary()
    {
        double left = Math.Clamp(CropRect.Left, 0, CropCanvas.Width);
        double top = Math.Clamp(CropRect.Top, 0, CropCanvas.Height);
        double right = Math.Clamp(CropRect.Right, 0, CropCanvas.Width);
        double bottom = Math.Clamp(CropRect.Bottom, 0, CropCanvas.Height);
        double centerX = (left + right) / 2;
        double centerY = (top + bottom) / 2;

        CropBoundary.BorderThickness = new Thickness(left, top, CropCanvas.Width - right, CropCanvas.Height - bottom);

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

    private void AttachEventHandlers()
    {
        var anchorBoxes = new FrameworkElement[]
        {
            CropAnchorBox_TopLeft, CropAnchorBox_TopRight, CropAnchorBox_BottomLeft, CropAnchorBox_BottomRight,
            CropAnchorBox_Top, CropAnchorBox_Bottom, CropAnchorBox_Left, CropAnchorBox_Right,
        };

        foreach (var anchorBox in anchorBoxes)
        {
            anchorBox.PointerPressed += AnchorBox_PointerPressed;
            anchorBox.PointerMoved += AnchorBox_AnchorMoved;
            anchorBox.PointerReleased += AnchorBox_EndInteraction;
            anchorBox.PointerCanceled += AnchorBox_EndInteraction;
        }

        var anchors = new FrameworkElement[]
        {
            CropAnchor_TopLeft, CropAnchor_TopRight, CropAnchor_BottomLeft, CropAnchor_BottomRight,
            CropAnchor_Top, CropAnchor_Bottom, CropAnchor_Left, CropAnchor_Right
        };

        foreach (var anchor in anchors)
        {
            anchor.PointerPressed += AnchorPressed;
            anchor.PointerMoved += AnchorMoved;
            anchor.PointerReleased += EndInteraction;
            anchor.PointerCanceled += EndInteraction;
            anchor.PointerEntered += AnchorPointerEntered;
            anchor.PointerExited += AnchorPointerExited;
            anchor.KeyDown += Anchor_KeyDown;
        }

        CropBoundary.PointerPressed += BoundaryPressed;
        CropBoundary.PointerMoved += BoundaryMoved;
        CropBoundary.PointerReleased += EndInteraction;
        CropBoundary.PointerCanceled += EndInteraction;

        InitAnchorHandlers();
    }

    private void InitAnchorHandlers()
    {
        _anchorDragHandlers[CropAnchor_TopLeft] = (dx, dy) => ResizeFromCorner(dx, dy, true, true);
        _anchorDragHandlers[CropAnchor_TopRight] = (dx, dy) => ResizeFromCorner(dx, dy, false, true);
        _anchorDragHandlers[CropAnchor_BottomLeft] = (dx, dy) => ResizeFromCorner(dx, dy, true, false);
        _anchorDragHandlers[CropAnchor_BottomRight] = (dx, dy) => ResizeFromCorner(dx, dy, false, false);
        _anchorDragHandlers[CropAnchor_Top] = (_, dy) => ResizeEdge(true, false, dy);
        _anchorDragHandlers[CropAnchor_Bottom] = (_, dy) => ResizeEdge(true, true, dy);
        _anchorDragHandlers[CropAnchor_Left] = (dx, _) => ResizeEdge(false, false, dx);
        _anchorDragHandlers[CropAnchor_Right] = (dx, _) => ResizeEdge(false, true, dx);

        _anchorDragHandlers[CropAnchorBox_TopLeft] = (dx, dy) => ResizeFromCorner(dx, dy, true, true);
        _anchorDragHandlers[CropAnchorBox_TopRight] = (dx, dy) => ResizeFromCorner(dx, dy, false, true);
        _anchorDragHandlers[CropAnchorBox_BottomLeft] = (dx, dy) => ResizeFromCorner(dx, dy, true, false);
        _anchorDragHandlers[CropAnchorBox_BottomRight] = (dx, dy) => ResizeFromCorner(dx, dy, false, false);
        _anchorDragHandlers[CropAnchorBox_Top] = (_, dy) => ResizeEdge(true, false, dy);
        _anchorDragHandlers[CropAnchorBox_Bottom] = (_, dy) => ResizeEdge(true, true, dy);
        _anchorDragHandlers[CropAnchorBox_Left] = (dx, _) => ResizeEdge(false, false, dx);
        _anchorDragHandlers[CropAnchorBox_Right] = (dx, _) => ResizeEdge(false, true, dx);

        _anchorCursors[CropAnchor_TopLeft] = CoreCursorType.SizeNorthwestSoutheast;
        _anchorCursors[CropAnchor_TopRight] = CoreCursorType.SizeNortheastSouthwest;
        _anchorCursors[CropAnchor_BottomLeft] = CoreCursorType.SizeNortheastSouthwest;
        _anchorCursors[CropAnchor_BottomRight] = CoreCursorType.SizeNorthwestSoutheast;
        _anchorCursors[CropAnchor_Top] = CoreCursorType.SizeNorthSouth;
        _anchorCursors[CropAnchor_Bottom] = CoreCursorType.SizeNorthSouth;
        _anchorCursors[CropAnchor_Left] = CoreCursorType.SizeWestEast;
        _anchorCursors[CropAnchor_Right] = CoreCursorType.SizeWestEast;
    }

    private void AnchorBox_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType == PointerDeviceType.Touch)
        {
            AnchorPressed(sender, e);
        }
    }

    private void AnchorPressed(object sender, PointerRoutedEventArgs e)
    {
        _activeAnchor = sender as FrameworkElement;
        _lastPointerPosition = e.GetCurrentPoint(CropCanvas).Position;
        _dragMode = DragMode.Resize;
        ((UIElement)sender).CapturePointer(e.Pointer);
        e.Handled = true;

        StartInteraction();
    }

    private void AnchorBox_AnchorMoved(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType == PointerDeviceType.Touch)
        {
            AnchorMoved(sender, e);
        }
    }

    private void AnchorMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragMode != DragMode.Resize || _activeAnchor is null) return;

        var pos = e.GetCurrentPoint(CropCanvas).Position;
        var dx = pos.X - _lastPointerPosition.X;
        var dy = pos.Y - _lastPointerPosition.Y;

        if (_anchorDragHandlers.TryGetValue(_activeAnchor, out var handler))
            handler(dx, dy);

        _lastPointerPosition = pos;
        e.Handled = true;
    }

    private void AnchorPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement anchor && _anchorCursors.TryGetValue(anchor, out var cursorType))
        {
            ProtectedCursor = InputCursor.CreateFromCoreCursor(new CoreCursor(cursorType, 0));
        }
    }

    private void AnchorPointerExited(object sender, PointerRoutedEventArgs e)
    {
        ProtectedCursor = InputCursor.CreateFromCoreCursor(new CoreCursor(CoreCursorType.Arrow, 0));
    }

    private void BoundaryPressed(object sender, PointerRoutedEventArgs e)
    {
        var pos = e.GetCurrentPoint(CropCanvas).Position;
        if (IsInCropArea(pos))
        {
            _dragMode = DragMode.Move;
            _lastPointerPosition = pos;
            CropBoundary.CapturePointer(e.Pointer);

            StartInteraction();
            e.Handled = true;
        }
    }

    private void BoundaryMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragMode != DragMode.Move) return;

        var pos = e.GetCurrentPoint(CropCanvas).Position;
        var dx = pos.X - _lastPointerPosition.X;
        var dy = pos.Y - _lastPointerPosition.Y;

        MoveBy(dx, dy);
        _lastPointerPosition = pos;
        e.Handled = true;
    }

    private void AnchorBox_EndInteraction(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType == PointerDeviceType.Touch)
        {
            EndInteraction(sender, e);
        }
    }

    private void StartInteraction()
    {
        _oldCropRect = CropRect;
    }

    private void EndInteraction(object sender, PointerRoutedEventArgs e)
    {
        if (sender is UIElement element)
            element.ReleasePointerCaptures();

        _dragMode = DragMode.None;
        _activeAnchor = null;
        ProtectedCursor = InputCursor.CreateFromCoreCursor(new CoreCursor(CoreCursorType.Arrow, 0));

        InteractionComplete?.Invoke(this, _oldCropRect);

        e.Handled = true;
    }

    private void MoveBy(double dx, double dy)
    {
        var r = CropRect;

        double canvasWidth = CropCanvas.ActualWidth;
        double canvasHeight = CropCanvas.ActualHeight;

        if (canvasWidth < 1 || canvasHeight < 1)
        {
            // Canvas not properly sized yet — skip movement to avoid invalid clamp
            return;
        }

        double maxX = Math.Max(0, canvasWidth - r.Width);
        double maxY = Math.Max(0, canvasHeight - r.Height);

        double newX = Math.Clamp(r.Left + dx, 0, maxX);
        double newY = Math.Clamp(r.Top + dy, 0, maxY);

        CropRect = new Rectangle(
            (int)Math.Round(newX),
            (int)Math.Round(newY),
            r.Width,
            r.Height);
    }

    private void ResizeFromCorner(double dx, double dy, bool adjustLeft, bool adjustTop)
    {
        var r = CropRect;
        double left = r.Left;
        double top = r.Top;
        double right = r.Right;
        double bottom = r.Bottom;

        if (adjustLeft)
        {
            double maxLeft = right - MinCropRectSize.Width;
            double newLeft = Math.Clamp(left + dx, 0, maxLeft);
            left = newLeft;
        }
        else
        {
            double minRight = left + MinCropRectSize.Width;
            double newRight = Math.Clamp(right + dx, minRight, CropCanvas.ActualWidth);
            right = newRight;
        }

        if (adjustTop)
        {
            double maxTop = bottom - MinCropRectSize.Height;
            double newTop = Math.Clamp(top + dy, 0, maxTop);
            top = newTop;
        }
        else
        {
            double minBottom = top + MinCropRectSize.Height;
            double newBottom = Math.Clamp(bottom + dy, minBottom, CropCanvas.ActualHeight);
            bottom = newBottom;
        }

        CropRect = new Rectangle(
            (int)Math.Round(left),
            (int)Math.Round(top),
            (int)Math.Round(right - left),
            (int)Math.Round(bottom - top));
    }

    private void ResizeEdge(bool vertical, bool positive, double delta)
    {
        var r = CropRect;
        double left = r.Left;
        double top = r.Top;
        double right = r.Right;
        double bottom = r.Bottom;

        if (vertical)
        {
            if (positive)
            {
                double minBottom = top + MinCropRectSize.Height;
                bottom = Math.Clamp(bottom + delta, minBottom, CropCanvas.ActualHeight);
            }
            else
            {
                double maxTop = bottom - MinCropRectSize.Height;
                top = Math.Clamp(top + delta, 0, maxTop);
            }
        }
        else
        {
            if (positive)
            {
                double minRight = left + MinCropRectSize.Width;
                right = Math.Clamp(right + delta, minRight, CropCanvas.ActualWidth);
            }
            else
            {
                double maxLeft = right - MinCropRectSize.Width;
                left = Math.Clamp(left + delta, 0, maxLeft);
            }
        }

        CropRect = new Rectangle(
            (int)Math.Round(left),
            (int)Math.Round(top),
            (int)Math.Round(right - left),
            (int)Math.Round(bottom - top));
    }

    private bool IsInCropArea(Point pos)
    {
        var r = CropRect;
        return pos.X >= r.Left && pos.X <= r.Right && pos.Y >= r.Top && pos.Y <= r.Bottom;
    }

    private void Anchor_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is FrameworkElement anchor)
        {
            const int step = 1; // pixels to move per key press
            var r = CropRect;
            double left = r.Left;
            double top = r.Top;
            double right = r.Right;
            double bottom = r.Bottom;

            switch (e.Key)
            {
                case VirtualKey.Left:
                    if (_anchorDragHandlers.TryGetValue(anchor, out var resizeHandlerLeft))
                        resizeHandlerLeft(-step, 0);
                    e.Handled = true;
                    break;

                case VirtualKey.Right:
                    if (_anchorDragHandlers.TryGetValue(anchor, out var resizeHandlerRight))
                        resizeHandlerRight(step, 0);
                    e.Handled = true;
                    break;

                case VirtualKey.Up:
                    if (_anchorDragHandlers.TryGetValue(anchor, out var resizeHandlerUp))
                        resizeHandlerUp(0, -step);
                    e.Handled = true;
                    break;

                case VirtualKey.Down:
                    if (_anchorDragHandlers.TryGetValue(anchor, out var resizeHandlerDown))
                        resizeHandlerDown(0, step);
                    e.Handled = true;
                    break;
            }
        }
    }

}
