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
    public Task ShowPrintUIAsync(IDrawable[] drawables, ImageCanvasRenderOptions options, nint hwnd)
    {
        Win2DImageCanvasPrintSession printSession = new();
        return printSession.ShowPrintUIAsync(drawables, options, hwnd);
    }
}

public partial class Win2DImageCanvasPrintSession
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private CanvasPrintDocument? _printDocument = null;

    public Win2DImageCanvasPrintSession()
    {
    }

    ~Win2DImageCanvasPrintSession()
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
        sender.PrintTaskRequested -= OnPrintTaskRequested;

        args.Request.CreatePrintTask("Capture Tool Image Print", (a) =>
        {
            if (_printDocument != null)
            {
                a.SetSource(_printDocument);
            }
        });
    }
}
