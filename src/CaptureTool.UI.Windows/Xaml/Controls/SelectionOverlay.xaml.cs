using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System;
using Windows.UI.Core;
using Point = Windows.Foundation.Point;
using Rectangle = System.Drawing.Rectangle;

namespace CaptureTool.UI.Windows.Xaml.Controls;

public sealed partial class SelectionOverlay : UserControlBase
{
    private enum CursorContext 
    { 
        None,
        Boundary
    }

    private Point _selectionBoundaryLastPointerPosition;
    private bool _isSelectionBoundaryDragging = false;
    private CursorContext _currentCursorContext = CursorContext.None;
    private bool _isCreatingNewSelection = false;
    private Point _newSelectionAnchor;

    public static readonly DependencyProperty SelectionRectProperty = DependencyProperty.Register(
        nameof(SelectionRect),
        typeof(Rectangle),
        typeof(SelectionOverlay),
        new PropertyMetadata(Rectangle.Empty, OnSelectionRectPropertyChanged));

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

    public event EventHandler? SelectionComplete;

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

        SelectionBoundary.BorderThickness = new Thickness(left, top, SelectionCanvas.Width - right, SelectionCanvas.Height - bottom);
    }

    private void AttachSelectionAnchorEvents()
    {
        SelectionBoundary.PointerPressed += SelectionBoundary_PointerPressed;
        SelectionBoundary.PointerMoved += SelectionBoundary_PointerMoved;
        SelectionBoundary.PointerReleased += SelectionBoundary_PointerReleased;
        SelectionBoundary.PointerCanceled += SelectionBoundary_PointerCanceled;

        SelectionBoundary.PointerEntered += SelectionBoundary_PointerEntered;
        SelectionBoundary.PointerExited += SelectionBoundary_PointerExited;
    }

    private void SelectionBoundary_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var pointerPos = e.GetCurrentPoint(SelectionCanvas).Position;
        if (IsPointerOverSelectionArea(pointerPos))
        {
            _isSelectionBoundaryDragging = true;
            _selectionBoundaryLastPointerPosition = pointerPos;
            SelectionBoundary.CapturePointer(e.Pointer);
            e.Handled = true;
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
        else if (IsPointerOverSelectionArea(pointerPos) && _currentCursorContext != CursorContext.Boundary)
        {
            _currentCursorContext = CursorContext.Boundary;
            ProtectedCursor = InputCursor.CreateFromCoreCursor(new(CoreCursorType.SizeAll, 0));
        }
        else if (_currentCursorContext != CursorContext.None)
        {
            _currentCursorContext = CursorContext.None;
            ProtectedCursor = InputCursor.CreateFromCoreCursor(new(CoreCursorType.Arrow, 0));
        }
    }

    private void SelectionBoundary_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isSelectionBoundaryDragging)
        {
            _isSelectionBoundaryDragging = false;
            SelectionBoundary.ReleasePointerCaptures();

            var pointerPos = e.GetCurrentPoint(SelectionCanvas).Position;
            if (IsPointerOverSelectionArea(pointerPos))
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
        var pointerPos = e.GetCurrentPoint(SelectionCanvas).Position;
        if (IsPointerOverSelectionArea(pointerPos))
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
        if (!IsPointerOverSelectionArea(pointerPos))
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

            if (SelectionRect.Height >= 40 && SelectionRect.Width >= 40)
            {
                SelectionRectangle.Opacity = 1;
            }
            else
            {
                SelectionRectangle.Opacity = .5;
            }

            e.Handled = true;
        }
    }

    private void SelectionCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isCreatingNewSelection)
        {
            SelectionComplete?.Invoke(this, EventArgs.Empty);

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

    private bool IsPointerOverSelectionArea(Point pos)
    {
        return IsPointerOverSelectionArea(pos, SelectionRect);
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
}
