using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;
using Rectangle = System.Drawing.Rectangle;

namespace CaptureTool.UI.Windows.Xaml.Controls;

public sealed partial class CropOverlay : UserControlBase
{
    private enum DragMode 
    { 
        None, 
        Move, 
        Resize 
    }

    private static readonly Size MinimumSelectionRectangleSize = new(50, 50);

    public static readonly DependencyProperty AnchorTopLeftProperty = DependencyProperty.Register(
        nameof(AnchorTopLeft),
        typeof(DataTemplate),
        typeof(CropOverlay),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty AnchorTopRightProperty = DependencyProperty.Register(
        nameof(AnchorTopRight),
        typeof(DataTemplate),
        typeof(CropOverlay),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty AnchorBottomLeftProperty = DependencyProperty.Register(
        nameof(AnchorBottomLeft),
        typeof(DataTemplate),
        typeof(CropOverlay),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty AnchorBottomRightProperty = DependencyProperty.Register(
        nameof(AnchorBottomRight),
        typeof(DataTemplate),
        typeof(CropOverlay),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty AnchorTopProperty = DependencyProperty.Register(
        nameof(AnchorTop),
        typeof(DataTemplate),
        typeof(CropOverlay),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty AnchorLeftProperty = DependencyProperty.Register(
        nameof(AnchorLeft),
        typeof(DataTemplate),
        typeof(CropOverlay),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty AnchorRightProperty = DependencyProperty.Register(
        nameof(AnchorRight),
        typeof(DataTemplate),
        typeof(CropOverlay),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty AnchorBottomProperty = DependencyProperty.Register(
        nameof(AnchorBottom),
        typeof(DataTemplate),
        typeof(CropOverlay),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty SelectionAreaProperty = DependencyProperty.Register(
        nameof(SelectionArea),
        typeof(Rectangle),
        typeof(CropOverlay),
        new PropertyMetadata(new Rectangle(), OnSelectionAreaChanged));

    public static readonly DependencyProperty CanResizeProperty = DependencyProperty.Register(
        nameof(CanResize),
        typeof(bool),
        typeof(CropOverlay),
        new PropertyMetadata(true));

    public static readonly DependencyProperty CanMoveProperty = DependencyProperty.Register(
        nameof(CanMove),
        typeof(bool),
        typeof(CropOverlay),
        new PropertyMetadata(true));

    public DataTemplate AnchorTopLeft
    {
        get => (DataTemplate)GetValue(AnchorTopLeftProperty);
        set => SetValue(AnchorTopLeftProperty, value);
    }

    public DataTemplate AnchorTopRight
    {
        get => (DataTemplate)GetValue(AnchorTopRightProperty);
        set => SetValue(AnchorTopRightProperty, value);
    }

    public DataTemplate AnchorBottomLeft
    {
        get => (DataTemplate)GetValue(AnchorBottomLeftProperty);
        set => SetValue(AnchorBottomLeftProperty, value);
    }

    public DataTemplate AnchorBottomRight
    {
        get => (DataTemplate)GetValue(AnchorBottomRightProperty);
        set => SetValue(AnchorBottomRightProperty, value);
    }

    public DataTemplate AnchorTop
    {
        get => (DataTemplate)GetValue(AnchorTopProperty);
        set => SetValue(AnchorTopProperty, value);
    }

    public DataTemplate AnchorLeft
    {
        get => (DataTemplate)GetValue(AnchorLeftProperty);
        set => SetValue(AnchorLeftProperty, value);
    }

    public DataTemplate AnchorRight
    {
        get => (DataTemplate)GetValue(AnchorRightProperty);
        set => SetValue(AnchorRightProperty, value);
    }

    public DataTemplate AnchorBottom
    {
        get => (DataTemplate)GetValue(AnchorBottomProperty);
        set => SetValue(AnchorBottomProperty, value);
    }

    public Rectangle SelectionArea
    {
        get => (Rectangle)GetValue(SelectionAreaProperty);
        set => SetValue(SelectionAreaProperty, value);
    }

    public bool CanResize
    {
        get => (bool)GetValue(CanResizeProperty);
        set => SetValue(CanResizeProperty, value);
    }

    public bool CanMove
    {
        get => (bool)GetValue(CanMoveProperty);
        set => SetValue(CanMoveProperty, value);
    }

    public event EventHandler<Rectangle>? InteractionComplete;
    public event EventHandler<Rectangle>? SelectionAreaChanged;

    private readonly Dictionary<FrameworkElement, Action<double, double>> _anchorDragHandlers = [];
    private readonly Dictionary<FrameworkElement, CoreCursorType> _anchorCursors = [];
    private Point _lastPointerPosition;
    private FrameworkElement? _activeAnchor;
    private DragMode _dragMode = DragMode.None;
    private Rectangle _oldSelectionArea = Rectangle.Empty;

    public CropOverlay()
    {
        InitializeComponent();
        SizeChanged += OnSizeChanged;
        Loaded += (_, _) => AttachEventHandlers();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        double height = e.NewSize.Height;
        double width = e.NewSize.Width;

        RootCanvas.Height = height;
        RootCanvas.Width = width;

        Boundary.Height = RootCanvas.Height;
        Boundary.Width = RootCanvas.Width;

        UpdateBoundary();
    }

    private static void OnSelectionAreaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CropOverlay overlay)
        {
            overlay.UpdateBoundary();
        }
    }

    private void UpdateSelectionArea(Rectangle value)
    {
        SelectionArea = value;
        SelectionAreaChanged?.Invoke(this, SelectionArea);
    }

    private void UpdateBoundary()
    {
        double left = Math.Clamp(SelectionArea.Left, 0, RootCanvas.Width);
        double top = Math.Clamp(SelectionArea.Top, 0, RootCanvas.Height);
        double right = Math.Clamp(SelectionArea.Right, 0, RootCanvas.Width);
        double bottom = Math.Clamp(SelectionArea.Bottom, 0, RootCanvas.Height);
        double centerX = (left + right) / 2;
        double centerY = (top + bottom) / 2;

        Boundary.BorderThickness = new Thickness(left, top, RootCanvas.Width - right, RootCanvas.Height - bottom);

        Canvas.SetLeft(Anchor_TopLeft, left - Anchor_TopLeft.Width / 2);
        Canvas.SetTop(Anchor_TopLeft, top - Anchor_TopLeft.Height / 2);

        Canvas.SetLeft(Anchor_TopRight, right - Anchor_TopRight.Width / 2);
        Canvas.SetTop(Anchor_TopRight, top - Anchor_TopRight.Height / 2);

        Canvas.SetLeft(Anchor_BottomLeft, left - Anchor_BottomLeft.Width / 2);
        Canvas.SetTop(Anchor_BottomLeft, bottom - Anchor_BottomLeft.Height / 2);

        Canvas.SetLeft(Anchor_BottomRight, right - Anchor_BottomRight.Width / 2);
        Canvas.SetTop(Anchor_BottomRight, bottom - Anchor_BottomRight.Height / 2);

        Canvas.SetLeft(Anchor_Top, centerX - Anchor_Top.Width / 2);
        Canvas.SetTop(Anchor_Top, top - Anchor_Top.Height / 2);

        Canvas.SetLeft(Anchor_Bottom, centerX - Anchor_Bottom.Width / 2);
        Canvas.SetTop(Anchor_Bottom, bottom - Anchor_Bottom.Height / 2);

        Canvas.SetLeft(Anchor_Left, left - Anchor_Left.Width / 2);
        Canvas.SetTop(Anchor_Left, centerY - Anchor_Left.Height / 2);

        Canvas.SetLeft(Anchor_Right, right - Anchor_Right.Width / 2);
        Canvas.SetTop(Anchor_Right, centerY - Anchor_Right.Height / 2);
    }

    private void AttachEventHandlers()
    {
        var anchorBoxes = new FrameworkElement[]
        {
            AnchorBox_TopLeft, AnchorBox_TopRight, AnchorBox_BottomLeft, AnchorBox_BottomRight,
            AnchorBox_Top, AnchorBox_Bottom, AnchorBox_Left, AnchorBox_Right,
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
            Anchor_TopLeft, Anchor_TopRight, Anchor_BottomLeft, Anchor_BottomRight,
            Anchor_Top, Anchor_Bottom, Anchor_Left, Anchor_Right
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

        Boundary.PointerPressed += BoundaryPressed;
        Boundary.PointerMoved += BoundaryMoved;
        Boundary.PointerReleased += EndInteraction;
        Boundary.PointerCanceled += EndInteraction;

        InitAnchorHandlers();
    }

    private void InitAnchorHandlers()
    {
        _anchorDragHandlers[Anchor_TopLeft] = (dx, dy) => ResizeFromCorner(dx, dy, true, true);
        _anchorDragHandlers[Anchor_TopRight] = (dx, dy) => ResizeFromCorner(dx, dy, false, true);
        _anchorDragHandlers[Anchor_BottomLeft] = (dx, dy) => ResizeFromCorner(dx, dy, true, false);
        _anchorDragHandlers[Anchor_BottomRight] = (dx, dy) => ResizeFromCorner(dx, dy, false, false);
        _anchorDragHandlers[Anchor_Top] = (_, dy) => ResizeEdge(true, false, dy);
        _anchorDragHandlers[Anchor_Bottom] = (_, dy) => ResizeEdge(true, true, dy);
        _anchorDragHandlers[Anchor_Left] = (dx, _) => ResizeEdge(false, false, dx);
        _anchorDragHandlers[Anchor_Right] = (dx, _) => ResizeEdge(false, true, dx);

        _anchorDragHandlers[AnchorBox_TopLeft] = (dx, dy) => ResizeFromCorner(dx, dy, true, true);
        _anchorDragHandlers[AnchorBox_TopRight] = (dx, dy) => ResizeFromCorner(dx, dy, false, true);
        _anchorDragHandlers[AnchorBox_BottomLeft] = (dx, dy) => ResizeFromCorner(dx, dy, true, false);
        _anchorDragHandlers[AnchorBox_BottomRight] = (dx, dy) => ResizeFromCorner(dx, dy, false, false);
        _anchorDragHandlers[AnchorBox_Top] = (_, dy) => ResizeEdge(true, false, dy);
        _anchorDragHandlers[AnchorBox_Bottom] = (_, dy) => ResizeEdge(true, true, dy);
        _anchorDragHandlers[AnchorBox_Left] = (dx, _) => ResizeEdge(false, false, dx);
        _anchorDragHandlers[AnchorBox_Right] = (dx, _) => ResizeEdge(false, true, dx);

        _anchorCursors[Anchor_TopLeft] = CoreCursorType.SizeNorthwestSoutheast;
        _anchorCursors[Anchor_TopRight] = CoreCursorType.SizeNortheastSouthwest;
        _anchorCursors[Anchor_BottomLeft] = CoreCursorType.SizeNortheastSouthwest;
        _anchorCursors[Anchor_BottomRight] = CoreCursorType.SizeNorthwestSoutheast;
        _anchorCursors[Anchor_Top] = CoreCursorType.SizeNorthSouth;
        _anchorCursors[Anchor_Bottom] = CoreCursorType.SizeNorthSouth;
        _anchorCursors[Anchor_Left] = CoreCursorType.SizeWestEast;
        _anchorCursors[Anchor_Right] = CoreCursorType.SizeWestEast;
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
        if (!CanResize)
        {
            return;
        }

        _activeAnchor = sender as FrameworkElement;
        _lastPointerPosition = e.GetCurrentPoint(RootCanvas).Position;
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
        if (!CanResize || _dragMode != DragMode.Resize || _activeAnchor is null)
        {
            return;
        }

        var pos = e.GetCurrentPoint(RootCanvas).Position;
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
        var pos = e.GetCurrentPoint(RootCanvas).Position;
        if (IsInSelectionArea(pos))
        {
            _dragMode = DragMode.Move;
            _lastPointerPosition = pos;
            Boundary.CapturePointer(e.Pointer);

            StartInteraction();
            e.Handled = true;
        }
    }

    private void BoundaryMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!CanMove || _dragMode != DragMode.Move)
        {
            return;
        }

        var pos = e.GetCurrentPoint(RootCanvas).Position;
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
        _oldSelectionArea = SelectionArea;
    }

    private void EndInteraction(object sender, PointerRoutedEventArgs e)
    {
        if (sender is UIElement element)
            element.ReleasePointerCaptures();

        _dragMode = DragMode.None;
        _activeAnchor = null;
        ProtectedCursor = InputCursor.CreateFromCoreCursor(new CoreCursor(CoreCursorType.Arrow, 0));

        InteractionComplete?.Invoke(this, _oldSelectionArea);

        e.Handled = true;
    }

    private void MoveBy(double dx, double dy)
    {
        var r = SelectionArea;

        double canvasWidth = RootCanvas.ActualWidth;
        double canvasHeight = RootCanvas.ActualHeight;

        if (canvasWidth < 1 || canvasHeight < 1)
        {
            // Canvas not properly sized yet — skip movement to avoid invalid clamp
            return;
        }

        double maxX = Math.Max(0, canvasWidth - r.Width);
        double maxY = Math.Max(0, canvasHeight - r.Height);

        double newX = Math.Clamp(r.Left + dx, 0, maxX);
        double newY = Math.Clamp(r.Top + dy, 0, maxY);

        UpdateSelectionArea(new Rectangle(
            (int)Math.Round(newX),
            (int)Math.Round(newY),
            r.Width,
            r.Height));
    }

    private void ResizeFromCorner(double dx, double dy, bool adjustLeft, bool adjustTop)
    {
        var r = SelectionArea;
        double left = r.Left;
        double top = r.Top;
        double right = r.Right;
        double bottom = r.Bottom;

        if (adjustLeft)
        {
            double maxLeft = right - MinimumSelectionRectangleSize.Width;
            double newLeft = Math.Clamp(left + dx, 0, maxLeft);
            left = newLeft;
        }
        else
        {
            double minRight = left + MinimumSelectionRectangleSize.Width;
            double newRight = Math.Clamp(right + dx, minRight, RootCanvas.ActualWidth);
            right = newRight;
        }

        if (adjustTop)
        {
            double maxTop = bottom - MinimumSelectionRectangleSize.Height;
            double newTop = Math.Clamp(top + dy, 0, maxTop);
            top = newTop;
        }
        else
        {
            double minBottom = top + MinimumSelectionRectangleSize.Height;
            double newBottom = Math.Clamp(bottom + dy, minBottom, RootCanvas.ActualHeight);
            bottom = newBottom;
        }

        UpdateSelectionArea(new Rectangle(
            (int)Math.Round(left),
            (int)Math.Round(top),
            (int)Math.Round(right - left),
            (int)Math.Round(bottom - top)));
    }

    private void ResizeEdge(bool vertical, bool positive, double delta)
    {
        var r = SelectionArea;
        double left = r.Left;
        double top = r.Top;
        double right = r.Right;
        double bottom = r.Bottom;

        if (vertical)
        {
            if (positive)
            {
                double minBottom = top + MinimumSelectionRectangleSize.Height;
                bottom = Math.Clamp(bottom + delta, minBottom, RootCanvas.ActualHeight);
            }
            else
            {
                double maxTop = bottom - MinimumSelectionRectangleSize.Height;
                top = Math.Clamp(top + delta, 0, maxTop);
            }
        }
        else
        {
            if (positive)
            {
                double minRight = left + MinimumSelectionRectangleSize.Width;
                right = Math.Clamp(right + delta, minRight, RootCanvas.ActualWidth);
            }
            else
            {
                double maxLeft = right - MinimumSelectionRectangleSize.Width;
                left = Math.Clamp(left + delta, 0, maxLeft);
            }
        }

        UpdateSelectionArea(new Rectangle(
            (int)Math.Round(left),
            (int)Math.Round(top),
            (int)Math.Round(right - left),
            (int)Math.Round(bottom - top)));
    }

    private bool IsInSelectionArea(Point pos)
    {
        var r = SelectionArea;
        return pos.X >= r.Left && pos.X <= r.Right && pos.Y >= r.Top && pos.Y <= r.Bottom;
    }

    private void Anchor_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is FrameworkElement anchor)
        {
            const int step = 1; // pixels to move per key press
            var r = SelectionArea;
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
