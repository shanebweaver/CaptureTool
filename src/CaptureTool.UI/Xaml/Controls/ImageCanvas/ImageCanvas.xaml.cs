using System;
using System.Collections;
using System.Numerics;
using System.Threading.Tasks;
using CaptureTool.UI.Xaml.Controls.ImageCanvas.Drawable;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace CaptureTool.UI.Xaml.Controls.ImageCanvas;

public sealed partial class ImageCanvas : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(IEnumerable),
        typeof(ImageCanvas),
        new PropertyMetadata(null));

    public IEnumerable ItemsSource
    {
        get => (IEnumerable)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    private static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register(
        nameof(ImageSource),
        typeof(string),
        typeof(ImageCanvas),
        new PropertyMetadata(null));

    public string ImageSource
    {
        get => (string)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    private CanvasBitmap? _canvasImage;
    private bool _isPointerDown;
    private Point _lastPointerPosition;

    public ImageCanvas()
    {
        InitializeComponent();
        SizeChanged += ImageCanvas_SizeChanged;
    }

    private void ImageCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateCanvasSize();
        ZoomAndCenterCanvas();
    }

    private void CanvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        lock (this)
        {
            UpdateCanvasSize();

            Vector2 sceneTopLeft = new(0, 0);
            Vector2 sceneCenter = new((int)Math.Floor(AnnotationCanvas.ActualWidth / 2), (int)Math.Floor(AnnotationCanvas.ActualHeight / 2));

            if (_canvasImage != null)
            {
                Vector2 imageCenter = new((int)Math.Floor(_canvasImage.Bounds.Width / 2), (int)Math.Floor(_canvasImage.Bounds.Height / 2));
                sceneTopLeft = sceneCenter - imageCenter;

                args.DrawingSession.DrawImage(_canvasImage, sceneTopLeft);
            }

            Rect sceneBounds = new(new Point(sceneTopLeft.X, sceneTopLeft.Y), new Size(AnnotationCanvas.ActualWidth, AnnotationCanvas.ActualHeight));

            if (ItemsSource is IEnumerable items)
            {
                foreach (var item in items)
                {
                    if (item is IDrawable drawable)
                    {
                        try
                        {
                            drawable.Draw(args.DrawingSession, sceneBounds);
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }

            // Center dot
            //args.DrawingSession.DrawRectangle(new Rect(sceneCenter.X - 1, sceneCenter.Y - 1, 2, 2), Colors.Red);
        }
    }

    private void CanvasControl_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
    {
        // Create any resources needed by the Draw event handler.

        // Asynchronous work can be tracked with TrackAsyncAction:
        args.TrackAsyncAction(CreateResourcesAsync(sender).AsAsyncAction());
    }

    private async Task CreateResourcesAsync(CanvasControl sender)
    {
        // Load bitmaps, create brushes, etc.
        if (ImageSource != null)
        {
            _canvasImage = await CanvasBitmap.LoadAsync(sender, ImageSource);
        }
    }

    private void UpdateCanvasSize()
    {
        lock (this)
        {
            double newHeight = CanvasContainer.ActualHeight;
            double newWidth = CanvasContainer.ActualWidth;

            AnnotationCanvas.Height = newHeight;
            AnnotationCanvas.Width = newWidth;
            AnnotationCanvas.CenterPoint = new((float)(newWidth / 2), (float)(newHeight / 2), 0);
        }
    }

    private void ZoomAndCenterCanvas(float zoomFactor = 1.0f)
    {
        lock (this)
        {
            if (CanvasScrollView == null || AnnotationCanvas == null)
            {
                return;
            }

            CanvasScrollView.ZoomTo(zoomFactor, null, new(ScrollingAnimationMode.Disabled));

            // Get the dimensions of the ScrollViewer and CanvasControl
            double scrollViewWidth = CanvasScrollView.ActualWidth;
            double scrollViewHeight = CanvasScrollView.ActualHeight;
            double canvasWidth = AnnotationCanvas.ActualWidth * zoomFactor;
            double canvasHeight = AnnotationCanvas.ActualHeight * zoomFactor;

            // Calculate the offsets to center the canvas
            double horizontalOffset = (canvasWidth - scrollViewWidth) / 2;
            double verticalOffset = (canvasHeight - scrollViewHeight) / 2;

            // Ensure offsets are non-negative (to prevent scrolling out of bounds)
            horizontalOffset = Math.Max(0, horizontalOffset);
            verticalOffset = Math.Max(0, verticalOffset);

            // Scroll to the calculated offsets
            CanvasScrollView.ZoomTo(zoomFactor, new((float)horizontalOffset, (float)verticalOffset));
        }
    }

    private void CanvasContainer_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _isPointerDown = true;
        _lastPointerPosition = e.GetCurrentPoint(CanvasContainer).Position;
        CanvasContainer.CapturePointer(e.Pointer); // Capture the pointer for consistent tracking
    }

    private void CanvasContainer_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_isPointerDown)
        {
            // Get the current pointer position
            Point currentPosition = e.GetCurrentPoint(CanvasContainer).Position;

            // Calculate the difference between the current and last pointer positions
            double deltaX = _lastPointerPosition.X - currentPosition.X;
            double deltaY = _lastPointerPosition.Y - currentPosition.Y;

            if (deltaX == 0 && deltaY == 0)
            {
                return;
            }

            // Update the scroll offsets of the CanvasScrollView
            CanvasScrollView.ScrollBy(
                deltaX,
                deltaY,
                new(ScrollingAnimationMode.Disabled)
            );

            // Update the last pointer position
            _lastPointerPosition = currentPosition;
        }
    }

    private void CanvasContainer_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _isPointerDown = false;
        CanvasContainer.ReleasePointerCaptures();
    }

    private void CanvasContainer_PointerCanceled(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _isPointerDown = false;
        CanvasContainer.ReleasePointerCaptures();
    }
}
