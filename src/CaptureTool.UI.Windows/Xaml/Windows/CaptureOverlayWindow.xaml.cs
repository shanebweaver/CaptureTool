using CaptureTool.Capture;
using CaptureTool.Storage;
using CaptureTool.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CaptureTool.UI.Windows.Xaml.Windows;

public sealed partial class CaptureOverlayWindow : Window
{
    public CaptureOverlayWindowViewModel ViewModel { get; } = ViewModelLocator.GetViewModel<CaptureOverlayWindowViewModel>();

    public CaptureOverlayWindow(MonitorCaptureResult monitor)
    {
        InitializeComponent();

        AppWindow.IsShownInSwitchers = false;
        AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);

        ViewModel.Monitor = monitor;

        var bounds = monitor.MonitorBounds;
        AppWindow.MoveAndResize(new(bounds.X, bounds.Y, bounds.Width, bounds.Height));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(false, false);
            presenter.Maximize();
        }
    }

    private async void CaptureButton_Click(object sender, RoutedEventArgs e)
    {
        RootPanel.Opacity = 0;

        // Allow the UI thread to process the opacity change and render.
        // This is not ideal, but there is no deterministic way to ensure that the UI is updated in time for the capture.
        await Task.Yield();
        await Task.Yield();
        await Task.Delay(50);

        DispatcherQueue.TryEnqueue(() =>
        {
            ViewModel.RequestCaptureCommand.Execute(null);

            /*
            var monitor = ViewModel.Monitor;
            if (monitor == null)
            {
                return;
            }

            var area = ViewModel.CaptureArea;
            var monitorBounds = monitor.MonitorBounds;

            // Create a bitmap for the full monitor
            using var fullBmp = new Bitmap(monitorBounds.Width, monitorBounds.Height, PixelFormat.Format32bppArgb);
            var bmpData = fullBmp.LockBits(
                new Rectangle(0, 0, monitorBounds.Width, monitorBounds.Height),
                ImageLockMode.WriteOnly,
                fullBmp.PixelFormat
            );

            try
            {
                Marshal.Copy(monitor.PixelBuffer, 0, bmpData.Scan0, monitor.PixelBuffer.Length);
            }
            finally
            {
                fullBmp.UnlockBits(bmpData);
            }

            // Crop to the selected area
            float scale = monitor.Scale;
            int cropX = (int)Math.Round((area.Left - monitorBounds.Left) * scale);
            int cropY = (int)Math.Round((area.Top - monitorBounds.Top) * scale);
            int cropWidth = (int)Math.Round(area.Width * scale);
            int cropHeight = (int)Math.Round(area.Height * scale);

            using var croppedBmp = fullBmp.Clone(new Rectangle(cropX, cropY, cropWidth, cropHeight), fullBmp.PixelFormat);
            var tempPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Temp",
                $"capture_{Guid.NewGuid()}.png"
            );

            Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);
            croppedBmp.Save(tempPath, ImageFormat.Png);

            var imageFile = new ImageFile(tempPath);
            ViewModel.PerformCaptureCommand.Execute(imageFile);
            */
        });
    }
}
