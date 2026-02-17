using CaptureTool.Domain.Edit.Implementations.Windows;
using CaptureTool.Domain.Edit.Interfaces;
using CaptureTool.Domain.Edit.Interfaces.Drawable;
using CaptureTool.Domain.Edit.Interfaces.Operations;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Drawing;
using Windows.System;
using Point = global::Windows.Foundation.Point;
using WinUIColor = global::Windows.UI.Color;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class ImageCanvas : UserControlBase
{
    public static readonly DependencyProperty DrawablesProperty = DependencyProperty.Register(
        nameof(Drawables),
        typeof(IEnumerable<IDrawable>),
        typeof(ImageCanvas),
        new PropertyMetadata(null));

    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation),
        typeof(ImageOrientation),
        typeof(ImageCanvas),
        new PropertyMetadata(ImageOrientation.RotateNoneFlipNone, OnOrientationPropertyChanged));

    public static readonly DependencyProperty CanvasSizeProperty = DependencyProperty.Register(
        nameof(CanvasSize),
        typeof(Size),
        typeof(ImageCanvas),
        new PropertyMetadata(Size.Empty, OnCanvasSizePropertyChanged));

    public static readonly DependencyProperty IsCropModeEnabledProperty = DependencyProperty.Register(
        nameof(IsCropModeEnabled),
        typeof(bool),
        typeof(ImageCanvas),
        new PropertyMetadata(false, OnIsCropModeEnabledPropertyChanged));

    public static readonly DependencyProperty IsShapesModeEnabledProperty = DependencyProperty.Register(
        nameof(IsShapesModeEnabled),
        typeof(bool),
        typeof(ImageCanvas),
        new PropertyMetadata(false, OnIsShapesModeEnabledPropertyChanged));

    private static void OnIsShapesModeEnabledPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageCanvas control && e.NewValue is bool isEnabled && !isEnabled)
        {
            // When leaving shapes mode, deselect any selected shape
            control.DeselectShape();
        }
    }

    public static readonly DependencyProperty CropRectProperty = DependencyProperty.Register(
       nameof(CropRect),
       typeof(Rectangle),
       typeof(ImageCanvas),
       new PropertyMetadata(Rectangle.Empty, OnCropRectPropertyChanged));

    public static readonly DependencyProperty IsAutoZoomLockedProperty = DependencyProperty.Register(
       nameof(IsAutoZoomLocked),
       typeof(bool),
       typeof(ImageCanvas),
       new PropertyMetadata(false));

    public static readonly DependencyProperty SelectedShapeTypeProperty = DependencyProperty.Register(
       nameof(SelectedShapeType),
       typeof(ShapeType),
       typeof(ImageCanvas),
       new PropertyMetadata(ShapeType.Rectangle));

    public static readonly DependencyProperty ShapeStrokeColorProperty = DependencyProperty.Register(
       nameof(ShapeStrokeColor),
       typeof(Color),
       typeof(ImageCanvas),
       new PropertyMetadata(Color.Red, OnShapeStrokeColorChanged));

    private static void OnShapeStrokeColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // If a shape is currently selected, update its stroke color
        // The property value is updated automatically by DependencyProperty for use with new shapes
        if (d is ImageCanvas control && control._selectedShape != null && e.NewValue is Color color)
        {
            control.UpdateSelectedShapeStrokeColor(color);
        }
    }

    public static readonly DependencyProperty ShapeFillColorProperty = DependencyProperty.Register(
       nameof(ShapeFillColor),
       typeof(Color),
       typeof(ImageCanvas),
       new PropertyMetadata(Color.Transparent, OnShapeFillColorChanged));

    private static void OnShapeFillColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // If a shape is currently selected, update its fill color
        // The property value is updated automatically by DependencyProperty for use with new shapes
        if (d is ImageCanvas control && control._selectedShape != null && e.NewValue is Color color)
        {
            control.UpdateSelectedShapeFillColor(color);
        }
    }

    public static readonly DependencyProperty ShapeStrokeWidthProperty = DependencyProperty.Register(
       nameof(ShapeStrokeWidth),
       typeof(int),
       typeof(ImageCanvas),
       new PropertyMetadata(2, OnShapeStrokeWidthChanged));

    private static void OnShapeStrokeWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // If a shape is currently selected, update its stroke width
        // The property value is updated automatically by DependencyProperty for use with new shapes
        if (d is ImageCanvas control && control._selectedShape != null && e.NewValue is int width)
        {
            control.UpdateSelectedShapeStrokeWidth(width);
        }
    }

    private static void OnCropRectPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageCanvas control && !control.IsCropModeEnabled)
        {
            control.RenderCanvas.Invalidate();
        }
    }

    private static void OnIsCropModeEnabledPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageCanvas control)
        {
            control.DispatcherQueue.TryEnqueue(() =>
            {
                if (e.NewValue is bool isCropModeEnabled && isCropModeEnabled)
                {
                    control.DimmedBackgroundRectangle.Visibility = Visibility.Visible;
                }
                else
                {
                    control.DimmedBackgroundRectangle.Visibility = Visibility.Collapsed;
                }
            });

            control.InvalidateCanvas();
        }
    }

    private static void OnOrientationPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageCanvas control)
        {
            control.InvalidateCanvas();
        }
    }

    private static void OnCanvasSizePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageCanvas control)
        {
            control.InvalidateCanvas();
        }
    }

    public IEnumerable<IDrawable> Drawables
    {
        get => Get<IEnumerable<IDrawable>>(DrawablesProperty);
        set => Set(DrawablesProperty, value);
    }

    public ImageOrientation Orientation
    {
        get => Get<ImageOrientation>(OrientationProperty);
        set => Set(OrientationProperty, value);
    }

    public Size CanvasSize
    {
        get => Get<Size>(CanvasSizeProperty);
        set => Set(CanvasSizeProperty, value);
    }

    public bool IsCropModeEnabled
    {
        get => Get<bool>(IsCropModeEnabledProperty);
        set => Set(IsCropModeEnabledProperty, value);
    }

    public bool IsShapesModeEnabled
    {
        get => Get<bool>(IsShapesModeEnabledProperty);
        set => Set(IsShapesModeEnabledProperty, value);
    }

    public Rectangle CropRect
    {
        get => Get<Rectangle>(CropRectProperty);
        set => Set(CropRectProperty, value);
    }

    public bool IsAutoZoomLocked
    {
        get => Get<bool>(IsAutoZoomLockedProperty);
        set => Set(IsAutoZoomLockedProperty, value);
    }

    public ShapeType SelectedShapeType
    {
        get => Get<ShapeType>(SelectedShapeTypeProperty);
        set => Set(SelectedShapeTypeProperty, value);
    }

    public Color ShapeStrokeColor
    {
        get => Get<Color>(ShapeStrokeColorProperty);
        set => Set(ShapeStrokeColorProperty, value);
    }

    public Color ShapeFillColor
    {
        get => Get<Color>(ShapeFillColorProperty);
        set => Set(ShapeFillColorProperty, value);
    }

    public int ShapeStrokeWidth
    {
        get => Get<int>(ShapeStrokeWidthProperty);
        set => Set(ShapeStrokeWidthProperty, value);
    }

    public event EventHandler<Rectangle>? InteractionComplete;
    public event EventHandler<Rectangle>? CropRectChanged;
    public event EventHandler<(System.Numerics.Vector2 Start, System.Numerics.Vector2 End)>? ShapeDrawn;
    public event EventHandler<(double ZoomFactor, ZoomUpdateSource Source)>? ZoomFactorChanged;
    public event EventHandler<int>? ShapeDeleted;
    public event EventHandler<(int ShapeIndex, IDrawable OldState, IDrawable NewState)>? ShapeModified;

    private readonly Lock _zoomUpdateLock = new Lock();

    private const int LineHandleRadius = 6; // Half of handle diameter (12px total)

    private bool _isPointerDown;
    private Point _lastPointerPosition;
    private Point? _shapeStartPoint;
    
    // Shape selection state
    private IDrawable? _selectedShape;
    private int _selectedShapeIndex = -1;
    private ModifyShapeOperation.ShapeState? _shapeStateBeforeModification;
    
    // Track if we're in a potential selection scenario
    private IDrawable? _shapeUnderPointer;
    private Point? _pointerPressPosition;

    // Cached preview elements for performance
    private Microsoft.UI.Xaml.Shapes.Rectangle? _previewRectangle;
    private Microsoft.UI.Xaml.Shapes.Ellipse? _previewEllipse;
    private Microsoft.UI.Xaml.Shapes.Line? _previewLine;
    private Microsoft.UI.Xaml.Shapes.Polyline? _previewArrowHead;
    private Microsoft.UI.Xaml.Media.SolidColorBrush? _previewStrokeBrush;
    private Microsoft.UI.Xaml.Media.SolidColorBrush? _previewFillBrush;
    private Color _cachedStrokeColor;
    private Color _cachedFillColor;

    // Line endpoint manipulation
    private bool _isDraggingLineStart = false;
    private bool _isDraggingLineEnd = false;
    private bool _isDraggingLineMove = false;
    private Point _lineMoveStartPoint;
    private System.Numerics.Vector2 _lineMoveInitialOffset;
    private System.Numerics.Vector2 _lineMoveInitialEndPoint;

    public ImageCanvas()
    {
        InitializeComponent();
        Loaded += ImageCanvas_Loaded;
        Unloaded += ImageCanvas_Unloaded;
        KeyDown += ImageCanvas_KeyDown;
    }

    private void ImageCanvas_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (!IsShapesModeEnabled || _selectedShape == null)
        {
            return;
        }

        switch (e.Key)
        {
            case VirtualKey.Delete:
                // Delete the selected shape
                DeleteSelectedShape();
                e.Handled = true;
                break;

            case VirtualKey.Escape:
                // Deselect the shape
                DeselectShape();
                e.Handled = true;
                break;
        }
    }

    private void ImageCanvas_Loaded(object sender, RoutedEventArgs e)
    {
        // Wire up the ViewChanged event after the control is loaded
        if (CanvasScrollView != null)
        {
            CanvasScrollView.ViewChanged += CanvasScrollView_ViewChanged;
        }
    }

    private void ImageCanvas_Unloaded(object sender, RoutedEventArgs e)
    {
        // Clean up event subscription to prevent memory leak
        if (CanvasScrollView != null)
        {
            CanvasScrollView.ViewChanged -= CanvasScrollView_ViewChanged;
        }
    }

    private void CanvasScrollView_ViewChanged(ScrollView? sender, object args)
    {
        if (sender == null)
        {
            return;
        }

        lock (_zoomUpdateLock)
        {
            double currentZoomFactor = sender.ZoomFactor;
            // Only raise event if zoom actually changed (not just scroll position)
            if (Math.Abs(currentZoomFactor - CanvasScrollView.ZoomFactor) > 1)
            {
                ZoomFactorChanged?.Invoke(this, (currentZoomFactor, ZoomUpdateSource.CanvasGesture));
            }
        }
    }

    #region Zoom, Center, and Size
    private void RootContainer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        InvalidateCanvas();
    }

    private bool IsTurned()
    {
        ImageOrientation orientation = Orientation;
        bool isTurned =
            orientation == ImageOrientation.Rotate90FlipNone ||
            orientation == ImageOrientation.Rotate90FlipX ||
            orientation == ImageOrientation.Rotate270FlipNone ||
            orientation == ImageOrientation.Rotate270FlipX;

        return isTurned;
    }

    private Size GetImageRenderSize()
    {
        bool isTurned = IsTurned();

        double canvasWidth, canvasHeight;

        if (IsCropModeEnabled)
        {
            canvasHeight = isTurned ? CanvasSize.Width : CanvasSize.Height;
            canvasWidth = isTurned ? CanvasSize.Height : CanvasSize.Width;
        }
        else
        {
            // Use CropRect dimensions when crop mode is not enabled
            var crop = CropRect;
            canvasHeight = crop.Height;
            canvasWidth = crop.Width;

            // Fallback to CanvasSize if CropRect is empty or invalid
            if (canvasWidth <= 0 || canvasHeight <= 0)
            {
                canvasHeight = isTurned ? CanvasSize.Width : CanvasSize.Height;
                canvasWidth = isTurned ? CanvasSize.Height : CanvasSize.Width;
            }
        }

        return new((int)canvasWidth, (int)canvasHeight);
    }

    private void UpdateDrawingCanvasSize()
    {
        lock (this)
        {
            Size renderSize = GetImageRenderSize();
            int width = renderSize.Width;
            int height = renderSize.Height;

            CanvasContainer.Width = width;
            CanvasContainer.Height = height;

            CropOverlay.Width = width;
            CropOverlay.Height = height;

            RenderCanvas.Width = width;
            RenderCanvas.Height = height;
            RenderCanvas.Invalidate();
        }
    }

    private void ZoomAndCenter()
    {
        lock (this)
        {
            if (CanvasScrollView == null || RootContainer == null || CanvasSize.Width == 0 || CanvasSize.Height == 0)
            {
                return;
            }

            double containerWidth = RootContainer.ActualWidth;
            double containerHeight = RootContainer.ActualHeight;

            Size renderSize = GetImageRenderSize();
            int canvasWidth = renderSize.Width;
            int canvasHeight = renderSize.Height;

            // Add padding
            int padding = 48;
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

            ZoomFactorChanged?.Invoke(this, (targetZoomFactor, ZoomUpdateSource.ZoomAndCenter));
        }
    }

    public void ForceZoomAndCenter()
    {
        UpdateDrawingCanvasSize();
        ZoomAndCenter();
    }

    public void SetZoom(double zoomLevel, ZoomUpdateSource source)
    {
        lock (this)
        {
            if (CanvasScrollView == null)
            {
                return;
            }

            CanvasScrollView.ZoomTo(
                (float)zoomLevel,
                null,
                new(ScrollingAnimationMode.Auto)
            );
        }

        // For non-programmatic sources, fire the event immediately
        if (source != ZoomUpdateSource.Programmatic)
        {
            ZoomFactorChanged?.Invoke(this, (zoomLevel, source));
        }
    }
    #endregion

    #region Drawing
    public void InvalidateCanvas()
    {
        UpdateDrawingCanvasSize();
        if (!IsAutoZoomLocked)
        {
            ZoomAndCenter();
        }
    }

    public void ForceCanvasRedrawWithResources()
    {
        RenderCanvas.DpiScale = RenderCanvas.DpiScale == 1 ? 1.0001f : 1f;
        InvalidateCanvas();
    }

    private void CanvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        lock (this)
        {
            var rect = (!IsCropModeEnabled) ? CropRect : IsTurned() ? new Rectangle(0, 0, CanvasSize.Height, CanvasSize.Width) : new Rectangle(0, 0, CanvasSize.Width, CanvasSize.Height);
            ImageCanvasRenderOptions options = new(Orientation, CanvasSize, rect);
            
            // Filter out the selected shape ONLY when it's being actively manipulated
            // This prevents double rendering during drag operations
            var drawablesToRender = Drawables;
            if (_selectedShape != null && IsShapeBeingManipulated())
            {
                drawablesToRender = Drawables.Where((d, i) => i != _selectedShapeIndex);
            }
            
            Win2DImageCanvasRenderer.Render([.. drawablesToRender], options, args.DrawingSession);
        }
    }

    private bool IsShapeBeingManipulated()
    {
        // Check if any manipulation is active (line endpoints, line move, or box resize/move)
        return _isDraggingLineStart || _isDraggingLineEnd || _isDraggingLineMove || 
               (_selectedShape is not LineDrawable && _selectedShape is not ArrowDrawable && ShapeResizeOverlay?.IsManipulating == true);
    }

    private bool CanCreateResources() => Drawables.Any();

    private void CanvasControl_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
    {
        if (CanCreateResources())
        {
            // Create any resources needed by the Draw event handler.
            // Asynchronous work can be tracked with TrackAsyncAction:
            args.TrackAsyncAction(CreateResourcesAsync(sender).AsAsyncAction());
        }
    }

    private async Task CreateResourcesAsync(CanvasControl sender)
    {
        // Load bitmaps, create brushes, etc.
        List<Task> preparationTasks = [];

        foreach (IDrawable drawable in Drawables)
        {
            if (drawable is ImageDrawable imageDrawable)
            {
                Task prepTask = Win2DImageCanvasRenderer.PrepareAsync(imageDrawable, sender);
                preparationTasks.Add(prepTask);
            }
        }

        await Task.WhenAll(preparationTasks);
    }
    #endregion

    #region Panning
    private void RootContainer_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (IsShapesModeEnabled)
        {
            // Get position relative to the RenderCanvas
            var point = e.GetCurrentPoint(RenderCanvas);
            if (point.Properties.IsLeftButtonPressed)
            {
                // Store the press position for later use
                _pointerPressPosition = point.Position;
                
                // Check if clicking on the already selected shape
                if (_selectedShape != null && IsPointInShape(point.Position, _selectedShape))
                {
                    // Clicking on already selected shape - let resize handles handle it (for moving)
                    e.Handled = true;
                    return;
                }
                
                // Check if clicking on any unselected shape
                _shapeUnderPointer = FindShapeAtPoint(point.Position);
                
                // If we had a previous selection and clicked away, deselect
                if (_selectedShape != null && _shapeUnderPointer == null)
                {
                    DeselectShape();
                }
                
                // Don't start drawing yet - wait to see if user drags or releases
                _isPointerDown = true;
                RootContainer.CapturePointer(e.Pointer);
                
                e.Handled = true;
                return;
            }
        }

        _isPointerDown = true;
        _lastPointerPosition = e.GetCurrentPoint(RootContainer).Position;
        RootContainer.CapturePointer(e.Pointer);
    }

    private void RootContainer_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_isPointerDown)
        {
            // In shapes mode, handle shape drawing
            if (IsShapesModeEnabled && _pointerPressPosition.HasValue)
            {
                var currentPoint = e.GetCurrentPoint(RenderCanvas).Position;
                
                // Check if user has moved enough to start drawing (threshold to distinguish from click)
                const double dragThreshold = 3.0; // pixels
                double distance = Math.Sqrt(
                    Math.Pow(currentPoint.X - _pointerPressPosition.Value.X, 2) +
                    Math.Pow(currentPoint.Y - _pointerPressPosition.Value.Y, 2));
                
                if (distance > dragThreshold)
                {
                    // User is dragging - start drawing a new shape
                    if (!_shapeStartPoint.HasValue)
                    {
                        // Deselect any shape under pointer since we're drawing
                        if (_shapeUnderPointer != null)
                        {
                            DeselectShape();
                            _shapeUnderPointer = null;
                        }
                        
                        _shapeStartPoint = _pointerPressPosition.Value;
                    }
                    
                    UpdatePreviewShape(_shapeStartPoint.Value, currentPoint);
                }
                
                e.Handled = true;
                return;
            }

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
        if (IsShapesModeEnabled)
        {
            if (_shapeStartPoint.HasValue)
            {
                // User dragged to draw a shape
                var endPoint = e.GetCurrentPoint(RenderCanvas).Position;

                // Hide the preview shape
                ClearPreviewShape();

                // Convert to Vector2 and invoke event
                var start = new System.Numerics.Vector2((float)_shapeStartPoint.Value.X, (float)_shapeStartPoint.Value.Y);
                var end = new System.Numerics.Vector2((float)endPoint.X, (float)endPoint.Y);

                ShapeDrawn?.Invoke(this, (start, end));

                _shapeStartPoint = null;
                e.Handled = true;
            }
            else if (_shapeUnderPointer != null && _pointerPressPosition.HasValue)
            {
                // User clicked without dragging - select the shape
                SelectShapeDirectly(_shapeUnderPointer);
                e.Handled = true;
            }
            
            // Clean up tracking variables
            _shapeUnderPointer = null;
            _pointerPressPosition = null;
        }

        _isPointerDown = false;
        RootContainer.ReleasePointerCaptures();
    }

    private void RootContainer_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        _isPointerDown = false;
        _shapeStartPoint = null;
        _shapeUnderPointer = null;
        _pointerPressPosition = null;
        ClearPreviewShape();
        RootContainer.ReleasePointerCaptures();
    }

    private void RootContainer_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isPointerDown = false;
        _shapeStartPoint = null;
        _shapeUnderPointer = null;
        _pointerPressPosition = null;
        ClearPreviewShape();
    }
    #endregion

    private void UpdateCropRect(Rectangle value)
    {
        CropRect = value;
        CropRectChanged?.Invoke(this, value);
    }

    private void CropOverlay_InteractionComplete(object sender, Rectangle e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            InteractionComplete?.Invoke(this, e);
        });
    }

    private void CropOverlay_SelectionAreaChanged(object sender, Rectangle e)
    {
        UpdateCropRect(e);
    }

    #region Preview Shape Management
    private void UpdatePreviewShape(Point startPoint, Point endPoint)
    {
        float x = (float)Math.Min(startPoint.X, endPoint.X);
        float y = (float)Math.Min(startPoint.Y, endPoint.Y);
        float width = (float)Math.Abs(endPoint.X - startPoint.X);
        float height = (float)Math.Abs(endPoint.Y - startPoint.Y);

        // Update or create brushes only if colors changed
        UpdatePreviewBrushes();

        switch (SelectedShapeType)
        {
            case ShapeType.Rectangle:
                {
                    // Only show rectangle if both dimensions meet minimum
                    if (width < 2 || height < 2)
                    {
                        PreviewShapeCanvas.Visibility = Visibility.Collapsed;
                        return;
                    }

                    // Create rectangle on first use
                    if (_previewRectangle == null)
                    {
                        _previewRectangle = new Microsoft.UI.Xaml.Shapes.Rectangle();
                        PreviewShapeCanvas.Children.Add(_previewRectangle);
                    }

                    // Update properties
                    _previewRectangle.Width = width;
                    _previewRectangle.Height = height;
                    _previewRectangle.Stroke = _previewStrokeBrush;
                    _previewRectangle.Fill = ShapeFillColor.A > 0 ? _previewFillBrush : null;
                    _previewRectangle.StrokeThickness = ShapeStrokeWidth;
                    Canvas.SetLeft(_previewRectangle, x);
                    Canvas.SetTop(_previewRectangle, y);
                    _previewRectangle.Visibility = Visibility.Visible;

                    // Hide other shapes
                    HideOtherPreviewShapes(ShapeType.Rectangle);
                }
                break;

            case ShapeType.Ellipse:
                {
                    // Only show ellipse if both dimensions meet minimum
                    if (width < 2 || height < 2)
                    {
                        PreviewShapeCanvas.Visibility = Visibility.Collapsed;
                        return;
                    }

                    // Create ellipse on first use
                    if (_previewEllipse == null)
                    {
                        _previewEllipse = new Microsoft.UI.Xaml.Shapes.Ellipse();
                        PreviewShapeCanvas.Children.Add(_previewEllipse);
                    }

                    // Update properties
                    _previewEllipse.Width = width;
                    _previewEllipse.Height = height;
                    _previewEllipse.Stroke = _previewStrokeBrush;
                    _previewEllipse.Fill = ShapeFillColor.A > 0 ? _previewFillBrush : null;
                    _previewEllipse.StrokeThickness = ShapeStrokeWidth;
                    Canvas.SetLeft(_previewEllipse, x);
                    Canvas.SetTop(_previewEllipse, y);
                    _previewEllipse.Visibility = Visibility.Visible;

                    // Hide other shapes
                    HideOtherPreviewShapes(ShapeType.Ellipse);
                }
                break;

            case ShapeType.Line:
            case ShapeType.Arrow:
                {
                    float distance = (float)Math.Sqrt(Math.Pow(endPoint.X - startPoint.X, 2) + Math.Pow(endPoint.Y - startPoint.Y, 2));
                    if (distance < 2)
                    {
                        PreviewShapeCanvas.Visibility = Visibility.Collapsed;
                        return;
                    }

                    // Create line on first use
                    if (_previewLine == null)
                    {
                        _previewLine = new Microsoft.UI.Xaml.Shapes.Line();
                        PreviewShapeCanvas.Children.Add(_previewLine);
                    }

                    // Update line properties
                    _previewLine.X1 = startPoint.X;
                    _previewLine.Y1 = startPoint.Y;
                    _previewLine.X2 = endPoint.X;
                    _previewLine.Y2 = endPoint.Y;
                    _previewLine.Stroke = _previewStrokeBrush;
                    _previewLine.StrokeThickness = ShapeStrokeWidth;
                    _previewLine.Visibility = Visibility.Visible;

                    // Handle arrow head
                    if (SelectedShapeType == ShapeType.Arrow)
                    {
                        // Create arrow head on first use
                        if (_previewArrowHead == null)
                        {
                            _previewArrowHead = new Microsoft.UI.Xaml.Shapes.Polyline
                            {
                                Points = new Microsoft.UI.Xaml.Media.PointCollection()
                            };
                            PreviewShapeCanvas.Children.Add(_previewArrowHead);
                        }

                        // Calculate arrow head
                        double angle = Math.Atan2(endPoint.Y - startPoint.Y, endPoint.X - startPoint.X);
                        double arrowLength = 10 + ShapeStrokeWidth;
                        double arrowAngle = Math.PI / 6; // 30 degrees

                        // Update arrow head points
                        _previewArrowHead.Points.Clear();
                        _previewArrowHead.Points.Add(new global::Windows.Foundation.Point(
                            endPoint.X - arrowLength * Math.Cos(angle - arrowAngle),
                            endPoint.Y - arrowLength * Math.Sin(angle - arrowAngle)));
                        _previewArrowHead.Points.Add(new global::Windows.Foundation.Point(endPoint.X, endPoint.Y));
                        _previewArrowHead.Points.Add(new global::Windows.Foundation.Point(
                            endPoint.X - arrowLength * Math.Cos(angle + arrowAngle),
                            endPoint.Y - arrowLength * Math.Sin(angle + arrowAngle)));

                        _previewArrowHead.Stroke = _previewStrokeBrush;
                        _previewArrowHead.StrokeThickness = ShapeStrokeWidth;
                        _previewArrowHead.StrokeLineJoin = Microsoft.UI.Xaml.Media.PenLineJoin.Miter;
                        _previewArrowHead.Visibility = Visibility.Visible;
                    }
                    else if (_previewArrowHead != null)
                    {
                        _previewArrowHead.Visibility = Visibility.Collapsed;
                    }

                    // Hide other shapes
                    HideOtherPreviewShapes(SelectedShapeType == ShapeType.Arrow ? ShapeType.Arrow : ShapeType.Line);
                }
                break;
        }

        PreviewShapeCanvas.Visibility = Visibility.Visible;
    }

    private void UpdatePreviewBrushes()
    {
        // Update stroke brush (compare color components explicitly)
        if (_previewStrokeBrush == null || 
            _cachedStrokeColor.A != ShapeStrokeColor.A ||
            _cachedStrokeColor.R != ShapeStrokeColor.R ||
            _cachedStrokeColor.G != ShapeStrokeColor.G ||
            _cachedStrokeColor.B != ShapeStrokeColor.B)
        {
            _cachedStrokeColor = ShapeStrokeColor;
            if (_previewStrokeBrush == null)
            {
                _previewStrokeBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush();
            }
            _previewStrokeBrush.Color = WinUIColor.FromArgb(
                ShapeStrokeColor.A,
                ShapeStrokeColor.R,
                ShapeStrokeColor.G,
                ShapeStrokeColor.B);
        }

        // Update fill brush (compare color components explicitly)
        if (ShapeFillColor.A > 0)
        {
            if (_previewFillBrush == null || 
                _cachedFillColor.A != ShapeFillColor.A ||
                _cachedFillColor.R != ShapeFillColor.R ||
                _cachedFillColor.G != ShapeFillColor.G ||
                _cachedFillColor.B != ShapeFillColor.B)
            {
                _cachedFillColor = ShapeFillColor;
                if (_previewFillBrush == null)
                {
                    _previewFillBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush();
                }
                _previewFillBrush.Color = WinUIColor.FromArgb(
                    ShapeFillColor.A,
                    ShapeFillColor.R,
                    ShapeFillColor.G,
                    ShapeFillColor.B);
            }
        }
    }

    private void HideOtherPreviewShapes(ShapeType activeType)
    {
        if (activeType != ShapeType.Rectangle && _previewRectangle != null)
        {
            _previewRectangle.Visibility = Visibility.Collapsed;
        }
        if (activeType != ShapeType.Ellipse && _previewEllipse != null)
        {
            _previewEllipse.Visibility = Visibility.Collapsed;
        }
        if (activeType != ShapeType.Line && activeType != ShapeType.Arrow && _previewLine != null)
        {
            _previewLine.Visibility = Visibility.Collapsed;
        }
        if (activeType != ShapeType.Arrow && _previewArrowHead != null)
        {
            _previewArrowHead.Visibility = Visibility.Collapsed;
        }
    }

    private void ClearPreviewShape()
    {
        if (_previewRectangle != null)
        {
            _previewRectangle.Visibility = Visibility.Collapsed;
        }
        if (_previewEllipse != null)
        {
            _previewEllipse.Visibility = Visibility.Collapsed;
        }
        if (_previewLine != null)
        {
            _previewLine.Visibility = Visibility.Collapsed;
        }
        if (_previewArrowHead != null)
        {
            _previewArrowHead.Visibility = Visibility.Collapsed;
        }
        PreviewShapeCanvas.Visibility = Visibility.Collapsed;
    }
    #endregion

    #region Shape Selection and Manipulation
    private void ShapeResizeOverlay_BoundsChanged(object? sender, RectangleF newBounds)
    {
        if (_selectedShape == null)
        {
            return;
        }

        // Update the selected shape's properties based on newBounds
        UpdateSelectedShapeBounds(newBounds);
        
        // Update the preview to show the new bounds
        UpdatePreviewShapeFromDrawable(_selectedShape);
    }

    private void ShapeResizeOverlay_ResizeComplete(object? sender, EventArgs e)
    {
        // Resize/move complete - redraw canvas with updated shape
        if (_selectedShape != null && _shapeStateBeforeModification.HasValue)
        {
            var newState = new ModifyShapeOperation.ShapeState(_selectedShape);
            
            // Only fire event if the shape actually changed
            if (!StatesAreEqual(_shapeStateBeforeModification.Value, newState))
            {
                ShapeModified?.Invoke(this, (_selectedShapeIndex, _selectedShape, _selectedShape));
            }
            
            _shapeStateBeforeModification = null;
            RenderCanvas.Invalidate();
        }
    }

    private bool StatesAreEqual(ModifyShapeOperation.ShapeState state1, ModifyShapeOperation.ShapeState state2)
    {
        return state1.Offset == state2.Offset && 
               state1.Size == state2.Size && 
               state1.EndPoint == state2.EndPoint;
    }

    private void SelectShape(Point clickPoint)
    {
        if (Drawables == null)
        {
            return;
        }

        var drawableList = Drawables.ToList();
        
        // Check shapes in reverse order (top to bottom)
        for (int i = drawableList.Count - 1; i >= 0; i--)
        {
            var drawable = drawableList[i];
            if (IsPointInShape(clickPoint, drawable))
            {
                _selectedShape = drawable;
                _selectedShapeIndex = i;
                
                // Capture state before modification
                _shapeStateBeforeModification = new ModifyShapeOperation.ShapeState(drawable);
                
                ShowResizeHandles(drawable);
                UpdatePreviewShapeFromDrawable(drawable);
                
                // Ensure preview canvas is visible
                PreviewShapeCanvas.Visibility = Visibility.Visible;
                
                RenderCanvas.Invalidate(); // Redraw to hide selected shape from Win2D rendering
                
                // Set focus to enable keyboard events
                Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                return;
            }
        }

        // No shape selected
        DeselectShape();
    }

    private void DeselectShape()
    {
        _selectedShape = null;
        _selectedShapeIndex = -1;
        ShapeResizeOverlay.Visibility = Visibility.Collapsed;
        LineEndpointHandlesCanvas.Visibility = Visibility.Collapsed;
        ClearPreviewShape();
        RenderCanvas.Invalidate(); // Redraw to show all shapes
    }

    private IDrawable? FindShapeAtPoint(Point clickPoint)
    {
        if (Drawables == null)
        {
            return null;
        }

        var drawableList = Drawables.ToList();
        
        // Check shapes in reverse order (top to bottom)
        for (int i = drawableList.Count - 1; i >= 0; i--)
        {
            var drawable = drawableList[i];
            if (IsPointInShape(clickPoint, drawable))
            {
                return drawable;
            }
        }

        return null;
    }

    private void SelectShapeDirectly(IDrawable shape)
    {
        if (Drawables == null)
        {
            return;
        }

        var drawableList = Drawables.ToList();
        int index = drawableList.IndexOf(shape);
        
        if (index >= 0)
        {
            _selectedShape = shape;
            _selectedShapeIndex = index;
            
            // Capture state before modification
            _shapeStateBeforeModification = new ModifyShapeOperation.ShapeState(shape);
            
            ShowResizeHandles(shape);
            UpdatePreviewShapeFromDrawable(shape);
            
            // Ensure preview canvas is visible
            PreviewShapeCanvas.Visibility = Visibility.Visible;
            
            RenderCanvas.Invalidate(); // Redraw to hide selected shape from Win2D rendering
            
            // Set focus to enable keyboard events
            Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        }
    }

    private void DeleteSelectedShape()
    {
        if (_selectedShape == null || _selectedShapeIndex < 0)
        {
            return;
        }

        // Notify via event so the ViewModel can remove it from the collection
        ShapeDeleted?.Invoke(this, _selectedShapeIndex);
        
        // Clear selection state
        _selectedShape = null;
        _selectedShapeIndex = -1;
        ShapeResizeOverlay.Visibility = Visibility.Collapsed;
        LineEndpointHandlesCanvas.Visibility = Visibility.Collapsed;
        ClearPreviewShape();
        RenderCanvas.Invalidate();
    }

    private bool IsPointInShape(Point point, IDrawable drawable)
    {
        switch (drawable)
        {
            case LineDrawable line:
                return IsPointNearLine(point, line.Offset, line.EndPoint, Math.Max(10, line.StrokeWidth * 2));
            
            case ArrowDrawable arrow:
                return IsPointNearLine(point, arrow.Offset, arrow.EndPoint, Math.Max(10, arrow.StrokeWidth * 2));
            
            default:
                var bounds = GetShapeBounds(drawable);
                return bounds.Contains((float)point.X, (float)point.Y);
        }
    }

    private bool IsPointNearLine(Point point, System.Numerics.Vector2 lineStart, System.Numerics.Vector2 lineEnd, float tolerance)
    {
        // Calculate distance from point to line segment
        float px = (float)point.X;
        float py = (float)point.Y;
        
        float dx = lineEnd.X - lineStart.X;
        float dy = lineEnd.Y - lineStart.Y;
        
        // If line is actually a point
        if (dx == 0 && dy == 0)
        {
            float dist = (float)Math.Sqrt((px - lineStart.X) * (px - lineStart.X) + (py - lineStart.Y) * (py - lineStart.Y));
            return dist <= tolerance;
        }
        
        // Calculate the parameter t for the projection of point onto the line
        float t = ((px - lineStart.X) * dx + (py - lineStart.Y) * dy) / (dx * dx + dy * dy);
        
        // Clamp t to [0, 1] to stay within the line segment
        t = Math.Max(0, Math.Min(1, t));
        
        // Calculate the closest point on the line segment
        float closestX = lineStart.X + t * dx;
        float closestY = lineStart.Y + t * dy;
        
        // Calculate distance from point to closest point on line
        float distance = (float)Math.Sqrt((px - closestX) * (px - closestX) + (py - closestY) * (py - closestY));
        
        return distance <= tolerance;
    }

    private RectangleF GetShapeBounds(IDrawable drawable)
    {
        switch (drawable)
        {
            case RectangleDrawable rect:
                return new RectangleF(rect.Offset.X, rect.Offset.Y, rect.Size.Width, rect.Size.Height);

            case EllipseDrawable ellipse:
                return new RectangleF(ellipse.Offset.X, ellipse.Offset.Y, ellipse.Size.Width, ellipse.Size.Height);

            case LineDrawable line:
                {
                    float minX = Math.Min(line.Offset.X, line.EndPoint.X);
                    float minY = Math.Min(line.Offset.Y, line.EndPoint.Y);
                    float maxX = Math.Max(line.Offset.X, line.EndPoint.X);
                    float maxY = Math.Max(line.Offset.Y, line.EndPoint.Y);
                    
                    // Return tight bounds without tolerance (used for resize overlay)
                    return new RectangleF(minX, minY, maxX - minX, maxY - minY);
                }

            case ArrowDrawable arrow:
                {
                    float minX = Math.Min(arrow.Offset.X, arrow.EndPoint.X);
                    float minY = Math.Min(arrow.Offset.Y, arrow.EndPoint.Y);
                    float maxX = Math.Max(arrow.Offset.X, arrow.EndPoint.X);
                    float maxY = Math.Max(arrow.Offset.Y, arrow.EndPoint.Y);
                    
                    // Return tight bounds without tolerance (used for resize overlay)
                    return new RectangleF(minX, minY, maxX - minX, maxY - minY);
                }

            default:
                return RectangleF.Empty;
        }
    }

    private void ShowResizeHandles(IDrawable drawable)
    {
        // For lines and arrows, show endpoint handles instead of box resize overlay
        if (drawable is LineDrawable line)
        {
            ShapeResizeOverlay.Visibility = Visibility.Collapsed;
            ShowLineEndpointHandles(line.Offset.X, line.Offset.Y, line.EndPoint.X, line.EndPoint.Y);
        }
        else if (drawable is ArrowDrawable arrow)
        {
            ShapeResizeOverlay.Visibility = Visibility.Collapsed;
            ShowLineEndpointHandles(arrow.Offset.X, arrow.Offset.Y, arrow.EndPoint.X, arrow.EndPoint.Y);
        }
        else
        {
            // For rectangles and ellipses, show the box resize overlay
            LineEndpointHandlesCanvas.Visibility = Visibility.Collapsed;
            var bounds = GetShapeBounds(drawable);
            ShapeResizeOverlay.ShapeBounds = bounds;
            ShapeResizeOverlay.Visibility = Visibility.Visible;
        }
    }

    private void ShowLineEndpointHandles(float x1, float y1, float x2, float y2)
    {
        LineEndpointHandlesCanvas.Visibility = Visibility.Visible;
        
        // Position the selection visual line (dashed line)
        LineSelectionVisual.X1 = x1;
        LineSelectionVisual.Y1 = y1;
        LineSelectionVisual.X2 = x2;
        LineSelectionVisual.Y2 = y2;
        
        // Position the move handle line (make it cover the entire line for hit testing)
        LineMoveHandle.X1 = x1;
        LineMoveHandle.Y1 = y1;
        LineMoveHandle.X2 = x2;
        LineMoveHandle.Y2 = y2;
        
        // Position start handle (center the handle on the endpoint)
        Canvas.SetLeft(LineStartHandle, x1 - LineHandleRadius);
        Canvas.SetTop(LineStartHandle, y1 - LineHandleRadius);
        
        // Position end handle (center the handle on the endpoint)
        Canvas.SetLeft(LineEndHandle, x2 - LineHandleRadius);
        Canvas.SetTop(LineEndHandle, y2 - LineHandleRadius);
    }

    private void LineStartHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isDraggingLineStart = true;
        
        // Capture state for undo
        if (_selectedShape != null)
        {
            _shapeStateBeforeModification = new ModifyShapeOperation.ShapeState(_selectedShape);
        }
        
        RenderCanvas.Invalidate(); // Trigger filtering
        LineStartHandle.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void LineStartHandle_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingLineStart || _selectedShape == null)
        {
            return;
        }

        UpdateLineEndpoint(true, e);
        e.Handled = true;
    }

    private void LineStartHandle_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isDraggingLineStart)
        {
            _isDraggingLineStart = false;
            LineStartHandle.ReleasePointerCaptures();
            CompleteLineEndpointDrag();
            e.Handled = true;
        }
    }

    private void LineEndHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isDraggingLineEnd = true;
        
        // Capture state for undo
        if (_selectedShape != null)
        {
            _shapeStateBeforeModification = new ModifyShapeOperation.ShapeState(_selectedShape);
        }
        
        RenderCanvas.Invalidate(); // Trigger filtering
        LineEndHandle.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void LineEndHandle_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingLineEnd || _selectedShape == null)
        {
            return;
        }

        UpdateLineEndpoint(false, e);
        e.Handled = true;
    }

    private void LineEndHandle_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isDraggingLineEnd)
        {
            _isDraggingLineEnd = false;
            LineEndHandle.ReleasePointerCaptures();
            CompleteLineEndpointDrag();
            e.Handled = true;
        }
    }

    private void LineStartHandle_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        // Show crosshair cursor for endpoint handles
        ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Cross);
    }

    private void LineStartHandle_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingLineStart)
        {
            ProtectedCursor = null;
        }
    }

    private void LineEndHandle_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        // Show crosshair cursor for endpoint handles
        ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Cross);
    }

    private void LineEndHandle_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingLineEnd)
        {
            ProtectedCursor = null;
        }
    }

    private void UpdateLineEndpoint(bool isStartPoint, PointerRoutedEventArgs e)
    {
        if (_selectedShape == null)
        {
            return;
        }

        var currentPoint = e.GetCurrentPoint(LineEndpointHandlesCanvas).Position;
        var newPoint = new System.Numerics.Vector2((float)currentPoint.X, (float)currentPoint.Y);
        
        if (_selectedShape is LineDrawable line)
        {
            if (isStartPoint)
            {
                line.Offset = newPoint;
            }
            else
            {
                line.EndPoint = newPoint;
            }
            UpdatePreviewShapeFromDrawable(line);
            ShowLineEndpointHandles(line.Offset.X, line.Offset.Y, line.EndPoint.X, line.EndPoint.Y);
        }
        else if (_selectedShape is ArrowDrawable arrow)
        {
            if (isStartPoint)
            {
                arrow.Offset = newPoint;
            }
            else
            {
                arrow.EndPoint = newPoint;
            }
            UpdatePreviewShapeFromDrawable(arrow);
            ShowLineEndpointHandles(arrow.Offset.X, arrow.Offset.Y, arrow.EndPoint.X, arrow.EndPoint.Y);
        }
    }

    private void CompleteLineEndpointDrag()
    {
        // Fire modification event
        if (_selectedShape != null && _shapeStateBeforeModification.HasValue)
        {
            var newState = new ModifyShapeOperation.ShapeState(_selectedShape);
            if (!StatesAreEqual(_shapeStateBeforeModification.Value, newState))
            {
                ShapeModified?.Invoke(this, (_selectedShapeIndex, _selectedShape, _selectedShape));
            }
            _shapeStateBeforeModification = null;
            RenderCanvas.Invalidate();
        }
    }

    private void LineMoveHandle_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        // Change cursor to move cursor when hovering over the line
        ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeAll);
    }

    private void LineMoveHandle_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        // Reset cursor
        if (!_isDraggingLineMove)
        {
            ProtectedCursor = null;
        }
    }

    private void LineMoveHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isDraggingLineMove = true;
        _lineMoveStartPoint = e.GetCurrentPoint(LineEndpointHandlesCanvas).Position;
        
        // Capture initial positions
        if (_selectedShape is LineDrawable line)
        {
            _lineMoveInitialOffset = line.Offset;
            _lineMoveInitialEndPoint = line.EndPoint;
        }
        else if (_selectedShape is ArrowDrawable arrow)
        {
            _lineMoveInitialOffset = arrow.Offset;
            _lineMoveInitialEndPoint = arrow.EndPoint;
        }
        
        // Capture state for undo
        if (_selectedShape != null)
        {
            _shapeStateBeforeModification = new ModifyShapeOperation.ShapeState(_selectedShape);
        }
        
        RenderCanvas.Invalidate(); // Trigger filtering
        LineMoveHandle.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void LineMoveHandle_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingLineMove || _selectedShape == null)
        {
            return;
        }

        var currentPoint = e.GetCurrentPoint(LineEndpointHandlesCanvas).Position;
        var deltaX = (float)(currentPoint.X - _lineMoveStartPoint.X);
        var deltaY = (float)(currentPoint.Y - _lineMoveStartPoint.Y);
        
        // Move both offset and endpoint by the same delta
        if (_selectedShape is LineDrawable line)
        {
            line.Offset = new System.Numerics.Vector2(_lineMoveInitialOffset.X + deltaX, _lineMoveInitialOffset.Y + deltaY);
            line.EndPoint = new System.Numerics.Vector2(_lineMoveInitialEndPoint.X + deltaX, _lineMoveInitialEndPoint.Y + deltaY);
            UpdatePreviewShapeFromDrawable(line);
            ShowLineEndpointHandles(line.Offset.X, line.Offset.Y, line.EndPoint.X, line.EndPoint.Y);
        }
        else if (_selectedShape is ArrowDrawable arrow)
        {
            arrow.Offset = new System.Numerics.Vector2(_lineMoveInitialOffset.X + deltaX, _lineMoveInitialOffset.Y + deltaY);
            arrow.EndPoint = new System.Numerics.Vector2(_lineMoveInitialEndPoint.X + deltaX, _lineMoveInitialEndPoint.Y + deltaY);
            UpdatePreviewShapeFromDrawable(arrow);
            ShowLineEndpointHandles(arrow.Offset.X, arrow.Offset.Y, arrow.EndPoint.X, arrow.EndPoint.Y);
        }
        
        e.Handled = true;
    }

    private void LineMoveHandle_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isDraggingLineMove)
        {
            _isDraggingLineMove = false;
            LineMoveHandle.ReleasePointerCaptures();
            
            // Reset cursor
            ProtectedCursor = null;
            
            // Fire modification event
            CompleteLineEndpointDrag();
            
            e.Handled = true;
        }
    }

    private void UpdateSelectedShapeBounds(RectangleF newBounds)
    {
        if (_selectedShape == null)
        {
            return;
        }

        switch (_selectedShape)
        {
            case RectangleDrawable rect:
                rect.Offset = new System.Numerics.Vector2(newBounds.X, newBounds.Y);
                rect.Size = new Size((int)Math.Ceiling(newBounds.Width), (int)Math.Ceiling(newBounds.Height));
                break;

            case EllipseDrawable ellipse:
                ellipse.Offset = new System.Numerics.Vector2(newBounds.X, newBounds.Y);
                ellipse.Size = new Size((int)Math.Ceiling(newBounds.Width), (int)Math.Ceiling(newBounds.Height));
                break;

            case LineDrawable line:
                {
                    // Preserve line direction when resizing
                    bool startsFromLeft = line.Offset.X <= line.EndPoint.X;
                    bool startsFromTop = line.Offset.Y <= line.EndPoint.Y;
                    
                    // Apply new bounds while preserving direction
                    if (startsFromLeft && startsFromTop)
                    {
                        line.Offset = new System.Numerics.Vector2(newBounds.X, newBounds.Y);
                        line.EndPoint = new System.Numerics.Vector2(newBounds.X + newBounds.Width, newBounds.Y + newBounds.Height);
                    }
                    else if (!startsFromLeft && startsFromTop)
                    {
                        line.Offset = new System.Numerics.Vector2(newBounds.X + newBounds.Width, newBounds.Y);
                        line.EndPoint = new System.Numerics.Vector2(newBounds.X, newBounds.Y + newBounds.Height);
                    }
                    else if (startsFromLeft && !startsFromTop)
                    {
                        line.Offset = new System.Numerics.Vector2(newBounds.X, newBounds.Y + newBounds.Height);
                        line.EndPoint = new System.Numerics.Vector2(newBounds.X + newBounds.Width, newBounds.Y);
                    }
                    else
                    {
                        line.Offset = new System.Numerics.Vector2(newBounds.X + newBounds.Width, newBounds.Y + newBounds.Height);
                        line.EndPoint = new System.Numerics.Vector2(newBounds.X, newBounds.Y);
                    }
                }
                break;

            case ArrowDrawable arrow:
                {
                    // Preserve arrow direction when resizing
                    bool startsFromLeft = arrow.Offset.X <= arrow.EndPoint.X;
                    bool startsFromTop = arrow.Offset.Y <= arrow.EndPoint.Y;
                    
                    // Apply new bounds while preserving direction
                    if (startsFromLeft && startsFromTop)
                    {
                        arrow.Offset = new System.Numerics.Vector2(newBounds.X, newBounds.Y);
                        arrow.EndPoint = new System.Numerics.Vector2(newBounds.X + newBounds.Width, newBounds.Y + newBounds.Height);
                    }
                    else if (!startsFromLeft && startsFromTop)
                    {
                        arrow.Offset = new System.Numerics.Vector2(newBounds.X + newBounds.Width, newBounds.Y);
                        arrow.EndPoint = new System.Numerics.Vector2(newBounds.X, newBounds.Y + newBounds.Height);
                    }
                    else if (startsFromLeft && !startsFromTop)
                    {
                        arrow.Offset = new System.Numerics.Vector2(newBounds.X, newBounds.Y + newBounds.Height);
                        arrow.EndPoint = new System.Numerics.Vector2(newBounds.X + newBounds.Width, newBounds.Y);
                    }
                    else
                    {
                        arrow.Offset = new System.Numerics.Vector2(newBounds.X + newBounds.Width, newBounds.Y + newBounds.Height);
                        arrow.EndPoint = new System.Numerics.Vector2(newBounds.X, newBounds.Y);
                    }
                }
                break;
        }
    }

    private void UpdateSelectedShapeStrokeColor(Color color)
    {
        if (_selectedShape == null)
        {
            return;
        }

        switch (_selectedShape)
        {
            case RectangleDrawable rect:
                rect.StrokeColor = color;
                break;
            case EllipseDrawable ellipse:
                ellipse.StrokeColor = color;
                break;
            case LineDrawable line:
                line.StrokeColor = color;
                break;
            case ArrowDrawable arrow:
                arrow.StrokeColor = color;
                break;
        }

        // Update preview to reflect the new color
        UpdatePreviewShapeFromDrawable(_selectedShape);
        RenderCanvas.Invalidate();
    }

    private void UpdateSelectedShapeFillColor(Color color)
    {
        if (_selectedShape == null)
        {
            return;
        }

        switch (_selectedShape)
        {
            case RectangleDrawable rect:
                rect.FillColor = color;
                break;
            case EllipseDrawable ellipse:
                ellipse.FillColor = color;
                break;
            // Lines and arrows don't have fill color
        }

        // Update preview to reflect the new color
        UpdatePreviewShapeFromDrawable(_selectedShape);
        RenderCanvas.Invalidate();
    }

    private void UpdateSelectedShapeStrokeWidth(int width)
    {
        if (_selectedShape == null)
        {
            return;
        }

        switch (_selectedShape)
        {
            case RectangleDrawable rect:
                rect.StrokeWidth = width;
                break;
            case EllipseDrawable ellipse:
                ellipse.StrokeWidth = width;
                break;
            case LineDrawable line:
                line.StrokeWidth = width;
                break;
            case ArrowDrawable arrow:
                arrow.StrokeWidth = width;
                break;
        }

        // Update preview to reflect the new width
        UpdatePreviewShapeFromDrawable(_selectedShape);
        RenderCanvas.Invalidate();
    }

    private void UpdatePreviewShapeFromDrawable(IDrawable drawable)
    {
        switch (drawable)
        {
            case RectangleDrawable rect:
                {
                    // Create rectangle on first use
                    if (_previewRectangle == null)
                    {
                        _previewRectangle = new Microsoft.UI.Xaml.Shapes.Rectangle();
                        PreviewShapeCanvas.Children.Add(_previewRectangle);
                    }

                    // Update properties from drawable
                    _previewRectangle.Width = rect.Size.Width;
                    _previewRectangle.Height = rect.Size.Height;
                    
                    // Update stroke brush
                    if (_previewStrokeBrush == null)
                    {
                        _previewStrokeBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush();
                    }
                    _previewStrokeBrush.Color = WinUIColor.FromArgb(
                        rect.StrokeColor.A,
                        rect.StrokeColor.R,
                        rect.StrokeColor.G,
                        rect.StrokeColor.B);
                    _previewRectangle.Stroke = _previewStrokeBrush;
                    
                    // Update fill brush
                    if (rect.FillColor.A > 0)
                    {
                        if (_previewFillBrush == null)
                        {
                            _previewFillBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush();
                        }
                        _previewFillBrush.Color = WinUIColor.FromArgb(
                            rect.FillColor.A,
                            rect.FillColor.R,
                            rect.FillColor.G,
                            rect.FillColor.B);
                        _previewRectangle.Fill = _previewFillBrush;
                    }
                    else
                    {
                        _previewRectangle.Fill = null;
                    }
                    
                    _previewRectangle.StrokeThickness = rect.StrokeWidth;
                    Canvas.SetLeft(_previewRectangle, rect.Offset.X);
                    Canvas.SetTop(_previewRectangle, rect.Offset.Y);
                    _previewRectangle.Visibility = Visibility.Visible;

                    // Hide other shapes
                    HideOtherPreviewShapes(ShapeType.Rectangle);
                }
                break;

            case EllipseDrawable ellipse:
                {
                    // Create ellipse on first use
                    if (_previewEllipse == null)
                    {
                        _previewEllipse = new Microsoft.UI.Xaml.Shapes.Ellipse();
                        PreviewShapeCanvas.Children.Add(_previewEllipse);
                    }

                    // Update properties from drawable
                    _previewEllipse.Width = ellipse.Size.Width;
                    _previewEllipse.Height = ellipse.Size.Height;
                    
                    // Update stroke brush
                    if (_previewStrokeBrush == null)
                    {
                        _previewStrokeBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush();
                    }
                    _previewStrokeBrush.Color = WinUIColor.FromArgb(
                        ellipse.StrokeColor.A,
                        ellipse.StrokeColor.R,
                        ellipse.StrokeColor.G,
                        ellipse.StrokeColor.B);
                    _previewEllipse.Stroke = _previewStrokeBrush;
                    
                    // Update fill brush
                    if (ellipse.FillColor.A > 0)
                    {
                        if (_previewFillBrush == null)
                        {
                            _previewFillBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush();
                        }
                        _previewFillBrush.Color = WinUIColor.FromArgb(
                            ellipse.FillColor.A,
                            ellipse.FillColor.R,
                            ellipse.FillColor.G,
                            ellipse.FillColor.B);
                        _previewEllipse.Fill = _previewFillBrush;
                    }
                    else
                    {
                        _previewEllipse.Fill = null;
                    }
                    
                    _previewEllipse.StrokeThickness = ellipse.StrokeWidth;
                    Canvas.SetLeft(_previewEllipse, ellipse.Offset.X);
                    Canvas.SetTop(_previewEllipse, ellipse.Offset.Y);
                    _previewEllipse.Visibility = Visibility.Visible;

                    // Hide other shapes
                    HideOtherPreviewShapes(ShapeType.Ellipse);
                }
                break;

            case LineDrawable line:
                {
                    // Create line on first use
                    if (_previewLine == null)
                    {
                        _previewLine = new Microsoft.UI.Xaml.Shapes.Line();
                        PreviewShapeCanvas.Children.Add(_previewLine);
                    }

                    // Update line properties from drawable
                    _previewLine.X1 = line.Offset.X;
                    _previewLine.Y1 = line.Offset.Y;
                    _previewLine.X2 = line.EndPoint.X;
                    _previewLine.Y2 = line.EndPoint.Y;
                    
                    // Update stroke brush
                    if (_previewStrokeBrush == null)
                    {
                        _previewStrokeBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush();
                    }
                    _previewStrokeBrush.Color = WinUIColor.FromArgb(
                        line.StrokeColor.A,
                        line.StrokeColor.R,
                        line.StrokeColor.G,
                        line.StrokeColor.B);
                    _previewLine.Stroke = _previewStrokeBrush;
                    _previewLine.StrokeThickness = line.StrokeWidth;
                    _previewLine.Visibility = Visibility.Visible;

                    // Hide arrow head if present
                    if (_previewArrowHead != null)
                    {
                        _previewArrowHead.Visibility = Visibility.Collapsed;
                    }

                    // Hide other shapes
                    HideOtherPreviewShapes(ShapeType.Line);
                }
                break;

            case ArrowDrawable arrow:
                {
                    // Create line on first use
                    if (_previewLine == null)
                    {
                        _previewLine = new Microsoft.UI.Xaml.Shapes.Line();
                        PreviewShapeCanvas.Children.Add(_previewLine);
                    }

                    // Update line properties from drawable
                    _previewLine.X1 = arrow.Offset.X;
                    _previewLine.Y1 = arrow.Offset.Y;
                    _previewLine.X2 = arrow.EndPoint.X;
                    _previewLine.Y2 = arrow.EndPoint.Y;
                    
                    // Update stroke brush
                    if (_previewStrokeBrush == null)
                    {
                        _previewStrokeBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush();
                    }
                    _previewStrokeBrush.Color = WinUIColor.FromArgb(
                        arrow.StrokeColor.A,
                        arrow.StrokeColor.R,
                        arrow.StrokeColor.G,
                        arrow.StrokeColor.B);
                    _previewLine.Stroke = _previewStrokeBrush;
                    _previewLine.StrokeThickness = arrow.StrokeWidth;
                    _previewLine.Visibility = Visibility.Visible;

                    // Create arrow head on first use
                    if (_previewArrowHead == null)
                    {
                        _previewArrowHead = new Microsoft.UI.Xaml.Shapes.Polyline
                        {
                            Points = new Microsoft.UI.Xaml.Media.PointCollection()
                        };
                        PreviewShapeCanvas.Children.Add(_previewArrowHead);
                    }

                    // Calculate arrow head
                    double angle = Math.Atan2(arrow.EndPoint.Y - arrow.Offset.Y, arrow.EndPoint.X - arrow.Offset.X);
                    double arrowLength = 10 + arrow.StrokeWidth;
                    double arrowAngle = Math.PI / 6;

                    // Update arrow head points
                    _previewArrowHead.Points.Clear();
                    _previewArrowHead.Points.Add(new global::Windows.Foundation.Point(
                        arrow.EndPoint.X - arrowLength * Math.Cos(angle - arrowAngle),
                        arrow.EndPoint.Y - arrowLength * Math.Sin(angle - arrowAngle)));
                    _previewArrowHead.Points.Add(new global::Windows.Foundation.Point(arrow.EndPoint.X, arrow.EndPoint.Y));
                    _previewArrowHead.Points.Add(new global::Windows.Foundation.Point(
                        arrow.EndPoint.X - arrowLength * Math.Cos(angle + arrowAngle),
                        arrow.EndPoint.Y - arrowLength * Math.Sin(angle + arrowAngle)));

                    _previewArrowHead.Stroke = _previewStrokeBrush;
                    _previewArrowHead.StrokeThickness = arrow.StrokeWidth;
                    _previewArrowHead.StrokeLineJoin = Microsoft.UI.Xaml.Media.PenLineJoin.Miter;
                    _previewArrowHead.Visibility = Visibility.Visible;

                    // Hide other shapes
                    HideOtherPreviewShapes(ShapeType.Arrow);
                }
                break;
        }

        PreviewShapeCanvas.Visibility = Visibility.Visible;
    }
    #endregion
}
