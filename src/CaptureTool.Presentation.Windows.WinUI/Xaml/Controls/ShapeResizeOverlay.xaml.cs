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
        Right
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

        // Top right
        Handle_TopRight.PointerPressed += (s, e) => StartResize(ResizeHandle.TopRight, e);
        Handle_TopRight.PointerMoved += (s, e) => ContinueResize(e);
        Handle_TopRight.PointerReleased += (s, e) => EndResize(e);

        // Bottom left
        Handle_BottomLeft.PointerPressed += (s, e) => StartResize(ResizeHandle.BottomLeft, e);
        Handle_BottomLeft.PointerMoved += (s, e) => ContinueResize(e);
        Handle_BottomLeft.PointerReleased += (s, e) => EndResize(e);

        // Bottom right
        Handle_BottomRight.PointerPressed += (s, e) => StartResize(ResizeHandle.BottomRight, e);
        Handle_BottomRight.PointerMoved += (s, e) => ContinueResize(e);
        Handle_BottomRight.PointerReleased += (s, e) => EndResize(e);

        // Top
        Handle_Top.PointerPressed += (s, e) => StartResize(ResizeHandle.Top, e);
        Handle_Top.PointerMoved += (s, e) => ContinueResize(e);
        Handle_Top.PointerReleased += (s, e) => EndResize(e);

        // Bottom
        Handle_Bottom.PointerPressed += (s, e) => StartResize(ResizeHandle.Bottom, e);
        Handle_Bottom.PointerMoved += (s, e) => ContinueResize(e);
        Handle_Bottom.PointerReleased += (s, e) => EndResize(e);

        // Left
        Handle_Left.PointerPressed += (s, e) => StartResize(ResizeHandle.Left, e);
        Handle_Left.PointerMoved += (s, e) => ContinueResize(e);
        Handle_Left.PointerReleased += (s, e) => EndResize(e);

        // Right
        Handle_Right.PointerPressed += (s, e) => StartResize(ResizeHandle.Right, e);
        Handle_Right.PointerMoved += (s, e) => ContinueResize(e);
        Handle_Right.PointerReleased += (s, e) => EndResize(e);
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
        }

        // Enforce minimum size
        if (newBounds.Width < 2)
        {
            newBounds.Width = 2;
            if (_activeHandle == ResizeHandle.TopLeft || _activeHandle == ResizeHandle.Left || _activeHandle == ResizeHandle.BottomLeft)
            {
                newBounds.X = _initialBounds.X + _initialBounds.Width - 2;
            }
        }

        if (newBounds.Height < 2)
        {
            newBounds.Height = 2;
            if (_activeHandle == ResizeHandle.TopLeft || _activeHandle == ResizeHandle.Top || _activeHandle == ResizeHandle.TopRight)
            {
                newBounds.Y = _initialBounds.Y + _initialBounds.Height - 2;
            }
        }

        ShapeBounds = newBounds;
        BoundsChanged?.Invoke(this, newBounds);
        e.Handled = true;
    }

    private void EndResize(PointerRoutedEventArgs e)
    {
        if (_activeHandle != ResizeHandle.None)
        {
            _activeHandle = ResizeHandle.None;
            ResizeComplete?.Invoke(this, EventArgs.Empty);

            var element = e.OriginalSource as UIElement;
            element?.ReleasePointerCaptures();
            e.Handled = true;
        }
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
}
