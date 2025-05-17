using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using System;
using Windows.Foundation;
using Windows.UI.Core;

namespace CaptureTool.UI.Xaml.Controls.ImageCanvas;

public sealed partial class CropOverlay : UserControlBase
{
    private enum CursorContext 
    { 
        None,
        Anchor,
        Boundary
    }

    public static readonly DependencyProperty CropRectProperty = DependencyProperty.Register(
        nameof(CropRect),
        typeof(Rect),
        typeof(CropOverlay),
        new PropertyMetadata(Rect.Empty));

    public Rect CropRect
    {
        get => Get<Rect>(CropRectProperty);
        set => Set(CropRectProperty, value);
    }

    private Point _cropAnchorLastPointerPosition;
    private Point _cropBoundaryLastPointerPosition;
    private Polygon? _activeCropAnchor = null;
    private bool _isCropBoundaryDragging = false;
    private CursorContext _currentCursorContext = CursorContext.None;
    private Thickness _cropOffsets = new(0, 0, 0, 0);

    public CropOverlay()
    {
        InitializeComponent();
        AttachCropAnchorEvents();
        SizeChanged += CropOverlay_SizeChanged;
    }

    private void CropOverlay_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        double height = e.NewSize.Height;
        double width = e.NewSize.Width;

        CropCanvas.Height = height;
        CropCanvas.Width = width;

        CropBoundary.Height = height;
        CropBoundary.Width = width;
        Canvas.SetLeft(CropBoundary, 0);
        Canvas.SetTop(CropBoundary, 0);

        UpdateCropRect();
    }

    private void UpdateCropRect()
    {
        double left = _cropOffsets.Left;
        double top = _cropOffsets.Top;
        double right = CropCanvas.Width - _cropOffsets.Right;
        double bottom = CropCanvas.Height - _cropOffsets.Bottom;

        // Clamp to valid values
        double width = Math.Max(0, right - left);
        double height = Math.Max(0, bottom - top);

        CropRect = new Rect(left, top, width, height);
    }

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

            UpdateCropAnchorPositions();
            UpdateCropRect();
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
            _currentCursorContext = CursorContext.Boundary;
            ProtectedCursor = InputCursor.CreateFromCoreCursor(new(CoreCursorType.SizeAll, 0));
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
            UpdateCropRect();
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

    private bool IsPointerOverCropArea(Point pos)
    {
        double left = _cropOffsets.Left;
        double top = _cropOffsets.Top;
        double right = CropCanvas.Width - _cropOffsets.Right;
        double bottom = CropCanvas.Height - _cropOffsets.Bottom;

        // Check if inside the crop area rectangle
        return pos.X > left && pos.X < right && pos.Y > top && pos.Y < bottom;
    }

    private bool IsPointerOverAnyAnchor(Point pos)
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
