using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using System;
using Windows.UI.Core;
using Rectangle = System.Drawing.Rectangle;
using Point = Windows.Foundation.Point;

namespace CaptureTool.UI.Windows.Xaml.Controls;

public sealed partial class SelectionOverlay : UserControlBase
{
    private enum CursorContext 
    { 
        None,
        Anchor,
        Boundary
    }

    private Point _selectionAnchorLastPointerPosition;
    private Point _selectionBoundaryLastPointerPosition;
    private Polygon? _activeSelectionAnchor = null;
    private bool _isSelectionBoundaryDragging = false;
    private CursorContext _currentCursorContext = CursorContext.None;
    private bool _isCreatingNewSelection = false;
    private Point _newSelectionAnchor;

    public static readonly DependencyProperty SelectionRectProperty = DependencyProperty.Register(
        nameof(SelectionRect),
        typeof(Rectangle),
        typeof(SelectionOverlay),
        new PropertyMetadata(new Rectangle(0,0,0,0), OnSelectionRectPropertyChanged));

    private static void OnSelectionRectPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SelectionOverlay control)
        {
            control.UpdateSelectionBoundary();
        }
    }

    public Rectangle SelectionRect
    {
        get => Get<Rectangle>(SelectionRectProperty);
        set => Set(SelectionRectProperty, value);
    }

    public SelectionOverlay()
    {
        InitializeComponent();
        AttachSelectionAnchorEvents();
        SizeChanged += SelectionOverlay_SizeChanged;

        SelectionCanvas.PointerPressed += SelectionCanvas_PointerPressed;
        SelectionCanvas.PointerMoved += SelectionCanvas_PointerMoved;
        SelectionCanvas.PointerReleased += SelectionCanvas_PointerReleased;
        SelectionCanvas.PointerCanceled += SelectionCanvas_PointerCanceled;
    }

    private void SelectionOverlay_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        double height = e.NewSize.Height;
        double width = e.NewSize.Width;

        SelectionCanvas.Height = height;
        SelectionCanvas.Width = width;

        SelectionBoundary.Height = SelectionCanvas.Height;
        SelectionBoundary.Width = SelectionCanvas.Width;

        UpdateSelectionBoundary();
    }

    private void UpdateSelectionBoundary()
    {
        double left = Math.Clamp(SelectionRect.Left, 0, SelectionCanvas.Width);
        double top = Math.Clamp(SelectionRect.Top, 0, SelectionCanvas.Height);
        double right = Math.Clamp(SelectionRect.Right, 0, SelectionCanvas.Width);
        double bottom = Math.Clamp(SelectionRect.Bottom, 0, SelectionCanvas.Height);
        double centerX = (left + right) / 2;
        double centerY = (top + bottom) / 2;

        SelectionBoundary.BorderThickness = new Thickness(left, top, SelectionCanvas.Width - right, SelectionCanvas.Height - bottom);

        Canvas.SetLeft(SelectionAnchor_TopLeft, left - SelectionAnchor_TopLeft.Width / 2);
        Canvas.SetTop(SelectionAnchor_TopLeft, top - SelectionAnchor_TopLeft.Height / 2);

        Canvas.SetLeft(SelectionAnchor_TopRight, right - SelectionAnchor_TopRight.Width / 2);
        Canvas.SetTop(SelectionAnchor_TopRight, top - SelectionAnchor_TopRight.Height / 2);

        Canvas.SetLeft(SelectionAnchor_BottomLeft, left - SelectionAnchor_BottomLeft.Width / 2);
        Canvas.SetTop(SelectionAnchor_BottomLeft, bottom - SelectionAnchor_BottomLeft.Height / 2);

        Canvas.SetLeft(SelectionAnchor_BottomRight, right - SelectionAnchor_BottomRight.Width / 2);
        Canvas.SetTop(SelectionAnchor_BottomRight, bottom - SelectionAnchor_BottomRight.Height / 2);

        Canvas.SetLeft(SelectionAnchor_Top, centerX - SelectionAnchor_Top.Width / 2);
        Canvas.SetTop(SelectionAnchor_Top, top - SelectionAnchor_Top.Height / 2);

        Canvas.SetLeft(SelectionAnchor_Bottom, centerX - SelectionAnchor_Bottom.Width / 2);
        Canvas.SetTop(SelectionAnchor_Bottom, bottom - SelectionAnchor_Bottom.Height / 2);

        Canvas.SetLeft(SelectionAnchor_Left, left - SelectionAnchor_Left.Width / 2);
        Canvas.SetTop(SelectionAnchor_Left, centerY - SelectionAnchor_Left.Height / 2);

        Canvas.SetLeft(SelectionAnchor_Right, right - SelectionAnchor_Right.Width / 2);
        Canvas.SetTop(SelectionAnchor_Right, centerY - SelectionAnchor_Right.Height / 2);
    }

    private Polygon[] GetSelectionAnchors()
    {
        return [
            SelectionAnchor_TopLeft,
            SelectionAnchor_TopRight,
            SelectionAnchor_BottomLeft,
            SelectionAnchor_BottomRight,
            SelectionAnchor_Top,
            SelectionAnchor_Bottom,
            SelectionAnchor_Left,
            SelectionAnchor_Right
        ];
    }

    private void AttachSelectionAnchorEvents()
    {
        foreach (var anchor in GetSelectionAnchors())
        {
            anchor.PointerPressed += SelectionAnchor_PointerPressed;
            anchor.PointerMoved += SelectionAnchor_PointerMoved;
            anchor.PointerReleased += SelectionAnchor_PointerReleased;
            anchor.PointerCanceled += SelectionAnchor_PointerCanceled;
            anchor.PointerEntered += SelectionAnchor_PointerEntered;
            anchor.PointerExited += SelectionAnchor_PointerExited;
        }

        SelectionBoundary.PointerPressed += SelectionBoundary_PointerPressed;
        SelectionBoundary.PointerMoved += SelectionBoundary_PointerMoved;
        SelectionBoundary.PointerReleased += SelectionBoundary_PointerReleased;
        SelectionBoundary.PointerCanceled += SelectionBoundary_PointerCanceled;

        SelectionBoundary.PointerEntered += SelectionBoundary_PointerEntered;
        SelectionBoundary.PointerExited += SelectionBoundary_PointerExited;
    }

    private void SelectionAnchor_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Polygon anchor)
        {
            _activeSelectionAnchor = anchor;
            _selectionAnchorLastPointerPosition = e.GetCurrentPoint(SelectionCanvas).Position;
            anchor.CapturePointer(e.Pointer);
            e.Handled = true;
        }
    }

    private void SelectionAnchor_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_activeSelectionAnchor != null && e.Pointer.IsInContact)
        {
            var currentPosition = e.GetCurrentPoint(SelectionCanvas).Position;
            double deltaX = currentPosition.X - _selectionAnchorLastPointerPosition.X;
            double deltaY = currentPosition.Y - _selectionAnchorLastPointerPosition.Y;

            double canvasWidth = SelectionCanvas.Width;
            double canvasHeight = SelectionCanvas.Height;

            double left = SelectionRect.Left;
            double top = SelectionRect.Top;
            double right = SelectionRect.Right;
            double bottom = SelectionRect.Bottom;

            double newLeft = left;
            double newTop = top;
            double newRight = right;
            double newBottom = bottom;

            // Clamp left and top so that width and height are always at least 1
            if (_activeSelectionAnchor == SelectionAnchor_TopLeft)
            {
                newLeft = Math.Clamp(left + deltaX, 0, right - 1);
                newTop = Math.Clamp(top + deltaY, 0, bottom - 1);
            }
            else if (_activeSelectionAnchor == SelectionAnchor_TopRight)
            {
                newRight = Math.Clamp(right + deltaX, left + 1, canvasWidth);
                newTop = Math.Clamp(top + deltaY, 0, bottom - 1);
            }
            else if (_activeSelectionAnchor == SelectionAnchor_BottomLeft)
            {
                newLeft = Math.Clamp(left + deltaX, 0, right - 1);
                newBottom = Math.Clamp(bottom + deltaY, top + 1, canvasHeight);
            }
            else if (_activeSelectionAnchor == SelectionAnchor_BottomRight)
            {
                newRight = Math.Clamp(right + deltaX, left + 1, canvasWidth);
                newBottom = Math.Clamp(bottom + deltaY, top + 1, canvasHeight);
            }
            else if (_activeSelectionAnchor == SelectionAnchor_Top)
            {
                newTop = Math.Clamp(top + deltaY, 0, bottom - 1);
            }
            else if (_activeSelectionAnchor == SelectionAnchor_Bottom)
            {
                newBottom = Math.Clamp(bottom + deltaY, top + 1, canvasHeight);
            }
            else if (_activeSelectionAnchor == SelectionAnchor_Left)
            {
                newLeft = Math.Clamp(left + deltaX, 0, right - 1);
            }
            else if (_activeSelectionAnchor == SelectionAnchor_Right)
            {
                newRight = Math.Clamp(right + deltaX, left + 1, canvasWidth);
            }

            int intLeft = (int)Math.Round(newLeft);
            int intTop = (int)Math.Round(newTop);
            int intRight = (int)Math.Round(newRight);
            int intBottom = (int)Math.Round(newBottom);

            int width = Math.Max(1, intRight - intLeft);
            int height = Math.Max(1, intBottom - intTop);

            SelectionRect = new Rectangle(intLeft, intTop, width, height);
            _selectionAnchorLastPointerPosition = currentPosition;
            e.Handled = true;
        }
    }

    private void SelectionAnchor_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_activeSelectionAnchor != null)
        {
            _activeSelectionAnchor.ReleasePointerCaptures();
            _activeSelectionAnchor = null;
            _currentCursorContext = CursorContext.None;
            ProtectedCursor = InputCursor.CreateFromCoreCursor(new(CoreCursorType.Arrow, 0));
            e.Handled = true;
        }
    }

    private void SelectionAnchor_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (_activeSelectionAnchor != null)
        {
            _activeSelectionAnchor.ReleasePointerCaptures();
            _activeSelectionAnchor = null;
            _currentCursorContext = CursorContext.None;
            ProtectedCursor = InputCursor.CreateFromCoreCursor(new(CoreCursorType.Arrow, 0));
            e.Handled = true;
        }
    }

    private void SelectionAnchor_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Polygon anchor)
        {
            _currentCursorContext = CursorContext.Anchor;
            ProtectedCursor = GetCursorForAnchor(anchor);
        }
    }

    private void SelectionAnchor_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (_activeSelectionAnchor == null)
        {
            _currentCursorContext = CursorContext.Boundary;
            ProtectedCursor = InputCursor.CreateFromCoreCursor(new(CoreCursorType.SizeAll, 0));
        }
    }

    private void SelectionBoundary_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_activeSelectionAnchor == null)
        {
            var pointerPos = e.GetCurrentPoint(SelectionCanvas).Position;
            if (IsPointerOverSelectionArea(pointerPos) && !IsPointerOverAnyAnchor(pointerPos))
            {
                _isSelectionBoundaryDragging = true;
                _selectionBoundaryLastPointerPosition = pointerPos;
                SelectionBoundary.CapturePointer(e.Pointer);
                e.Handled = true;
            }
        }
    }

    private void SelectionBoundary_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var pointerPos = e.GetCurrentPoint(SelectionCanvas).Position;
        if (_isSelectionBoundaryDragging && e.Pointer.IsInContact)
        {
            double deltaX = pointerPos.X - _selectionBoundaryLastPointerPosition.X;
            double deltaY = pointerPos.Y - _selectionBoundaryLastPointerPosition.Y;

            double canvasWidth = SelectionCanvas.Width;
            double canvasHeight = SelectionCanvas.Height;

            double left = SelectionRect.Left + deltaX;
            double top = SelectionRect.Top + deltaY;
            double width = SelectionRect.Width;
            double height = SelectionRect.Height;

            // Clamp so the selection rect stays within the canvas
            left = Math.Clamp(left, 0, canvasWidth - width);
            top = Math.Clamp(top, 0, canvasHeight - height);

            SelectionRect = new Rectangle(
                Convert.ToInt32(left), 
                Convert.ToInt32(top), 
                Convert.ToInt32(width), 
                Convert.ToInt32(height));
            _selectionBoundaryLastPointerPosition = pointerPos;
            e.Handled = true;
        }
        else if (_currentCursorContext != CursorContext.Anchor)
        {
            // Update cursor on hover
            if (IsPointerOverSelectionArea(pointerPos) && !IsPointerOverAnyAnchor(pointerPos))
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

    private void SelectionBoundary_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isSelectionBoundaryDragging)
        {
            _isSelectionBoundaryDragging = false;
            SelectionBoundary.ReleasePointerCaptures();

            var pointerPos = e.GetCurrentPoint(SelectionCanvas).Position;
            if (IsPointerOverAnyAnchor(pointerPos))
            {
                // Let anchor logic handle cursor
            }
            else if (IsPointerOverSelectionArea(pointerPos))
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

    private void SelectionBoundary_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (_isSelectionBoundaryDragging)
        {
            _isSelectionBoundaryDragging = false;
            SelectionBoundary.ReleasePointerCaptures();
            _currentCursorContext = CursorContext.None;
            ProtectedCursor = InputCursor.CreateFromCoreCursor(new(CoreCursorType.Arrow, 0));
            e.Handled = true;
        }
    }

    private void SelectionBoundary_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (_currentCursorContext != CursorContext.Anchor)
        {
            var pointerPos = e.GetCurrentPoint(SelectionCanvas).Position;
            if (IsPointerOverSelectionArea(pointerPos) && !IsPointerOverAnyAnchor(pointerPos))
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

    private void SelectionBoundary_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (_currentCursorContext == CursorContext.Boundary)
        {
            _currentCursorContext = CursorContext.None;
            ProtectedCursor = InputCursor.CreateFromCoreCursor(new(CoreCursorType.Arrow, 0));
        }
    }

    private void SelectionCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var pointerPos = e.GetCurrentPoint(SelectionCanvas).Position;
        if (!IsPointerOverSelectionArea(pointerPos) && !IsPointerOverAnyAnchor(pointerPos))
        {
            _isCreatingNewSelection = true;
            _newSelectionAnchor = pointerPos;

            // Start with a 1x1 rectangle at the pointer position
            SelectionRect = new Rectangle(
                (int)Math.Clamp(pointerPos.X, 0, SelectionCanvas.Width - 1),
                (int)Math.Clamp(pointerPos.Y, 0, SelectionCanvas.Height - 1),
                1,
                1
            );

            SelectionCanvas.CapturePointer(e.Pointer);
            e.Handled = true;
        }
    }

    private void SelectionCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_isCreatingNewSelection && e.Pointer.IsInContact)
        {
            var pointerPos = e.GetCurrentPoint(SelectionCanvas).Position;

            double x1 = Math.Clamp(_newSelectionAnchor.X, 0, SelectionCanvas.Width);
            double y1 = Math.Clamp(_newSelectionAnchor.Y, 0, SelectionCanvas.Height);
            double x2 = Math.Clamp(pointerPos.X, 0, SelectionCanvas.Width);
            double y2 = Math.Clamp(pointerPos.Y, 0, SelectionCanvas.Height);

            double left = Math.Min(x1, x2);
            double top = Math.Min(y1, y2);
            double right = Math.Max(x1, x2);
            double bottom = Math.Max(y1, y2);

            int intLeft = (int)Math.Round(left);
            int intTop = (int)Math.Round(top);
            int width = Math.Max(1, (int)Math.Round(right - left));
            int height = Math.Max(1, (int)Math.Round(bottom - top));

            SelectionRect = new Rectangle(intLeft, intTop, width, height);
            e.Handled = true;
        }
    }

    private void SelectionCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isCreatingNewSelection)
        {
            _isCreatingNewSelection = false;
            SelectionCanvas.ReleasePointerCaptures();
            e.Handled = true;
        }
    }

    private void SelectionCanvas_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (_isCreatingNewSelection)
        {
            _isCreatingNewSelection = false;
            SelectionCanvas.ReleasePointerCaptures();
            e.Handled = true;
        }
    }

    private InputCursor GetCursorForAnchor(Polygon anchor)
    {
        // Set cursor type based on anchor position
        if (anchor == SelectionAnchor_TopLeft || anchor == SelectionAnchor_BottomRight)
            return InputCursor.CreateFromCoreCursor(new(CoreCursorType.SizeNorthwestSoutheast, 0));
        if (anchor == SelectionAnchor_TopRight || anchor == SelectionAnchor_BottomLeft)
            return InputCursor.CreateFromCoreCursor(new(CoreCursorType.SizeNortheastSouthwest, 0));
        if (anchor == SelectionAnchor_Top || anchor == SelectionAnchor_Bottom)
            return InputCursor.CreateFromCoreCursor(new(CoreCursorType.SizeNorthSouth, 0));
        if (anchor == SelectionAnchor_Left || anchor == SelectionAnchor_Right)
            return InputCursor.CreateFromCoreCursor(new(CoreCursorType.SizeWestEast, 0));
        return InputCursor.CreateFromCoreCursor(new(CoreCursorType.Arrow, 0));
    }

    private bool IsPointerOverSelectionArea(Point pos)
    {
        return IsPointerOverSelectionArea(pos, SelectionRect);
    }

    private bool IsPointerOverAnyAnchor(Point pos)
    {
        var anchors = GetSelectionAnchors();
        return IsPointerOverAnyElement(pos, anchors);
    }


    private static bool IsPointerOverSelectionArea(Point pos, Rectangle selectionRect)
    {
        double left = selectionRect.Left;
        double top = selectionRect.Top;
        double right = selectionRect.Right;
        double bottom = selectionRect.Bottom;

        // Check if inside the selection area rectangle
        return pos.X > left && pos.X < right && pos.Y > top && pos.Y < bottom;
    }
    private static bool IsPointerOverAnyElement(Point pos, FrameworkElement[] elements)
    {
        foreach (var element in elements)
        {
            var elementLeft = Canvas.GetLeft(element);
            var elementTop = Canvas.GetTop(element);
            var elementRight = elementLeft + element.Width;
            var elementBottom = elementTop + element.Height;
            if (pos.X >= elementLeft && pos.X <= elementRight &&
                pos.Y >= elementTop && pos.Y <= elementBottom)
            {
                return true;
            }
        }
        return false;
    }
}
