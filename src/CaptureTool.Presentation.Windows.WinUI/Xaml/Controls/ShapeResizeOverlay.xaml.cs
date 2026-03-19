using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Drawing;
using Point = Windows.Foundation.Point;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class ShapeResizeOverlay : UserControlBase
{
    public static readonly DependencyProperty ShapeBoundsProperty = DependencyProperty.Register(
        nameof(ShapeBounds),
        typeof(RectangleF),
        typeof(ShapeResizeOverlay),
        new PropertyMetadata(RectangleF.Empty, OnShapeBoundsChanged));

    private static void OnShapeBoundsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ShapeResizeOverlay overlay && e.NewValue is RectangleF bounds)
        {
            overlay.UpdateLayout(bounds);
        }
    }

    public RectangleF ShapeBounds
    {
        get => Get<RectangleF>(ShapeBoundsProperty);
        set => Set(ShapeBoundsProperty, value);
    }

    public event EventHandler<RectangleF>? BoundsChanged;
    public event EventHandler? ResizeComplete;

    public bool IsManipulating => _activeHandle != ResizeHandle.None;

    private ResizeHandle _activeHandle = ResizeHandle.None;
    private Point _handleStartPoint;
    private RectangleF _initialBounds;

    private enum ResizeHandle
    {
        None,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        Top,
        Bottom,
        Left,
        Right,
        Move
    }

    public ShapeResizeOverlay()
    {
        InitializeComponent();
        AttachHandleEvents();
    }

    private void AttachHandleEvents()
    {
        // Top left
        Handle_TopLeft.PointerPressed += (s, e) => StartResize(ResizeHandle.TopLeft, e);
        Handle_TopLeft.PointerMoved += (s, e) => ContinueResize(e);
        Handle_TopLeft.PointerReleased += (s, e) => EndResize(e);
        Handle_TopLeft.PointerEntered += (s, e) => SetCursorForHandle(ResizeHandle.TopLeft);
        Handle_TopLeft.PointerExited += (s, e) => ResetCursor();

        // Top right
        Handle_TopRight.PointerPressed += (s, e) => StartResize(ResizeHandle.TopRight, e);
        Handle_TopRight.PointerMoved += (s, e) => ContinueResize(e);
        Handle_TopRight.PointerReleased += (s, e) => EndResize(e);
        Handle_TopRight.PointerEntered += (s, e) => SetCursorForHandle(ResizeHandle.TopRight);
        Handle_TopRight.PointerExited += (s, e) => ResetCursor();

        // Bottom left
        Handle_BottomLeft.PointerPressed += (s, e) => StartResize(ResizeHandle.BottomLeft, e);
        Handle_BottomLeft.PointerMoved += (s, e) => ContinueResize(e);
        Handle_BottomLeft.PointerReleased += (s, e) => EndResize(e);
        Handle_BottomLeft.PointerEntered += (s, e) => SetCursorForHandle(ResizeHandle.BottomLeft);
        Handle_BottomLeft.PointerExited += (s, e) => ResetCursor();

        // Bottom right
        Handle_BottomRight.PointerPressed += (s, e) => StartResize(ResizeHandle.BottomRight, e);
        Handle_BottomRight.PointerMoved += (s, e) => ContinueResize(e);
        Handle_BottomRight.PointerReleased += (s, e) => EndResize(e);
        Handle_BottomRight.PointerEntered += (s, e) => SetCursorForHandle(ResizeHandle.BottomRight);
        Handle_BottomRight.PointerExited += (s, e) => ResetCursor();

        // Top
        Handle_Top.PointerPressed += (s, e) => StartResize(ResizeHandle.Top, e);
        Handle_Top.PointerMoved += (s, e) => ContinueResize(e);
        Handle_Top.PointerReleased += (s, e) => EndResize(e);
        Handle_Top.PointerEntered += (s, e) => SetCursorForHandle(ResizeHandle.Top);
        Handle_Top.PointerExited += (s, e) => ResetCursor();

        // Bottom
        Handle_Bottom.PointerPressed += (s, e) => StartResize(ResizeHandle.Bottom, e);
        Handle_Bottom.PointerMoved += (s, e) => ContinueResize(e);
        Handle_Bottom.PointerReleased += (s, e) => EndResize(e);
        Handle_Bottom.PointerEntered += (s, e) => SetCursorForHandle(ResizeHandle.Bottom);
        Handle_Bottom.PointerExited += (s, e) => ResetCursor();

        // Left
        Handle_Left.PointerPressed += (s, e) => StartResize(ResizeHandle.Left, e);
        Handle_Left.PointerMoved += (s, e) => ContinueResize(e);
        Handle_Left.PointerReleased += (s, e) => EndResize(e);
        Handle_Left.PointerEntered += (s, e) => SetCursorForHandle(ResizeHandle.Left);
        Handle_Left.PointerExited += (s, e) => ResetCursor();

        // Right
        Handle_Right.PointerPressed += (s, e) => StartResize(ResizeHandle.Right, e);
        Handle_Right.PointerMoved += (s, e) => ContinueResize(e);
        Handle_Right.PointerReleased += (s, e) => EndResize(e);
        Handle_Right.PointerEntered += (s, e) => SetCursorForHandle(ResizeHandle.Right);
        Handle_Right.PointerExited += (s, e) => ResetCursor();

        // Move (interior area)
        MoveHandle.PointerPressed += (s, e) => StartResize(ResizeHandle.Move, e);
        MoveHandle.PointerMoved += (s, e) => ContinueResize(e);
        MoveHandle.PointerReleased += (s, e) => EndResize(e);
        MoveHandle.PointerEntered += (s, e) => SetCursorForHandle(ResizeHandle.Move);
        MoveHandle.PointerExited += (s, e) => ResetCursor();
    }

    private void StartResize(ResizeHandle handle, PointerRoutedEventArgs e)
    {
        _activeHandle = handle;
        _handleStartPoint = e.GetCurrentPoint(this).Position;
        _initialBounds = ShapeBounds;

        var element = e.OriginalSource as UIElement;
        element?.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void ContinueResize(PointerRoutedEventArgs e)
    {
        if (_activeHandle == ResizeHandle.None)
        {
            return;
        }

        var currentPoint = e.GetCurrentPoint(this).Position;
        var deltaX = (float)(currentPoint.X - _handleStartPoint.X);
        var deltaY = (float)(currentPoint.Y - _handleStartPoint.Y);

        var newBounds = _initialBounds;

        switch (_activeHandle)
        {
            case ResizeHandle.TopLeft:
                newBounds.X += deltaX;
                newBounds.Y += deltaY;
                newBounds.Width -= deltaX;
                newBounds.Height -= deltaY;
                break;

            case ResizeHandle.TopRight:
                newBounds.Y += deltaY;
                newBounds.Width += deltaX;
                newBounds.Height -= deltaY;
                break;

            case ResizeHandle.BottomLeft:
                newBounds.X += deltaX;
                newBounds.Width -= deltaX;
                newBounds.Height += deltaY;
                break;

            case ResizeHandle.BottomRight:
                newBounds.Width += deltaX;
                newBounds.Height += deltaY;
                break;

            case ResizeHandle.Top:
                newBounds.Y += deltaY;
                newBounds.Height -= deltaY;
                break;

            case ResizeHandle.Bottom:
                newBounds.Height += deltaY;
                break;

            case ResizeHandle.Left:
                newBounds.X += deltaX;
                newBounds.Width -= deltaX;
                break;

            case ResizeHandle.Right:
                newBounds.Width += deltaX;
                break;

            case ResizeHandle.Move:
                // Move the entire shape
                newBounds.X += deltaX;
                newBounds.Y += deltaY;
                break;
        }

        // Enforce minimum size (but not for Move operations)
        if (_activeHandle != ResizeHandle.Move)
        {
            if (newBounds.Width < 2)
            {
                // Adjust X position based on which edge is being dragged
                if (_activeHandle == ResizeHandle.TopLeft || _activeHandle == ResizeHandle.Left || _activeHandle == ResizeHandle.BottomLeft)
                {
                    // Left edge being dragged - keep right edge fixed
                    float rightEdge = newBounds.X + newBounds.Width;
                    newBounds.Width = 2;
                    newBounds.X = rightEdge - 2;
                }
                else
                {
                    // Right edge being dragged - keep left edge fixed
                    newBounds.Width = 2;
                }
            }

            if (newBounds.Height < 2)
            {
                // Adjust Y position based on which edge is being dragged
                if (_activeHandle == ResizeHandle.TopLeft || _activeHandle == ResizeHandle.Top || _activeHandle == ResizeHandle.TopRight)
                {
                    // Top edge being dragged - keep bottom edge fixed
                    float bottomEdge = newBounds.Y + newBounds.Height;
                    newBounds.Height = 2;
                    newBounds.Y = bottomEdge - 2;
                }
                else
                {
                    // Bottom edge being dragged - keep top edge fixed
                    newBounds.Height = 2;
                }
            }
        }

        ShapeBounds = newBounds;
        BoundsChanged?.Invoke(this, newBounds);
        e.Handled = true;
    }

    private void EndResizeCore()
    {
        if (_activeHandle != ResizeHandle.None)
        {
            _activeHandle = ResizeHandle.None;
            ResizeComplete?.Invoke(this, EventArgs.Empty);
        }
    }

    private void EndResize(PointerRoutedEventArgs e)
    {
        if (_activeHandle != ResizeHandle.None)
        {
            EndResizeCore();

            var element = e.OriginalSource as UIElement;
            element?.ReleasePointerCaptures();
            e.Handled = true;
        }
    }

    protected override void OnPointerCaptureLost(PointerRoutedEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndResizeCore();
        e.Handled = true;
    }

    protected override void OnPointerCanceled(PointerRoutedEventArgs e)
    {
        base.OnPointerCanceled(e);
        EndResizeCore();
        e.Handled = true;
    }

    private void UpdateLayout(RectangleF bounds)
    {
        if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        Visibility = Visibility.Visible;

        // Position and size the boundary
        Canvas.SetLeft(Boundary, bounds.X);
        Canvas.SetTop(Boundary, bounds.Y);
        Boundary.Width = bounds.Width;
        Boundary.Height = bounds.Height;
    }

    private void SetCursorForHandle(ResizeHandle handle)
    {
        ProtectedCursor = handle switch
        {
            ResizeHandle.TopLeft => Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeNorthwestSoutheast),
            ResizeHandle.TopRight => Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeNortheastSouthwest),
            ResizeHandle.BottomLeft => Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeNortheastSouthwest),
            ResizeHandle.BottomRight => Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeNorthwestSoutheast),
            ResizeHandle.Top => Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeNorthSouth),
            ResizeHandle.Bottom => Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeNorthSouth),
            ResizeHandle.Left => Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeWestEast),
            ResizeHandle.Right => Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeWestEast),
            ResizeHandle.Move => Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeAll),
            _ => null
        };
    }

    private void ResetCursor()
    {
        ProtectedCursor = null;
    }
}
