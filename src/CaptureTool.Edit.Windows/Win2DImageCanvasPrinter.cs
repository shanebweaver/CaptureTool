using CaptureTool.Edit.Drawable;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Printing;
using System;
using System.Threading.Tasks;
using Windows.Graphics.Printing;

namespace CaptureTool.Edit.Windows;

public partial class Win2DImageCanvasPrinter : IImageCanvasPrinter
{
    private const uint PageCount = 1;

    public Win2DImageCanvasPrinter()
    {
    }

    public async Task ShowPrintUIAsync(IDrawable[] drawables, ImageCanvasRenderOptions options, nint hwnd)
    {
        if (!PrintManager.IsSupported())
        {
            throw new NotSupportedException("Printing is not supported.");
        }

        CanvasPrintDocument printDocument = new();
        printDocument.Preview += PrintDocument_Preview;
        printDocument.Print += PrintDocument_Print;

        PrintManager printManager = PrintManagerInterop.GetForWindow(hwnd);
        printManager.PrintTaskRequested -= OnPrintTaskRequested;
        printManager.PrintTaskRequested += OnPrintTaskRequested;
        await PrintManagerInterop.ShowPrintUIForWindowAsync(hwnd);
       
        void PrintDocument_Preview(CanvasPrintDocument sender, CanvasPreviewEventArgs args)
        {
            var deferral = args.GetDeferral();
            try
            {
                printDocument.SetPageCount(PageCount);
                Print(args.DrawingSession, args.PrintTaskOptions, drawables, options);
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
                Print(printDrawingSession, args.PrintTaskOptions, drawables, options);
            }
            finally
            {
                deferral.Complete();
            }
        }

        void OnPrintTaskRequested(PrintManager sender, PrintTaskRequestedEventArgs args)
        {
            sender.PrintTaskRequested -= OnPrintTaskRequested;

            PrintTask printTask = args.Request.CreatePrintTask("Capture Tool Image Print", (printTaskArgs) =>
            {
                if (printDocument != null)
                {
                    printTaskArgs.SetSource(printDocument);
                }
            });
            printTask.Completed += PrintTask_Completed;
        }

        void PrintTask_Completed(PrintTask sender, PrintTaskCompletedEventArgs args)
        {
            sender.Completed -= PrintTask_Completed;
            printDocument?.Dispose();
        }
    }

    private static void Print(CanvasDrawingSession printDrawingSession, PrintTaskOptions printOptions, IDrawable[] drawables, ImageCanvasRenderOptions renderOptions)
    {
        PrintPageDescription pageDescription = printOptions.GetPageDescription(PageCount);
        float scale = CalculateScaleForOnePage(renderOptions, pageDescription);
        Win2DImageCanvasRenderer.Render(drawables, renderOptions, printDrawingSession, scale);
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
