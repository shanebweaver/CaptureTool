using CaptureTool.Application.Abstractions.Features.ImageEdit.Rendering;
using CaptureTool.Domain.Edit;
using CaptureTool.Domain.Edit.Drawable;
using CaptureTool.Domain.Edit.Operations;
using CaptureTool.Infrastructure.Edit.Windows;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Collections.Specialized;
using System.Drawing;
using System.Numerics;
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
        new PropertyMetadata(null, OnDrawablesPropertyChanged));

    private static void OnDrawablesPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ImageCanvas control)
        {
            return;
        }

        control.SetObservableDrawables(e.NewValue as INotifyCollectionChanged);
        control.ReconcileSelectedShape();
        control.InvalidateCanvas();
    }

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

    public static readonly DependencyProperty IsTextModeEnabledProperty = DependencyProperty.Register(
        nameof(IsTextModeEnabled),
        typeof(bool),
        typeof(ImageCanvas),
        new PropertyMetadata(false, OnIsTextModeEnabledPropertyChanged));

    private static void OnIsTextModeEnabledPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageCanvas control && e.NewValue is bool isEnabled && !isEnabled)
        {
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

    public static readonly DependencyProperty TextForegroundColorProperty = DependencyProperty.Register(
       nameof(TextForegroundColor),
       typeof(Color),
       typeof(ImageCanvas),
       new PropertyMetadata(Color.Black, OnTextForegroundColorChanged));

    private static void OnTextForegroundColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageCanvas control && e.NewValue is Color color)
        {
            control.UpdateSelectedTextStyle(text => text.Color = color);
        }
    }

    public static readonly DependencyProperty TextBackgroundColorProperty = DependencyProperty.Register(
       nameof(TextBackgroundColor),
       typeof(Color),
       typeof(ImageCanvas),
       new PropertyMetadata(Color.Transparent, OnTextBackgroundColorChanged));

    private static void OnTextBackgroundColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageCanvas control && e.NewValue is Color color)
        {
            control.UpdateSelectedTextStyle(text => text.BackgroundColor = color);
        }
    }

    public static readonly DependencyProperty TextFontFamilyProperty = DependencyProperty.Register(
       nameof(TextFontFamily),
       typeof(string),
       typeof(ImageCanvas),
       new PropertyMetadata(TextDrawable.DefaultFontFamily, OnTextFontFamilyChanged));

    private static void OnTextFontFamilyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageCanvas control && e.NewValue is string fontFamily)
        {
            control.UpdateSelectedTextStyle(text => text.FontFamily = fontFamily);
        }
    }

    public static readonly DependencyProperty TextFontSizeProperty = DependencyProperty.Register(
       nameof(TextFontSize),
       typeof(int),
       typeof(ImageCanvas),
       new PropertyMetadata((int)TextDrawable.DefaultFontSize, OnTextFontSizeChanged));

    private static void OnTextFontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageCanvas control && e.NewValue is int fontSize)
        {
            control.UpdateSelectedTextStyle(text => text.FontSize = fontSize);
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

    public bool IsTextModeEnabled
    {
        get => Get<bool>(IsTextModeEnabledProperty);
        set => Set(IsTextModeEnabledProperty, value);
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

    public Color TextForegroundColor
    {
        get => Get<Color>(TextForegroundColorProperty);
        set => Set(TextForegroundColorProperty, value);
    }

    public Color TextBackgroundColor
    {
        get => Get<Color>(TextBackgroundColorProperty);
        set => Set(TextBackgroundColorProperty, value);
    }

    public string TextFontFamily
    {
        get => Get<string>(TextFontFamilyProperty) ?? TextDrawable.DefaultFontFamily;
        set => Set(TextFontFamilyProperty, value);
    }

    public int TextFontSize
    {
        get => Get<int>(TextFontSizeProperty);
        set => Set(TextFontSizeProperty, value);
    }

    public event EventHandler<Rectangle>? InteractionComplete;
    public event EventHandler<Rectangle>? CropRectChanged;
    public event EventHandler<(System.Numerics.Vector2 Start, System.Numerics.Vector2 End)>? ShapeDrawn;
    public event EventHandler<(System.Numerics.Vector2 Start, System.Numerics.Vector2 End)>? TextBoxDrawn;
    public event EventHandler<(double ZoomFactor, ZoomUpdateSource Source)>? ZoomFactorChanged;
    public event EventHandler<int>? ShapeDeleted;
    public event EventHandler<(int ShapeIndex, ModifyShapeOperation.ShapeState OldState, ModifyShapeOperation.ShapeState NewState)>? ShapeModified;
    public event EventHandler<TextDrawable>? TextDrawableSelected;
    public event EventHandler<Point>? ImageContextMenuRequested;
    public event EventHandler<Point>? ShapeContextMenuRequested;

    private readonly Lock _zoomUpdateLock = new Lock();

    private const int LineHandleRadius = 6; // Half of handle diameter (12px total)
    private const double MinimumHandleScale = 0.1;
    private const double MaximumHandleScale = 10;
    private const double CanvasContainerBaseMargin = 24;
    private const double LineSelectionStrokeThickness = 1;
    private const double LineMoveHandleStrokeThickness = 20;

    private bool _isPointerDown;
    private Point _lastPointerPosition;
    private Point? _shapeStartPoint;

    // Shape selection state
    private IDrawable? _selectedShape;
    private int _selectedShapeIndex = -1;
    private ModifyShapeOperation.ShapeState? _shapeStateBeforeModification;
    private ModifyShapeOperation.ShapeState? _styleInteractionStateBeforeModification;
    private int _styleInteractionShapeIndex = -1;
    private int _styleInteractionDepth;
    private INotifyCollectionChanged? _observableDrawables;

    // Track if we're in a potential selection scenario
    private IDrawable? _shapeUnderPointer;
    private Point? _pointerPressPosition;

    // Cached preview elements for performance
    private Microsoft.UI.Xaml.Shapes.Rectangle? _previewRectangle;
    private Microsoft.UI.Xaml.Shapes.Rectangle? _previewRectangleFill;
    private Microsoft.UI.Xaml.Shapes.Ellipse? _previewEllipse;
    private Microsoft.UI.Xaml.Shapes.Ellipse? _previewEllipseFill;
    private Microsoft.UI.Xaml.Shapes.Line? _previewLine;
    private Microsoft.UI.Xaml.Shapes.Polyline? _previewArrowHead;
    private Microsoft.UI.Xaml.Media.SolidColorBrush? _previewStrokeBrush;
    private Microsoft.UI.Xaml.Media.SolidColorBrush? _previewFillBrush;
    private Color _cachedStrokeColor;
    private Color _cachedFillColor;
    private TextBox? _selectedTextEditor;
    private bool _isUpdatingTextEditor;

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
        LineStartHandle.RenderTransformOrigin = new Point(0.5, 0.5);
        LineEndHandle.RenderTransformOrigin = new Point(0.5, 0.5);
        Loaded += ImageCanvas_Loaded;
        Unloaded += ImageCanvas_Unloaded;
        KeyDown += ImageCanvas_KeyDown;
    }

    private void ImageCanvas_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if ((!IsShapesModeEnabled && !IsTextModeEnabled) || _selectedShape == null)
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

        SetObservableDrawables(Drawables as INotifyCollectionChanged);
    }

    private void ImageCanvas_Unloaded(object sender, RoutedEventArgs e)
    {
        // Clean up event subscription to prevent memory leak
        if (CanvasScrollView != null)
        {
            CanvasScrollView.ViewChanged -= CanvasScrollView_ViewChanged;
        }

        SetObservableDrawables(null);
    }

    private void SetObservableDrawables(INotifyCollectionChanged? drawables)
    {
        if (ReferenceEquals(_observableDrawables, drawables))
        {
            return;
        }

        if (_observableDrawables != null)
        {
            _observableDrawables.CollectionChanged -= Drawables_CollectionChanged;
        }

        _observableDrawables = drawables;

        if (_observableDrawables != null)
        {
            _observableDrawables.CollectionChanged += Drawables_CollectionChanged;
        }
    }

    private void Drawables_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ReconcileSelectedShape();
        RenderCanvas.Invalidate();
    }

    private void ReconcileSelectedShape()
    {
        if (_selectedShape == null || Drawables == null)
        {
            return;
        }

        var drawableList = Drawables.ToList();
        int selectedShapeIndex = drawableList.IndexOf(_selectedShape);

        if (selectedShapeIndex < 0)
        {
            DeselectShape();
            return;
        }

        _selectedShapeIndex = selectedShapeIndex;
        ShowResizeHandles(_selectedShape);
        UpdatePreviewShapeFromDrawable(_selectedShape);

        if (_selectedShape is TextDrawable text)
        {
            UpdateSelectedTextEditor(text);
            TextDrawableSelected?.Invoke(this, text);
        }
    }

    private void CanvasScrollView_ViewChanged(ScrollView? sender, object args)
    {
        if (sender == null)
        {
            return;
        }

        UpdateOverlayHandleScale();

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
            UpdateOverlayHandleScale();

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

            double availableWidth = Math.Max(1, containerWidth - (CanvasContainerBaseMargin * 2));
            double availableHeight = Math.Max(1, containerHeight - (CanvasContainerBaseMargin * 2));

            double scaleX = availableWidth / canvasWidth;
            double scaleY = availableHeight / canvasHeight;

            // Choose the smaller scale to ensure the image fits within the container
            double targetZoomFactor = Math.Min(1, Math.Min(scaleX, scaleY));

            CanvasScrollView.ZoomTo(
                (float)targetZoomFactor,
                null,
                new(ScrollingAnimationMode.Auto)
            );
            UpdateOverlayHandleScale(targetZoomFactor);

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
            UpdateOverlayHandleScale(zoomLevel);
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
        ReconcileSelectedShape();
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
            var rect = GetRenderCropRect();
            ImageCanvasRenderOptions options = new(Orientation, CanvasSize, rect);

            // Filter out the selected shape ONLY when it's being actively manipulated
            // This prevents double rendering during drag operations
            var drawablesToRender = Drawables;
            if (IsShapeSelected())
            {
                drawablesToRender = Drawables.Where((d, i) => i != _selectedShapeIndex);
            }

            Win2DImageCanvasRenderer.Render([.. drawablesToRender], options, args.DrawingSession);
        }
    }

    private Rectangle GetRenderCropRect()
    {
        return !IsCropModeEnabled
            ? CropRect
            : IsTurned()
                ? new Rectangle(0, 0, CanvasSize.Height, CanvasSize.Width)
                : new Rectangle(0, 0, CanvasSize.Width, CanvasSize.Height);
    }

    private bool IsShapeSelected()
    {
        return _selectedShape != null;
    }

    private Vector2 DisplayPointToCanvasPoint(Point point)
    {
        var cropRect = GetRenderCropRect();
        var orientedPoint = new Vector2((float)(point.X + cropRect.X), (float)(point.Y + cropRect.Y));
        Matrix3x2 transform = ImageRenderTransformHelper.CalculateRenderTransform(CanvasSize, Orientation);

        return Matrix3x2.Invert(transform, out Matrix3x2 inverseTransform)
            ? Vector2.Transform(orientedPoint, inverseTransform)
            : orientedPoint;
    }

    private Point CanvasPointToDisplayPoint(Vector2 point)
    {
        var cropRect = GetRenderCropRect();
        Matrix3x2 transform = ImageRenderTransformHelper.CalculateRenderTransform(CanvasSize, Orientation);
        Vector2 orientedPoint = Vector2.Transform(point, transform);

        return new Point(orientedPoint.X - cropRect.X, orientedPoint.Y - cropRect.Y);
    }

    private RectangleF CanvasBoundsToDisplayBounds(RectangleF bounds)
    {
        Point topLeft = CanvasPointToDisplayPoint(new Vector2(bounds.Left, bounds.Top));
        Point topRight = CanvasPointToDisplayPoint(new Vector2(bounds.Right, bounds.Top));
        Point bottomLeft = CanvasPointToDisplayPoint(new Vector2(bounds.Left, bounds.Bottom));
        Point bottomRight = CanvasPointToDisplayPoint(new Vector2(bounds.Right, bounds.Bottom));

        double minX = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X));
        double minY = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y));
        double maxX = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X));
        double maxY = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y));

        return new RectangleF((float)minX, (float)minY, (float)(maxX - minX), (float)(maxY - minY));
    }

    private RectangleF DisplayBoundsToCanvasBounds(RectangleF bounds)
    {
        Vector2 topLeft = DisplayPointToCanvasPoint(new Point(bounds.Left, bounds.Top));
        Vector2 topRight = DisplayPointToCanvasPoint(new Point(bounds.Right, bounds.Top));
        Vector2 bottomLeft = DisplayPointToCanvasPoint(new Point(bounds.Left, bounds.Bottom));
        Vector2 bottomRight = DisplayPointToCanvasPoint(new Point(bounds.Right, bounds.Bottom));

        float minX = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X));
        float minY = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y));
        float maxX = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X));
        float maxY = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y));

        return new RectangleF(minX, minY, maxX - minX, maxY - minY);
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
        if (TryHandleContextMenuRequest(e))
        {
            return;
        }

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
        else if (IsTextModeEnabled)
        {
            var point = e.GetCurrentPoint(RenderCanvas);
            if (point.Properties.IsLeftButtonPressed)
            {
                _pointerPressPosition = point.Position;

                if (_selectedShape is TextDrawable selectedText && IsPointInShape(point.Position, selectedText))
                {
                    e.Handled = true;
                    return;
                }

                _shapeUnderPointer = FindTextAtPoint(point.Position);
                if (_shapeUnderPointer != null)
                {
                    SelectShape(_shapeUnderPointer);
                    e.Handled = true;
                    return;
                }

                if (_selectedShape != null)
                {
                    DeselectShape();
                }

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

    private bool TryHandleContextMenuRequest(PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(RenderCanvas);
        if (!point.Properties.IsRightButtonPressed)
        {
            return false;
        }

        var menuPosition = e.GetCurrentPoint(this).Position;

        if (IsShapesModeEnabled)
        {
            var shape = FindShapeAtPoint(point.Position);
            if (shape != null)
            {
                SelectShape(shape);
                ShapeContextMenuRequested?.Invoke(this, menuPosition);
                e.Handled = true;
                return true;
            }
        }
        else if (IsTextModeEnabled)
        {
            var text = FindTextAtPoint(point.Position);
            if (text != null)
            {
                SelectShape(text);
                ShapeContextMenuRequested?.Invoke(this, menuPosition);
                e.Handled = true;
                return true;
            }
        }

        DeselectShape();
        ImageContextMenuRequested?.Invoke(this, menuPosition);
        e.Handled = true;
        return true;
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
            else if (IsTextModeEnabled && _pointerPressPosition.HasValue)
            {
                var currentPoint = e.GetCurrentPoint(RenderCanvas).Position;
                const double dragThreshold = 3.0;
                double distance = Math.Sqrt(
                    Math.Pow(currentPoint.X - _pointerPressPosition.Value.X, 2) +
                    Math.Pow(currentPoint.Y - _pointerPressPosition.Value.Y, 2));

                if (_shapeUnderPointer == null && distance > dragThreshold)
                {
                    _shapeStartPoint ??= _pointerPressPosition.Value;
                    UpdateTextBoxPreview(_shapeStartPoint.Value, currentPoint);
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
                var start = DisplayPointToCanvasPoint(_shapeStartPoint.Value);
                var end = DisplayPointToCanvasPoint(endPoint);

                ShapeDrawn?.Invoke(this, (start, end));

                DispatcherQueue.TryEnqueue(() =>
                {
                    SelectShape(endPoint);
                });

                _shapeStartPoint = null;
                e.Handled = true;
            }
            else if (_shapeUnderPointer != null && _pointerPressPosition.HasValue)
            {
                // User clicked without dragging - select the shape
                SelectShape(_shapeUnderPointer);
                e.Handled = true;
            }

            // Clean up tracking variables
            _shapeUnderPointer = null;
            _pointerPressPosition = null;
        }
        else if (IsTextModeEnabled && _pointerPressPosition.HasValue)
        {
            if (_shapeStartPoint.HasValue)
            {
                var endPoint = e.GetCurrentPoint(RenderCanvas).Position;
                ClearPreviewShape();

                var start = DisplayPointToCanvasPoint(_shapeStartPoint.Value);
                var end = DisplayPointToCanvasPoint(endPoint);

                TextBoxDrawn?.Invoke(this, (start, end));

                DispatcherQueue.TryEnqueue(() =>
                {
                    SelectShape(endPoint);
                });

                _shapeStartPoint = null;
                e.Handled = true;
            }

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
        RootContainer.ReleasePointerCaptures();
    }

    private void RootContainer_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isPointerDown = false;
        _shapeStartPoint = null;
        _shapeUnderPointer = null;
        _pointerPressPosition = null;
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

                    UpdateRectanglePreview(x, y, width, height, ShapeStrokeWidth, ShapeFillColor.A > 0);

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

                    UpdateEllipsePreview(x, y, width, height, ShapeStrokeWidth, ShapeFillColor.A > 0);

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

    private void UpdateTextBoxPreview(Point startPoint, Point endPoint)
    {
        float x = (float)Math.Min(startPoint.X, endPoint.X);
        float y = (float)Math.Min(startPoint.Y, endPoint.Y);
        float width = (float)Math.Abs(endPoint.X - startPoint.X);
        float height = (float)Math.Abs(endPoint.Y - startPoint.Y);

        if (width < 2 || height < 2)
        {
            PreviewShapeCanvas.Visibility = Visibility.Collapsed;
            return;
        }

        _previewStrokeBrush ??= new Microsoft.UI.Xaml.Media.SolidColorBrush();
        _previewStrokeBrush.Color = WinUIColor.FromArgb(255, 59, 130, 246);

        _previewFillBrush ??= new Microsoft.UI.Xaml.Media.SolidColorBrush();
        _previewFillBrush.Color = WinUIColor.FromArgb(
            TextBackgroundColor.A,
            TextBackgroundColor.R,
            TextBackgroundColor.G,
            TextBackgroundColor.B);

        UpdateRectanglePreview(x, y, width, height, 1, TextBackgroundColor.A > 0, 4);
        HideOtherPreviewShapes(ShapeType.Rectangle);
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
        if (activeType != ShapeType.Rectangle && _previewRectangleFill != null)
        {
            _previewRectangleFill.Visibility = Visibility.Collapsed;
        }
        if (activeType != ShapeType.Ellipse && _previewEllipse != null)
        {
            _previewEllipse.Visibility = Visibility.Collapsed;
        }
        if (activeType != ShapeType.Ellipse && _previewEllipseFill != null)
        {
            _previewEllipseFill.Visibility = Visibility.Collapsed;
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
        _previewRectangle?.Visibility = Visibility.Collapsed;
        _previewRectangleFill?.Visibility = Visibility.Collapsed;
        _previewEllipse?.Visibility = Visibility.Collapsed;
        _previewEllipseFill?.Visibility = Visibility.Collapsed;
        _previewLine?.Visibility = Visibility.Collapsed;
        _previewArrowHead?.Visibility = Visibility.Collapsed;
        PreviewShapeCanvas.Visibility = Visibility.Collapsed;
    }

    private void UpdateRectanglePreview(float x, float y, float width, float height, int strokeWidth, bool hasFill, double cornerRadius = 0)
    {
        EnsureRectanglePreview();

        float normalizedStrokeWidth = Math.Max(0, strokeWidth);
        double strokeWidthValue = Math.Max(0, width);
        double strokeHeightValue = Math.Max(0, height);

        _previewRectangle!.Width = strokeWidthValue;
        _previewRectangle.Height = strokeHeightValue;
        _previewRectangle.Stroke = _previewStrokeBrush;
        _previewRectangle.Fill = null;
        _previewRectangle.StrokeThickness = normalizedStrokeWidth;
        _previewRectangle.RadiusX = cornerRadius;
        _previewRectangle.RadiusY = cornerRadius;
        Canvas.SetLeft(_previewRectangle, x);
        Canvas.SetTop(_previewRectangle, y);
        _previewRectangle.Visibility = strokeWidthValue > 0 && strokeHeightValue > 0 && normalizedStrokeWidth > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        double fillWidth = Math.Max(0, width - normalizedStrokeWidth * 2);
        double fillHeight = Math.Max(0, height - normalizedStrokeWidth * 2);
        _previewRectangleFill!.Width = fillWidth;
        _previewRectangleFill.Height = fillHeight;
        _previewRectangleFill.Stroke = null;
        _previewRectangleFill.Fill = hasFill ? _previewFillBrush : null;
        _previewRectangleFill.RadiusX = Math.Max(0, cornerRadius - normalizedStrokeWidth);
        _previewRectangleFill.RadiusY = Math.Max(0, cornerRadius - normalizedStrokeWidth);
        Canvas.SetLeft(_previewRectangleFill, x + normalizedStrokeWidth);
        Canvas.SetTop(_previewRectangleFill, y + normalizedStrokeWidth);
        _previewRectangleFill.Visibility = hasFill && fillWidth > 0 && fillHeight > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void UpdateEllipsePreview(float x, float y, float width, float height, int strokeWidth, bool hasFill)
    {
        EnsureEllipsePreview();

        float normalizedStrokeWidth = Math.Max(0, strokeWidth);
        double strokeWidthValue = Math.Max(0, width);
        double strokeHeightValue = Math.Max(0, height);

        _previewEllipse!.Width = strokeWidthValue;
        _previewEllipse.Height = strokeHeightValue;
        _previewEllipse.Stroke = _previewStrokeBrush;
        _previewEllipse.Fill = null;
        _previewEllipse.StrokeThickness = normalizedStrokeWidth;
        Canvas.SetLeft(_previewEllipse, x);
        Canvas.SetTop(_previewEllipse, y);
        _previewEllipse.Visibility = strokeWidthValue > 0 && strokeHeightValue > 0 && normalizedStrokeWidth > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        double fillWidth = Math.Max(0, width - normalizedStrokeWidth * 2);
        double fillHeight = Math.Max(0, height - normalizedStrokeWidth * 2);
        _previewEllipseFill!.Width = fillWidth;
        _previewEllipseFill.Height = fillHeight;
        _previewEllipseFill.Stroke = null;
        _previewEllipseFill.Fill = hasFill ? _previewFillBrush : null;
        Canvas.SetLeft(_previewEllipseFill, x + normalizedStrokeWidth);
        Canvas.SetTop(_previewEllipseFill, y + normalizedStrokeWidth);
        _previewEllipseFill.Visibility = hasFill && fillWidth > 0 && fillHeight > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void EnsureRectanglePreview()
    {
        if (_previewRectangleFill == null)
        {
            _previewRectangleFill = new Microsoft.UI.Xaml.Shapes.Rectangle();
            PreviewShapeCanvas.Children.Add(_previewRectangleFill);
        }

        if (_previewRectangle == null)
        {
            _previewRectangle = new Microsoft.UI.Xaml.Shapes.Rectangle();
            PreviewShapeCanvas.Children.Add(_previewRectangle);
        }
    }

    private void EnsureEllipsePreview()
    {
        if (_previewEllipseFill == null)
        {
            _previewEllipseFill = new Microsoft.UI.Xaml.Shapes.Ellipse();
            PreviewShapeCanvas.Children.Add(_previewEllipseFill);
        }

        if (_previewEllipse == null)
        {
            _previewEllipse = new Microsoft.UI.Xaml.Shapes.Ellipse();
            PreviewShapeCanvas.Children.Add(_previewEllipse);
        }
    }
    #endregion

    #region Shape Selection and Manipulation
    private void ShapeResizeOverlay_ResizeStarted(object? sender, EventArgs e)
    {
        if (_selectedShape == null)
        {
            return;
        }

        _shapeStateBeforeModification = new ModifyShapeOperation.ShapeState(_selectedShape);
        RenderCanvas.Invalidate();
    }

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
                ShapeModified?.Invoke(this, (_selectedShapeIndex, _shapeStateBeforeModification.Value, newState));
            }

            _shapeStateBeforeModification = null;
            RenderCanvas.Invalidate();
        }
    }

    private static bool StatesAreEqual(ModifyShapeOperation.ShapeState state1, ModifyShapeOperation.ShapeState state2)
    {
        return state1.Offset == state2.Offset &&
               state1.Size == state2.Size &&
               state1.EndPoint == state2.EndPoint &&
               state1.StrokeColor == state2.StrokeColor &&
               state1.FillColor == state2.FillColor &&
               state1.StrokeWidth == state2.StrokeWidth &&
               state1.Text == state2.Text &&
               state1.TextColor == state2.TextColor &&
               state1.TextBackgroundColor == state2.TextBackgroundColor &&
               state1.FontFamily == state2.FontFamily &&
               state1.FontSize.Equals(state2.FontSize);
    }

    private void DeselectShape()
    {
        CommitSelectedTextEditor();
        HideSelectedTextEditor();
        _selectedShape = null;
        _selectedShapeIndex = -1;
        ClearStyleInteraction();
        ShapeResizeOverlay.IsInteriorMoveEnabled = true;
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
            if (drawable is TextDrawable)
            {
                continue;
            }

            if (IsPointInShape(clickPoint, drawable))
            {
                return drawable;
            }
        }

        return null;
    }

    private IDrawable? FindTextAtPoint(Point clickPoint)
    {
        if (Drawables == null)
        {
            return null;
        }

        var drawableList = Drawables.ToList();
        for (int i = drawableList.Count - 1; i >= 0; i--)
        {
            if (drawableList[i] is TextDrawable text && IsPointInShape(clickPoint, text))
            {
                return text;
            }
        }

        return null;
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
                ShowSelectedTextEditor(drawable);

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

    private void SelectShape(IDrawable shape)
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
            ShowSelectedTextEditor(shape);

            // Ensure preview canvas is visible
            PreviewShapeCanvas.Visibility = Visibility.Visible;

            RenderCanvas.Invalidate(); // Redraw to hide selected shape from Win2D rendering

            // Set focus to enable keyboard events
            Focus(FocusState.Programmatic);
        }
    }

    public void DeleteSelectedShape()
    {
        if (_selectedShape == null || _selectedShapeIndex < 0)
        {
            return;
        }

        // Notify via event so the ViewModel can remove it from the collection
        ShapeDeleted?.Invoke(this, _selectedShapeIndex);

        // Clear selection state
        HideSelectedTextEditor();
        _selectedShape = null;
        _selectedShapeIndex = -1;
        ClearStyleInteraction();
        ShapeResizeOverlay.IsInteriorMoveEnabled = true;
        ShapeResizeOverlay.Visibility = Visibility.Collapsed;
        LineEndpointHandlesCanvas.Visibility = Visibility.Collapsed;
        ClearPreviewShape();
        RenderCanvas.Invalidate();
    }

    private void ClearStyleInteraction()
    {
        _styleInteractionStateBeforeModification = null;
        _styleInteractionShapeIndex = -1;
        _styleInteractionDepth = 0;
    }

    public void BeginSelectedDrawableStyleInteraction()
    {
        if (_selectedShape == null)
        {
            return;
        }

        if (_styleInteractionDepth++ > 0)
        {
            return;
        }

        CommitSelectedTextEditor();
        _styleInteractionStateBeforeModification = new ModifyShapeOperation.ShapeState(_selectedShape);
        _styleInteractionShapeIndex = _selectedShapeIndex;
    }

    public void CompleteSelectedDrawableStyleInteraction()
    {
        if (_styleInteractionDepth == 0)
        {
            return;
        }

        _styleInteractionDepth--;
        if (_styleInteractionDepth > 0)
        {
            return;
        }

        if (_selectedShape != null &&
            _styleInteractionStateBeforeModification.HasValue &&
            _styleInteractionShapeIndex == _selectedShapeIndex)
        {
            var newState = new ModifyShapeOperation.ShapeState(_selectedShape);
            if (!StatesAreEqual(_styleInteractionStateBeforeModification.Value, newState))
            {
                ShapeModified?.Invoke(this, (_selectedShapeIndex, _styleInteractionStateBeforeModification.Value, newState));
            }
        }

        _styleInteractionStateBeforeModification = null;
        _styleInteractionShapeIndex = -1;
        RenderCanvas.Invalidate();
    }

    private bool IsPointInShape(Point point, IDrawable drawable)
    {
        switch (drawable)
        {
            case LineDrawable line:
                return IsPointNearLine(
                    point,
                    CanvasPointToVector2DisplayPoint(line.Offset),
                    CanvasPointToVector2DisplayPoint(line.EndPoint),
                    Math.Max(10, line.StrokeWidth * 2));

            case ArrowDrawable arrow:
                return IsPointNearLine(
                    point,
                    CanvasPointToVector2DisplayPoint(arrow.Offset),
                    CanvasPointToVector2DisplayPoint(arrow.EndPoint),
                    Math.Max(10, arrow.StrokeWidth * 2));

            default:
                var bounds = CanvasBoundsToDisplayBounds(GetShapeBounds(drawable));
                return bounds.Contains((float)point.X, (float)point.Y);
        }
    }

    private Vector2 CanvasPointToVector2DisplayPoint(Vector2 point)
    {
        Point displayPoint = CanvasPointToDisplayPoint(point);
        return new Vector2((float)displayPoint.X, (float)displayPoint.Y);
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

            case TextDrawable text:
                return new RectangleF(text.Offset.X, text.Offset.Y, text.Size.Width, text.Size.Height);

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
            var bounds = CanvasBoundsToDisplayBounds(GetShapeBounds(drawable));
            ShapeResizeOverlay.IsInteriorMoveEnabled = drawable is not TextDrawable;
            ShapeResizeOverlay.ShapeBounds = bounds;
            ShapeResizeOverlay.Visibility = Visibility.Visible;
        }
    }

    private void ShowLineEndpointHandles(float x1, float y1, float x2, float y2)
    {
        UpdateOverlayHandleScale();
        LineEndpointHandlesCanvas.Visibility = Visibility.Visible;
        Point displayStart = CanvasPointToDisplayPoint(new Vector2(x1, y1));
        Point displayEnd = CanvasPointToDisplayPoint(new Vector2(x2, y2));
        float displayX1 = (float)displayStart.X;
        float displayY1 = (float)displayStart.Y;
        float displayX2 = (float)displayEnd.X;
        float displayY2 = (float)displayEnd.Y;

        // Position the selection visual line (dashed line)
        LineSelectionVisual.X1 = displayX1;
        LineSelectionVisual.Y1 = displayY1;
        LineSelectionVisual.X2 = displayX2;
        LineSelectionVisual.Y2 = displayY2;

        // Position the move handle line (make it cover the entire line for hit testing)
        LineMoveHandle.X1 = displayX1;
        LineMoveHandle.Y1 = displayY1;
        LineMoveHandle.X2 = displayX2;
        LineMoveHandle.Y2 = displayY2;

        // Position start handle (center the handle on the endpoint)
        Canvas.SetLeft(LineStartHandle, displayX1 - LineHandleRadius);
        Canvas.SetTop(LineStartHandle, displayY1 - LineHandleRadius);

        // Position end handle (center the handle on the endpoint)
        Canvas.SetLeft(LineEndHandle, displayX2 - LineHandleRadius);
        Canvas.SetTop(LineEndHandle, displayY2 - LineHandleRadius);
    }

    private double GetOverlayHandleScale(double? zoomFactor = null)
    {
        double zoom = zoomFactor ?? CanvasScrollView?.ZoomFactor ?? 1;
        if (double.IsNaN(zoom) || zoom <= 0)
        {
            zoom = 1;
        }

        return Math.Clamp(1d / zoom, MinimumHandleScale, MaximumHandleScale);
    }

    private void UpdateOverlayHandleScale(double? zoomFactor = null)
    {
        double scale = GetOverlayHandleScale(zoomFactor);

        CanvasContainer.Margin = new Thickness(CanvasContainerBaseMargin * scale);
        CropOverlay.HandleScale = scale;
        ShapeResizeOverlay.HandleScale = scale;
        LineStartHandle.RenderTransform = new ScaleTransform
        {
            ScaleX = scale,
            ScaleY = scale
        };
        LineEndHandle.RenderTransform = new ScaleTransform
        {
            ScaleX = scale,
            ScaleY = scale
        };
        LineSelectionVisual.StrokeThickness = LineSelectionStrokeThickness * scale;
        LineMoveHandle.StrokeThickness = LineMoveHandleStrokeThickness * scale;
    }

    private void LineStartHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(LineEndpointHandlesCanvas).Properties.IsLeftButtonPressed)
        {
            return;
        }

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
        if (!e.GetCurrentPoint(LineEndpointHandlesCanvas).Properties.IsLeftButtonPressed)
        {
            return;
        }

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
        var newPoint = DisplayPointToCanvasPoint(currentPoint);

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
                ShapeModified?.Invoke(this, (_selectedShapeIndex, _shapeStateBeforeModification.Value, newState));
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
        if (!e.GetCurrentPoint(LineEndpointHandlesCanvas).Properties.IsLeftButtonPressed)
        {
            return;
        }

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
        Vector2 currentCanvasPoint = DisplayPointToCanvasPoint(currentPoint);
        Vector2 startCanvasPoint = DisplayPointToCanvasPoint(_lineMoveStartPoint);
        float deltaX = currentCanvasPoint.X - startCanvasPoint.X;
        float deltaY = currentCanvasPoint.Y - startCanvasPoint.Y;

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

        RectangleF canvasBounds = DisplayBoundsToCanvasBounds(newBounds);

        switch (_selectedShape)
        {
            case RectangleDrawable rect:
                rect.Offset = new Vector2(canvasBounds.X, canvasBounds.Y);
                rect.Size = new Size((int)Math.Ceiling(canvasBounds.Width), (int)Math.Ceiling(canvasBounds.Height));
                break;

            case EllipseDrawable ellipse:
                ellipse.Offset = new Vector2(canvasBounds.X, canvasBounds.Y);
                ellipse.Size = new Size((int)Math.Ceiling(canvasBounds.Width), (int)Math.Ceiling(canvasBounds.Height));
                break;

            case LineDrawable line:
                {
                    // Preserve line direction when resizing
                    bool startsFromLeft = line.Offset.X <= line.EndPoint.X;
                    bool startsFromTop = line.Offset.Y <= line.EndPoint.Y;

                    // Apply new bounds while preserving direction
                    if (startsFromLeft && startsFromTop)
                    {
                        line.Offset = new Vector2(canvasBounds.X, canvasBounds.Y);
                        line.EndPoint = new Vector2(canvasBounds.X + canvasBounds.Width, canvasBounds.Y + canvasBounds.Height);
                    }
                    else if (!startsFromLeft && startsFromTop)
                    {
                        line.Offset = new Vector2(canvasBounds.X + canvasBounds.Width, canvasBounds.Y);
                        line.EndPoint = new Vector2(canvasBounds.X, canvasBounds.Y + canvasBounds.Height);
                    }
                    else if (startsFromLeft && !startsFromTop)
                    {
                        line.Offset = new Vector2(canvasBounds.X, canvasBounds.Y + canvasBounds.Height);
                        line.EndPoint = new Vector2(canvasBounds.X + canvasBounds.Width, canvasBounds.Y);
                    }
                    else
                    {
                        line.Offset = new Vector2(canvasBounds.X + canvasBounds.Width, canvasBounds.Y + canvasBounds.Height);
                        line.EndPoint = new Vector2(canvasBounds.X, canvasBounds.Y);
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
                        arrow.Offset = new Vector2(canvasBounds.X, canvasBounds.Y);
                        arrow.EndPoint = new Vector2(canvasBounds.X + canvasBounds.Width, canvasBounds.Y + canvasBounds.Height);
                    }
                    else if (!startsFromLeft && startsFromTop)
                    {
                        arrow.Offset = new Vector2(canvasBounds.X + canvasBounds.Width, canvasBounds.Y);
                        arrow.EndPoint = new Vector2(canvasBounds.X, canvasBounds.Y + canvasBounds.Height);
                    }
                    else if (startsFromLeft && !startsFromTop)
                    {
                        arrow.Offset = new Vector2(canvasBounds.X, canvasBounds.Y + canvasBounds.Height);
                        arrow.EndPoint = new Vector2(canvasBounds.X + canvasBounds.Width, canvasBounds.Y);
                    }
                    else
                    {
                        arrow.Offset = new Vector2(canvasBounds.X + canvasBounds.Width, canvasBounds.Y + canvasBounds.Height);
                        arrow.EndPoint = new Vector2(canvasBounds.X, canvasBounds.Y);
                    }
                }
                break;

            case TextDrawable text:
                text.Offset = new Vector2(canvasBounds.X, canvasBounds.Y);
                text.Size = new Size((int)Math.Ceiling(canvasBounds.Width), (int)Math.Ceiling(canvasBounds.Height));
                break;
        }
    }

    private void UpdateSelectedShapeStrokeColor(Color color)
    {
        if (_selectedShape == null)
        {
            return;
        }

        UpdateSelectedShapeStyle(() =>
        {
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
        });
    }

    private void UpdateSelectedShapeFillColor(Color color)
    {
        if (_selectedShape == null)
        {
            return;
        }

        UpdateSelectedShapeStyle(() =>
        {
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
        });
    }

    private void UpdateSelectedShapeStrokeWidth(int width)
    {
        if (_selectedShape == null)
        {
            return;
        }

        UpdateSelectedShapeStyle(() =>
        {
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
        });
    }

    private void UpdateSelectedShapeStyle(Action updateStyle)
    {
        if (_selectedShape == null)
        {
            return;
        }

        var oldState = new ModifyShapeOperation.ShapeState(_selectedShape);
        updateStyle();
        var newState = new ModifyShapeOperation.ShapeState(_selectedShape);

        if (_styleInteractionDepth == 0 && !StatesAreEqual(oldState, newState))
        {
            ShapeModified?.Invoke(this, (_selectedShapeIndex, oldState, newState));
        }

        UpdatePreviewShapeFromDrawable(_selectedShape);
        RenderCanvas.Invalidate();
    }

    private void UpdatePreviewShapeFromDrawable(IDrawable drawable)
    {
        switch (drawable)
        {
            case RectangleDrawable rect:
                {
                    RectangleF displayBounds = CanvasBoundsToDisplayBounds(GetShapeBounds(rect));

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

                    // Update fill brush
                    bool hasFill = rect.FillColor.A > 0;
                    if (hasFill)
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
                    }

                    UpdateRectanglePreview(displayBounds.X, displayBounds.Y, displayBounds.Width, displayBounds.Height, rect.StrokeWidth, hasFill);

                    // Hide other shapes
                    HideOtherPreviewShapes(ShapeType.Rectangle);
                }
                break;

            case EllipseDrawable ellipse:
                {
                    RectangleF displayBounds = CanvasBoundsToDisplayBounds(GetShapeBounds(ellipse));

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

                    // Update fill brush
                    bool hasFill = ellipse.FillColor.A > 0;
                    if (hasFill)
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
                    }

                    UpdateEllipsePreview(displayBounds.X, displayBounds.Y, displayBounds.Width, displayBounds.Height, ellipse.StrokeWidth, hasFill);

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

                    Point displayStart = CanvasPointToDisplayPoint(line.Offset);
                    Point displayEnd = CanvasPointToDisplayPoint(line.EndPoint);

                    // Update line properties from drawable
                    _previewLine.X1 = displayStart.X;
                    _previewLine.Y1 = displayStart.Y;
                    _previewLine.X2 = displayEnd.X;
                    _previewLine.Y2 = displayEnd.Y;

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
                    Point displayStart = CanvasPointToDisplayPoint(arrow.Offset);
                    Point displayEnd = CanvasPointToDisplayPoint(arrow.EndPoint);

                    _previewLine.X1 = displayStart.X;
                    _previewLine.Y1 = displayStart.Y;
                    _previewLine.X2 = displayEnd.X;
                    _previewLine.Y2 = displayEnd.Y;

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
                    double angle = Math.Atan2(displayEnd.Y - displayStart.Y, displayEnd.X - displayStart.X);
                    double arrowLength = 10 + arrow.StrokeWidth;
                    double arrowAngle = Math.PI / 6;

                    // Update arrow head points
                    _previewArrowHead.Points.Clear();
                    _previewArrowHead.Points.Add(new global::Windows.Foundation.Point(
                        displayEnd.X - arrowLength * Math.Cos(angle - arrowAngle),
                        displayEnd.Y - arrowLength * Math.Sin(angle - arrowAngle)));
                    _previewArrowHead.Points.Add(displayEnd);
                    _previewArrowHead.Points.Add(new global::Windows.Foundation.Point(
                        displayEnd.X - arrowLength * Math.Cos(angle + arrowAngle),
                        displayEnd.Y - arrowLength * Math.Sin(angle + arrowAngle)));

                    _previewArrowHead.Stroke = _previewStrokeBrush;
                    _previewArrowHead.StrokeThickness = arrow.StrokeWidth;
                    _previewArrowHead.StrokeLineJoin = Microsoft.UI.Xaml.Media.PenLineJoin.Miter;
                    _previewArrowHead.Visibility = Visibility.Visible;

                    // Hide other shapes
                    HideOtherPreviewShapes(ShapeType.Arrow);
                }
                break;

            case TextDrawable text:
                ClearPreviewShape();
                UpdateSelectedTextEditorLayout(text);
                break;
        }

        PreviewShapeCanvas.Visibility = drawable is TextDrawable ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateSelectedTextStyle(Action<TextDrawable> updateStyle)
    {
        if (_selectedShape is not TextDrawable text)
        {
            return;
        }

        if (_styleInteractionDepth == 0)
        {
            CommitSelectedTextEditor();
        }

        var oldState = new ModifyShapeOperation.ShapeState(text);
        updateStyle(text);
        var newState = new ModifyShapeOperation.ShapeState(text);

        if (_styleInteractionDepth == 0 && !StatesAreEqual(oldState, newState))
        {
            ShapeModified?.Invoke(this, (_selectedShapeIndex, oldState, newState));
        }

        UpdateSelectedTextEditor(text);
        RenderCanvas.Invalidate();
    }

    private void ShowSelectedTextEditor(IDrawable drawable)
    {
        if (drawable is not TextDrawable text)
        {
            HideSelectedTextEditor();
            return;
        }

        if (_selectedTextEditor == null)
        {
            _selectedTextEditor = new TextBox
            {
                AcceptsReturn = true,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(2, 3, 2, 3),
                TextWrapping = TextWrapping.Wrap,
                VerticalContentAlignment = VerticalAlignment.Top,
                HorizontalContentAlignment = HorizontalAlignment.Left,
            };
            _selectedTextEditor.TextChanged += SelectedTextEditor_TextChanged;
            _selectedTextEditor.LostFocus += SelectedTextEditor_LostFocus;
            _selectedTextEditor.RightTapped += SelectedTextEditor_RightTapped;
            TextEditorCanvas.Children.Add(_selectedTextEditor);
        }

        UpdateSelectedTextEditor(text);
        TextEditorCanvas.Visibility = Visibility.Visible;
        TextDrawableSelected?.Invoke(this, text);
        _selectedTextEditor.Focus(FocusState.Programmatic);
    }

    private void HideSelectedTextEditor()
    {
        if (_selectedTextEditor != null)
        {
            _selectedTextEditor.Visibility = Visibility.Collapsed;
        }

        TextEditorCanvas.Visibility = Visibility.Collapsed;
    }

    private void UpdateSelectedTextEditor(TextDrawable text)
    {
        if (_selectedTextEditor == null)
        {
            return;
        }

        _isUpdatingTextEditor = true;
        try
        {
            if (!string.Equals(_selectedTextEditor.Text, text.Text, StringComparison.Ordinal))
            {
                _selectedTextEditor.Text = text.Text;
            }

            _selectedTextEditor.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(WinUIColor.FromArgb(
                text.Color.A,
                text.Color.R,
                text.Color.G,
                text.Color.B));
            _selectedTextEditor.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(WinUIColor.FromArgb(
                text.BackgroundColor.A,
                text.BackgroundColor.R,
                text.BackgroundColor.G,
                text.BackgroundColor.B));
            _selectedTextEditor.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily(
                string.IsNullOrWhiteSpace(text.FontFamily) ? TextDrawable.DefaultFontFamily : text.FontFamily);
            _selectedTextEditor.FontSize = text.FontSize > 0 ? text.FontSize : TextDrawable.DefaultFontSize;
            _selectedTextEditor.Visibility = Visibility.Visible;
            UpdateSelectedTextEditorLayout(text);
        }
        finally
        {
            _isUpdatingTextEditor = false;
        }
    }

    private void UpdateSelectedTextEditorLayout(TextDrawable text)
    {
        if (_selectedTextEditor == null)
        {
            return;
        }

        RectangleF displayBounds = CanvasBoundsToDisplayBounds(GetShapeBounds(text));
        Canvas.SetLeft(_selectedTextEditor, displayBounds.X);
        Canvas.SetTop(_selectedTextEditor, displayBounds.Y);
        _selectedTextEditor.Width = Math.Max(2, displayBounds.Width);
        _selectedTextEditor.Height = Math.Max(2, displayBounds.Height);
    }

    private void SelectedTextEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingTextEditor || _selectedShape is not TextDrawable text || sender is not TextBox textBox)
        {
            return;
        }

        _shapeStateBeforeModification ??= new ModifyShapeOperation.ShapeState(text);
        text.Text = textBox.Text;
    }

    private void SelectedTextEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitSelectedTextEditor();
    }

    private void SelectedTextEditor_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (!IsTextModeEnabled || _selectedShape is not TextDrawable)
        {
            return;
        }

        CommitSelectedTextEditor();
        ShapeContextMenuRequested?.Invoke(this, e.GetPosition(this));
        e.Handled = true;
    }

    private void CommitSelectedTextEditor()
    {
        if (_selectedShape is not TextDrawable text || !_shapeStateBeforeModification.HasValue)
        {
            return;
        }

        var newState = new ModifyShapeOperation.ShapeState(text);
        if (!StatesAreEqual(_shapeStateBeforeModification.Value, newState))
        {
            ShapeModified?.Invoke(this, (_selectedShapeIndex, _shapeStateBeforeModification.Value, newState));
            RenderCanvas.Invalidate();
        }

        _shapeStateBeforeModification = null;
    }
    #endregion
}
