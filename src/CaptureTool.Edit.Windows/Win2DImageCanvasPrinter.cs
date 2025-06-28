using CaptureTool.Core.AppController;
using CaptureTool.Edit.Drawable;
using Microsoft.Graphics.Canvas.Printing;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Printing;

namespace CaptureTool.Edit.Windows;

public partial class Win2DImageCanvasPrinter : IImageCanvasPrinter
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly IAppController _appController;

    private CanvasPrintDocument? _printDocument = null;

    public Win2DImageCanvasPrinter(IAppController appController)
    {
        _appController = appController;
    }

    ~Win2DImageCanvasPrinter()
    {
        _printDocument?.Dispose();
    }

    public async Task ShowPrintUIAsync(IDrawable[] drawables, ImageCanvasRenderOptions options)
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
                nint hwnd = _appController.GetMainWindowHandle();
                PrintManager printManager = PrintManagerInterop.GetForWindow(hwnd);
                printManager.PrintTaskRequested += OnPrintTaskRequested;
                bool success = await PrintManagerInterop.ShowPrintUIForWindowAsync(hwnd);
                printManager.PrintTaskRequested -= OnPrintTaskRequested;
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
            sender.SetPageCount(1);
            Win2DImageCanvasRenderer.Render(drawables, options, args.DrawingSession);
        }

        void PrintDocument_Print(CanvasPrintDocument sender, CanvasPrintEventArgs args)
        {
            using var printDrawingSession = args.CreateDrawingSession();
            Win2DImageCanvasRenderer.Render(drawables, options, printDrawingSession);
        }
    }

    private void OnPrintTaskRequested(PrintManager sender, PrintTaskRequestedEventArgs args)
    {
        args.Request.CreatePrintTask("Capture Tool Image Print", (a) =>
        {
            if (_printDocument != null)
            {
                a.SetSource(_printDocument);
            }
        });
    }
}
