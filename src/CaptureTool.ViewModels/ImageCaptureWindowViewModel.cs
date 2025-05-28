using CaptureTool.Capture.Image;
using CaptureTool.Capture.Windows;
using CaptureTool.Common.Commands;
using CaptureTool.Core;
using CaptureTool.Core.AppController;
using CaptureTool.Services.Navigation;
using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

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
            // Find the monitor that contains the capture area
            var area = CaptureArea;
            var monitor = monitors.FirstOrDefault(m =>
                area.Left >= m.Left &&
                area.Top >= m.Top &&
                area.Right <= m.Left + m.Width &&
                area.Bottom <= m.Top + m.Height);

            monitor ??= monitors[0]; // fallback

            float scale = monitor.Scale;
            int cropX = (int)((area.Left - monitor.Left) * scale);
            int cropY = (int)((area.Top - monitor.Top) * scale);
            int cropWidth = (int)(area.Width * scale);
            int cropHeight = (int)(area.Height * scale);

            // Create a bitmap for the full monitor
            using var fullBmp = new Bitmap(monitor.Width, monitor.Height, PixelFormat.Format32bppArgb);
            var bmpData = fullBmp.LockBits(
                new Rectangle(0, 0, monitor.Width, monitor.Height),
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
            using var croppedBmp = fullBmp.Clone(new Rectangle(cropX, cropY, cropWidth, cropHeight), fullBmp.PixelFormat);
            var tempPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Temp",
                $"capture_{Guid.NewGuid()}.png"
            );

            Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);
            croppedBmp.Save(tempPath, System.Drawing.Imaging.ImageFormat.Png);

            var imageFile = new ImageFile(tempPath);
            _navigationService.Navigate(CaptureToolNavigationRoutes.ImageEdit, imageFile);
        }
    }
}
