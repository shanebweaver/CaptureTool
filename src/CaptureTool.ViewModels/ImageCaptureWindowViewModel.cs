using CaptureTool.Capture.Image;
using CaptureTool.Capture.Windows;
using CaptureTool.Common.Commands;
using CaptureTool.Core;
using CaptureTool.Core.AppController;
using CaptureTool.Services.Navigation;
using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;

namespace CaptureTool.ViewModels;

public sealed partial class ImageCaptureWindowViewModel : ViewModelBase
{
    private readonly IAppController _appController;
    private readonly INavigationService _navigationService;

    public RelayCommand PerformCaptureCommand => new(PerformCapture);
    public RelayCommand GoBackCommand => new(GoBack);
    public RelayCommand ToggleShowOptionsCommand => new(ToggleShowOptions);

    private Rectangle _captureArea;
    public Rectangle CaptureArea
    {
        get => _captureArea;
        set => Set(ref _captureArea, value);
    }

    private bool _showOptions;
    public bool ShowOptions
    {
        get => _showOptions;
        set => Set(ref _showOptions, value);
    }

    private ObservableCollection<MonitorCaptureResult> _monitors;
    public ObservableCollection<MonitorCaptureResult> Monitors
    {
        get => _monitors;
        set => Set(ref _monitors, value);
    }

    public ImageCaptureWindowViewModel(
        IAppController appController,
        INavigationService navigationService)
    {
        _appController = appController;
        _navigationService = navigationService;

        _captureArea = new(100, 100, 200, 300);
        _monitors = [];
    }

    private void GoBack()
    {
        _appController.GoBackOrHome();
    }

    private void ToggleShowOptions()
    {
        ShowOptions = !ShowOptions;
    }

    private void PerformCapture()
    {
        Monitors.Clear();
        var monitors = MonitorCaptureHelper.CaptureAllMonitors();
        foreach (var monitor in monitors)
        {
            Monitors.Add(monitor);
        }

        if (monitors.Count > 0)
        {
            var firstMonitor = monitors[0];
            var tempPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Temp",
                $"capture_{Guid.NewGuid()}.png"
            );

            Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);

            using (var bmp = new Bitmap(firstMonitor.Width, firstMonitor.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                var bmpData = bmp.LockBits(
                    new Rectangle(0, 0, firstMonitor.Width, firstMonitor.Height),
                    System.Drawing.Imaging.ImageLockMode.WriteOnly,
                    bmp.PixelFormat
                );

                try
                {
                    // BGRA8 to ARGB
                    System.Runtime.InteropServices.Marshal.Copy(firstMonitor.PixelBuffer, 0, bmpData.Scan0, firstMonitor.PixelBuffer.Length);
                }
                finally
                {
                    bmp.UnlockBits(bmpData);
                }

                bmp.Save(tempPath, System.Drawing.Imaging.ImageFormat.Png);
            }

            // Assuming ImageFile is a model class that takes a file path
            var imageFile = new ImageFile(tempPath);
            _navigationService.Navigate(CaptureToolNavigationRoutes.ImageEdit, imageFile);
        }
    }
}
