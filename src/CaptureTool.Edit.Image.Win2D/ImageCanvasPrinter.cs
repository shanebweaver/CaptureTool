using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Edit.Image.Win2D.Drawable;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Printing;
using Windows.Graphics.Printing;

namespace CaptureTool.Edit.Image.Win2D;

public static partial class ImageCanvasPrinter
{
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    public static async Task ShowPrintUIAsync(IDrawable[] drawables, ImageCanvasRenderOptions options, nint hwnd)
    {
        await _semaphore.WaitAsync();

        void PrintDocument_Preview(CanvasPrintDocument sender, CanvasPreviewEventArgs args)
        {
            sender.SetPageCount(1);
            ImageCanvasRenderer.Render(drawables, options, args.DrawingSession);
        }

        void PrintDocument_Print(CanvasPrintDocument sender, CanvasPrintEventArgs args)
        {
            using var printDrawingSession = args.CreateDrawingSession();
            ImageCanvasRenderer.Render(drawables, options, printDrawingSession);
        }

        CanvasPrintDocument printDocument = new(CanvasDevice.GetSharedDevice());
        printDocument.Preview += PrintDocument_Preview;
        printDocument.Print += PrintDocument_Print;

        void OnPrintTaskRequested(PrintManager sender, PrintTaskRequestedEventArgs args)
        {
            args.Request.CreatePrintTask("Capture Tool Image Print", (a) =>
            {
                a.SetSource(printDocument);
            });
        }

        try
        {
            PrintManager printManager = PrintManagerInterop.GetForWindow(hwnd);
            printManager.PrintTaskRequested += OnPrintTaskRequested;

            try
            {
                bool success = await PrintManagerInterop.ShowPrintUIForWindowAsync(hwnd);
            }
            finally
            {
                printManager.PrintTaskRequested -= OnPrintTaskRequested;
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e.Message);
        }
        finally
        {
            printDocument.Preview -= PrintDocument_Preview;
            printDocument.Print -= PrintDocument_Print;
            printDocument.Dispose();
            _semaphore.Release();
        }
    }
}
