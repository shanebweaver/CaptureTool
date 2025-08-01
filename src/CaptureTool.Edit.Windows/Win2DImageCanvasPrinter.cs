using CaptureTool.Edit.Drawable;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Printing;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Printing;

namespace CaptureTool.Edit.Windows;

public partial class Win2DImageCanvasPrinter : IImageCanvasPrinter
{
    private const uint PageCount = 1;

    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private CanvasPrintDocument? _printDocument = null;

    public Win2DImageCanvasPrinter()
    {
    }

    ~Win2DImageCanvasPrinter()
    {
        _printDocument?.Dispose();
    }

    public async Task ShowPrintUIAsync(IDrawable[] drawables, ImageCanvasRenderOptions options, nint hwnd)
    {
        await _semaphore.WaitAsync();

        _printDocument?.Dispose();

        _printDocument = new();
        _printDocument.Preview += PrintDocument_Preview;
        _printDocument.Print += PrintDocument_Print;

        try
        {
            if (PrintManager.IsSupported())
            {
                PrintManager printManager = PrintManagerInterop.GetForWindow(hwnd);
                printManager.PrintTaskRequested -= OnPrintTaskRequested;
                printManager.PrintTaskRequested += OnPrintTaskRequested;
                await PrintManagerInterop.ShowPrintUIForWindowAsync(hwnd);
            }
            else
            {
                throw new NotSupportedException("Printing is not supported.");
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e.Message);
        }
        finally
        {
            _semaphore.Release();
        }

        void PrintDocument_Preview(CanvasPrintDocument sender, CanvasPreviewEventArgs args)
        {
            var deferral = args.GetDeferral();
            try
            {
                Print(sender, args.DrawingSession, args.PrintTaskOptions, drawables, options);
            }
            finally 
            {
                deferral.Complete(); 
            }
        }

        void PrintDocument_Print(CanvasPrintDocument sender, CanvasPrintEventArgs args)
        {
            var deferral = args.GetDeferral();
            try
            {
                using var printDrawingSession = args.CreateDrawingSession();
                Print(sender, printDrawingSession, args.PrintTaskOptions, drawables, options);
            }
            finally
            {
                deferral.Complete();
            }
        }
    }

    private static void Print(CanvasPrintDocument printDocument, CanvasDrawingSession printDrawingSession, PrintTaskOptions printOptions, IDrawable[] drawables, ImageCanvasRenderOptions renderOptions)
    {
        printDocument.SetPageCount(PageCount);

        var pageDescription = printOptions.GetPageDescription(PageCount);
        float scale = CalculateScaleForOnePage(renderOptions, pageDescription);
        
        Win2DImageCanvasRenderer.Render(drawables, renderOptions, printDrawingSession, scale);
    }

    private void OnPrintTaskRequested(PrintManager sender, PrintTaskRequestedEventArgs args)
    {
        sender.PrintTaskRequested -= OnPrintTaskRequested;

        PrintTask printTask = args.Request.CreatePrintTask("Capture Tool Image Print", (printTaskArgs) =>
        {
            if (_printDocument != null)
            {
                printTaskArgs.SetSource(_printDocument);
            }
        });
        printTask.Completed += PrintTask_Completed;
    }

    private void PrintTask_Completed(PrintTask sender, PrintTaskCompletedEventArgs args)
    {
        sender.Completed -= PrintTask_Completed;
        _printDocument?.Dispose();
    }

    private static float CalculateScaleForOnePage(ImageCanvasRenderOptions options, PrintPageDescription pageDescription)
    {
        var imageableRect = pageDescription.ImageableRect;
        var originalRect = options.CropRect;

        // Calculate scale factor to fit originalRect within imageableRect
        float scaleX = (float)(imageableRect.Width / originalRect.Width);
        float scaleY = (float)(imageableRect.Height / originalRect.Height);
        float scale = Math.Min(scaleX, scaleY);

        // Only shrink, don't grow
        return Math.Min(scale, 1);
    }
}
