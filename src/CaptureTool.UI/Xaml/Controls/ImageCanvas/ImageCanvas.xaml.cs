using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using CaptureTool.Edit.Image.Win2D;
using CaptureTool.Edit.Image.Win2D.Drawable;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

namespace CaptureTool.UI.Xaml.Controls.ImageCanvas;

public sealed partial class ImageCanvas : UserControl
{
    private static readonly DependencyProperty DrawablesProperty = DependencyProperty.Register(
        nameof(Drawables),
        typeof(IEnumerable<IDrawable>),
        typeof(ImageCanvas),
        new PropertyMetadata(null));

    private static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register(
        nameof(ImageSource),
        typeof(string),
        typeof(ImageCanvas),
        new PropertyMetadata(null, OnImageSourcePropertyChanged));

    private static void OnImageSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageCanvas canvas)
        {
            if (e.NewValue is string imageSource)
            {
                Rect imageSize = GetImageSize(imageSource);
                canvas.UpdateCanvasSize(imageSize);
            }
        }
    }

    public IEnumerable<IDrawable> Drawables
    {
        get => (IEnumerable<IDrawable>)GetValue(DrawablesProperty);
        set => SetValue(DrawablesProperty, value);
    }

    public string ImageSource
    {
        get => (string)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    private bool _isPointerDown;
    private Point _lastPointerPosition;
    private ImageDrawable? _imageDrawable;

    public ImageCanvas()
    {
        InitializeComponent();
    }

    public void ShowPrintUI()
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            IDrawable[] toDraw = GetDrawablesToDraw();
            await ImageCanvasPrinter.ShowPrintUIAsync(toDraw);
        });
    }

    public void CopyImageToClipboard()
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            if (_imageDrawable != null)
            {
                IDrawable[] toDraw = GetDrawablesToDraw();
                Rect imageSize = GetImageSize(ImageSource);
                await ImageCanvasRenderer.CopyImageToClipboardAsync(toDraw, (float)imageSize.Width, (float)imageSize.Height, 96);
            }
        });
    }

    private IDrawable[] GetDrawablesToDraw()
    {
        List<IDrawable> toDraw = [_imageDrawable];
        toDraw.AddRange(Drawables);
        return [.. toDraw];
    }

    private void CanvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        lock (this)
        {
            IDrawable[] toDraw = GetDrawablesToDraw();
            CanvasCommandList commandList = ImageCanvasRenderer.Render(toDraw);

            Vector2 sceneTopLeft = new(0, 0);
            args.DrawingSession.DrawImage(commandList, sceneTopLeft);
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
        _imageDrawable = new(new(0, 0), ImageSource);

        List<Task> preparationTasks = [
            _imageDrawable.PrepareAsync(sender)
        ];

        foreach (IDrawable drawable in Drawables)
        {
            if (drawable is ImageDrawable imageDrawable)
            {
                Task prepTask = imageDrawable.PrepareAsync(sender);
                preparationTasks.Add(prepTask);
            }
        }

        await Task.WhenAll(preparationTasks);
    }

    private void UpdateCanvasSize(Rect newSize)
    {
        lock (this)
        {
            AnnotationCanvas.Height = newSize.Height;
            AnnotationCanvas.Width = newSize.Width;
        }
    }

    #region Panning
    private void CanvasContainer_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isPointerDown = true;
        _lastPointerPosition = e.GetCurrentPoint(CanvasContainer).Position;
        CanvasContainer.CapturePointer(e.Pointer); // Capture the pointer for consistent tracking
    }

    private void CanvasContainer_PointerMoved(object sender, PointerRoutedEventArgs e)
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

    private void CanvasContainer_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isPointerDown = false;
        CanvasContainer.ReleasePointerCaptures();
    }

    private void CanvasContainer_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        _isPointerDown = false;
        CanvasContainer.ReleasePointerCaptures();
    }
    #endregion

    private static Rect GetImageSize(string imageFileName)
    {
        using FileStream file = new(imageFileName, FileMode.Open, FileAccess.Read);
        var image = System.Drawing.Image.FromStream(
            stream: file,
            useEmbeddedColorManagement: false,
            validateImageData: false);

        float width = image.PhysicalDimension.Width;
        float height = image.PhysicalDimension.Height;

        return new Rect(0, 0, width, height);
    }
}
