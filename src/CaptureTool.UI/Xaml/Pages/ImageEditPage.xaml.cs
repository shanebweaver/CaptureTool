using System;
using System.Numerics;
using System.Threading.Tasks;
using CaptureTool.ViewModels;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace CaptureTool.UI.Xaml.Pages;

public sealed partial class ImageEditPage : ImageEditPageBase
{
    private CanvasBitmap? _canvasImage;

    public ImageEditPage()
    {
        InitializeComponent();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        SizeChanged += OnPageSizeChanged;
    }

    ~ImageEditPage()
    {
        AnnotationCanvas.RemoveFromVisualTree();
        AnnotationCanvas = null;
    }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateCanvasSize();
        ZoomAndCenterCanvas();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.LoadState) && ViewModel.IsLoaded)
        {
            AnnotationCanvas.Invalidate();
        }
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

            foreach (var item in ViewModel.CanvasItems)
            {
                if (item is RectangleCanvasItemViewModel rectangleItem)
                {
                    Rect rectangleRect = new(sceneTopLeft.X + rectangleItem.Left, sceneTopLeft.Y + rectangleItem.Top, rectangleItem.Width, rectangleItem.Height);
                    args.DrawingSession.DrawRectangle(rectangleRect, Colors.Red);
                }
                else if (item is TextCanvasItemViewModel textItem)
                {
                    Vector2 textPosition = new(sceneTopLeft.X + textItem.Left, sceneTopLeft.Y + textItem.Top);
                    args.DrawingSession.DrawText(textItem.Text, textPosition, Colors.Black);
                }
            }

            // Center dot
            args.DrawingSession.DrawRectangle(new Rect(sceneCenter.X - 1, sceneCenter.Y - 1, 2, 2), Colors.Red);
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
        if (ViewModel.ImageCanvasItemViewModel?.ImageFile != null)
        {
            _canvasImage = await CanvasBitmap.LoadAsync(sender, ViewModel.ImageCanvasItemViewModel?.ImageFile.Path);
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
}
