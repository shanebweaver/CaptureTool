using CaptureTool.Edit.Image.Win2D.Drawable;
using Microsoft.Graphics.Canvas.Printing;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Printing;

namespace CaptureTool.Edit.Image.Win2D;

public partial class ImageCanvasPrinter
{
    public static ImageCanvasPrinter Default { get; } = new();

    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private CanvasPrintDocument? _printDocument = null;

    ~ImageCanvasPrinter()
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
            ImageCanvasRenderer.Render(drawables, options, args.DrawingSession);
        }

        void PrintDocument_Print(CanvasPrintDocument sender, CanvasPrintEventArgs args)
        {
            using var printDrawingSession = args.CreateDrawingSession();
            ImageCanvasRenderer.Render(drawables, options, printDrawingSession);
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
