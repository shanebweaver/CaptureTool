using CaptureTool.Domains.Capture.Interfaces;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Point = Windows.Foundation.Point;
using Rectangle = System.Drawing.Rectangle;

namespace CaptureTool.UI.Windows.Xaml.Controls;

public sealed partial class SelectionOverlay : UserControlBase
{
    private bool _isCreatingNewSelection = false;
    private Point _newSelectionAnchor;

    private Rectangle _selectionRect = Rectangle.Empty;
    public Rectangle SelectionRect
    {
        get => _selectionRect;
        set
        {
            if (_selectionRect != value)
            {
                _selectionRect = value;
                RaisePropertyChanged();
            }
        }
    }

    private CaptureType _captureType = CaptureType.Rectangle;
    public CaptureType CaptureType
    {
        get => _captureType;
        set
        {
            if (_captureType != value)
            {
                _captureType = value;
                RaisePropertyChanged();
            }
        }
    }

    private IEnumerable<Rectangle> _windowRects = [];
    public IEnumerable<Rectangle> WindowRects
    {
        get => _windowRects;
        set
        {
            if (_windowRects != value)
            {
                _windowRects = value;
                RaisePropertyChanged();
            }
        }
    }

    public event EventHandler<Rectangle>? SelectionComplete;

    public SelectionOverlay()
    {
        InitializeComponent();
        SizeChanged += SelectionOverlay_SizeChanged;

        SelectionCanvas.PointerPressed += SelectionCanvas_PointerPressed;
        SelectionCanvas.PointerMoved += SelectionCanvas_PointerMoved;
        SelectionCanvas.PointerReleased += SelectionCanvas_PointerReleased;
        SelectionCanvas.PointerCanceled += SelectionCanvas_PointerCanceled;
        SelectionCanvas.PointerExited += SelectionCanvas_PointerExited;
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

    public void UpdateSelectionRect(Rectangle rect)
    {
        SelectionRect = rect;
        UpdateSelectionBoundary();
    }

    private void UpdateToolTip()
    {
        if (CaptureType == CaptureType.Rectangle && IsValidSelection(SelectionRect))
        {
            double scale = XamlRoot.RasterizationScale;
            SelectionToolTip.Content = $"{Math.Floor(SelectionRect.Width * scale)} × {Math.Floor(SelectionRect.Height * scale)}";
            SelectionToolTip.Visibility = Visibility.Visible;
            double left = Math.Clamp(SelectionRect.Left + (SelectionRect.Width / 2) - (SelectionToolTip.ActualWidth / 2), 0, SelectionCanvas.Width - SelectionToolTip.ActualWidth);
            double top = Math.Clamp(SelectionRect.Top - SelectionToolTip.ActualHeight, 0, SelectionCanvas.Height - SelectionToolTip.ActualHeight);
            Canvas.SetLeft(SelectionToolTip, left);
            Canvas.SetTop(SelectionToolTip, top);
        }
        else
        {
            SelectionToolTip.Visibility = Visibility.Collapsed;
        }
    }

    private void SelectionCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (CaptureType == CaptureType.Rectangle)
        {
            var pointerPos = e.GetCurrentPoint(SelectionCanvas).Position;
            if (!IsPointerOverSelectionArea(pointerPos))
            {
                _isCreatingNewSelection = true;
                _newSelectionAnchor = pointerPos;

                // Start with a 1x1 rectangle at the pointer position
                UpdateSelectionRect(new Rectangle(
                    (int)Math.Clamp(pointerPos.X, 0, SelectionCanvas.Width - 1),
                    (int)Math.Clamp(pointerPos.Y, 0, SelectionCanvas.Height - 1),
                    1,
                    1
                ));

                SelectionCanvas.CapturePointer(e.Pointer);
                e.Handled = true;
            }
        }
        else if (CaptureType == CaptureType.Window)
        {
            var pointerPos = e.GetCurrentPoint(SelectionCanvas).Position;
            if (IsPointerOverSelectionArea(pointerPos))
            {
                _isCreatingNewSelection = true;
                SelectionCanvas.CapturePointer(e.Pointer);
                e.Handled = true;
            }
        }
        else if (CaptureType == CaptureType.FullScreen)
        {
            var pointerPos = e.GetCurrentPoint(SelectionCanvas).Position;
            if (IsPointerOverSelectionArea(pointerPos))
            {
                _isCreatingNewSelection = true;
                SelectionCanvas.CapturePointer(e.Pointer);
                e.Handled = true;
            }
        }
    }

    private void SelectionCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (CaptureType == CaptureType.Rectangle)
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

                UpdateSelectionRect(new Rectangle(intLeft, intTop, width, height));

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
        else if (CaptureType == CaptureType.Window)
        {
            if(_isCreatingNewSelection || e.Pointer.IsInContact)
            {
                return;
            }

            // Look for a window rectangle and if the pointer is inside it, use that rectangle as the selection area
            var pointerPos = e.GetCurrentPoint(SelectionCanvas).Position;
            var pointerPoint = new System.Drawing.Point((int)pointerPos.X, (int)pointerPos.Y);

            bool windowFound = false;
            foreach (var windowRect in WindowRects)
            {
                if (windowRect.Contains(pointerPoint))
                {
                    var adjusted = new Rectangle(
                        Math.Max(windowRect.X, 0),
                        Math.Max(windowRect.Y, 0),
                        windowRect.Width + Math.Min(windowRect.X, 0),
                        windowRect.Height + Math.Min(windowRect.Y, 0));

                    UpdateSelectionRect(adjusted);
                    windowFound = true;
                    break;
                }
            }

            // If no window is found, clear the selection area.
            if (!windowFound)
            {
                UpdateSelectionRect(Rectangle.Empty);
            }

            e.Handled = true;
        }
        else if (CaptureType == CaptureType.FullScreen)
        {
            UpdateSelectionRect(new(0,0, (int)SelectionCanvas.Width, (int)SelectionCanvas.Height));
            e.Handled = true;
        }

        UpdateToolTip();
    }

    private void SelectionCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        SelectionCanvas.ReleasePointerCaptures();

        if (_isCreatingNewSelection)
        {
            if (!IsValidSelection(SelectionRect))
            {
                UpdateSelectionRect(Rectangle.Empty); // Reset selection if too small
            }
            else
            {
                SelectionComplete?.Invoke(this, SelectionRect);
            }

            _isCreatingNewSelection = false;
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

        UpdateSelectionRect(Rectangle.Empty);
    }

    private void SelectionCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        UpdateSelectionRect(Rectangle.Empty);
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

    private static bool IsValidSelection(Rectangle selectionRect)
    {
        return selectionRect.Width >= 40 && selectionRect.Height >= 40;
    }
}
