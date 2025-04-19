using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using CaptureTool.Edit.Image.Win2D;
using CaptureTool.Edit.Image.Win2D.Drawable;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Printing;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Graphics.Printing;
using Windows.Storage.Streams;

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
    private ImageCanvasRenderer? _imageCanvasRenderer;
    private ImageDrawable? _imageDrawable;
    private CanvasPrintDocument? _printDocument;

    public ImageCanvas()
    {
        InitializeComponent();

        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_printDocument != null)
        {
            _printDocument.Dispose();
            _printDocument = null;
        }

        if (_imageCanvasRenderer != null)
        {
            _imageCanvasRenderer.Dispose();
            _imageCanvasRenderer = null;
        }
    }

    public async Task<IRandomAccessStream> GetCanvasImageStreamAsync()
    {
        var renderTargetBitmap = new RenderTargetBitmap();
        await renderTargetBitmap.RenderAsync(AnnotationCanvas);

        var pixelBuffer = await renderTargetBitmap.GetPixelsAsync();
        var stream = new InMemoryRandomAccessStream();

        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            (uint)renderTargetBitmap.PixelWidth,
            (uint)renderTargetBitmap.PixelHeight,
            96, // DPI X
            96, // DPI Y
            pixelBuffer.ToArray()
        );

        await encoder.FlushAsync();

        stream.Seek(0); // Reset the stream position to the beginning
        return stream;
    }

    private void CanvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        lock (this)
        {
            if (_imageCanvasRenderer != null)
            {
                List<IDrawable> toDraw = [_imageDrawable];
                toDraw.AddRange(Drawables);
                CanvasCommandList commandList = _imageCanvasRenderer.Render([.. toDraw]);

                Vector2 sceneTopLeft = new(0, 0);
                args.DrawingSession.DrawImage(commandList, sceneTopLeft);
            }
        }
    }

    private void CanvasControl_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
    {
        // Create any resources needed by the Draw event handler.
        if (_imageCanvasRenderer != null)
        {
            _imageCanvasRenderer.Dispose();
            _imageCanvasRenderer = null;
        }
        _imageCanvasRenderer = new();

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

    #region Printing
    public async Task ShowPrintUIAsync()
    {
        CanvasPrintDocument printDocument = MakePrintDocument();

        void OnPrintTaskRequested(PrintManager sender, PrintTaskRequestedEventArgs args)
        {
            args.Request.CreatePrintTask("Capture Tool Image Print", (a) =>
            {
                a.SetSource(printDocument);
            });
        }

        var printManager = PrintManager.GetForCurrentView(); // TODO: Replace with IPrintManagerInterop::GetForWindow
        printManager.PrintTaskRequested += OnPrintTaskRequested;
        await PrintManager.ShowPrintUIAsync();
        printManager.PrintTaskRequested -= OnPrintTaskRequested;
    }

    private CanvasPrintDocument MakePrintDocument()
    {
        _printDocument?.Dispose();
        _printDocument = new CanvasPrintDocument();

        _printDocument.Preview += (sender, args) =>
        {
            sender.SetPageCount(1);
            PrintPage(args.DrawingSession, args.PrintTaskOptions.GetPageDescription(1));
        };

        _printDocument.Print += (sender, args) =>
        {
            using var printDrawingSession = args.CreateDrawingSession();
            PrintPage(printDrawingSession, args.PrintTaskOptions.GetPageDescription(1));
        };

        return _printDocument;
    }

    private void PrintPage(CanvasDrawingSession printDrawingSession, PrintPageDescription desc)
    {
        List<IDrawable> toDraw = [_imageDrawable];
        toDraw.AddRange(Drawables);
        ImageCanvasRenderer.Render([.. toDraw], printDrawingSession);
    }
    #endregion

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

    //public Task<SoftwareBitmap> GetCanvasImageAsync()
    //{
    //    return GetCanvasImageAsync(AnnotationCanvas);
    //}

    //public static async Task<SoftwareBitmap> GetCanvasImageAsync(CanvasControl canvasControl)
    //{
    //    var renderTargetBitmap = new RenderTargetBitmap();
    //    await renderTargetBitmap.RenderAsync(canvasControl);

    //    var pixelBuffer = await renderTargetBitmap.GetPixelsAsync();
    //    var softwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, renderTargetBitmap.PixelWidth, renderTargetBitmap.PixelHeight);
    //    softwareBitmap.CopyFromBuffer(pixelBuffer);

    //    return softwareBitmap;
    //}
}
