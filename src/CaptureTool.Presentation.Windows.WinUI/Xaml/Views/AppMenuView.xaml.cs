using CaptureTool.Domain.Capture;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Views;

public sealed partial class AppMenuView : AppMenuViewBase
{
    private const uint RecentCaptureThumbnailSize = 32;

    public AppMenuView()
    {
        InitializeComponent();
        ViewModel.RecentCapturesUpdated += ViewModel_RecentCapturesUpdated;
        Loaded += AppMenuView_Loaded;
    }

    ~AppMenuView()
    {
        ViewModel.RecentCapturesUpdated -= ViewModel_RecentCapturesUpdated;
        Loaded -= AppMenuView_Loaded;
    }

    private void ViewModel_RecentCapturesUpdated(object? sender, EventArgs e)
    {
        ReloadRecentCapturesMenu();
    }

    private void AppMenuView_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ReloadRecentCapturesMenu();
    }

    private void ReloadRecentCapturesMenu()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (ViewModel.IsLoaded)
            {
                RecentCapturesSubMenu.Items.Clear();

                if (!ViewModel.RecentCaptures.Any())
                {
                    RecentCapturesSubMenu.Items.Add(new MenuFlyoutItem()
                    {
                        Text = "None",
                        IsEnabled = false
                    });
                    return;
                }

                foreach (var recentCapture in ViewModel.RecentCaptures)
                {
                    MenuFlyoutItem recentCaptureItem = new()
                    {
                        Icon = CreateFallbackIcon(recentCapture.CaptureFileType),
                        Text = recentCapture.FileName,
                        Command = ViewModel.OpenRecentCaptureCommand,
                        CommandParameter = recentCapture
                    };
                    ToolTipService.SetToolTip(recentCaptureItem, recentCapture.FileName);

                    RecentCapturesSubMenu.Items.Add(recentCaptureItem);

                    if (CanLoadRecentCaptureThumbnail(recentCapture.CaptureFileType))
                    {
                        _ = LoadRecentCaptureThumbnailAsync(recentCapture.FilePath, recentCapture.CaptureFileType, recentCaptureItem);
                    }
                }
            }
        });
    }

    private static async Task LoadRecentCaptureThumbnailAsync(
        string filePath,
        CaptureFileType fileType,
        MenuFlyoutItem recentCaptureItem)
    {
        if (!CanLoadRecentCaptureThumbnail(fileType))
        {
            recentCaptureItem.Icon = CreateFallbackIcon(fileType);
            return;
        }

        try
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
            using var thumbnail = await file.GetThumbnailAsync(
                ThumbnailMode.SingleItem,
                RecentCaptureThumbnailSize,
                ThumbnailOptions.UseCurrentScale);

            if (thumbnail.Size == 0)
            {
                return;
            }

            BitmapImage thumbnailImage = new();
            await thumbnailImage.SetSourceAsync(thumbnail);
            recentCaptureItem.Icon = new ImageIcon
            {
                Width = RecentCaptureThumbnailSize,
                Height = RecentCaptureThumbnailSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Source = thumbnailImage
            };
        }
        catch (Exception ex)
        {
            AppServiceLocator.Logging.LogException(ex, $"Failed to load recent capture thumbnail for '{filePath}'.");
            recentCaptureItem.Icon = CreateFallbackIcon(fileType);
        }
    }

    private static bool CanLoadRecentCaptureThumbnail(CaptureFileType fileType)
    {
        return fileType is CaptureFileType.Image or CaptureFileType.Video;
    }

    private static FontIcon CreateFallbackIcon(CaptureFileType fileType)
    {
        string iconGlyph = fileType switch
        {
            CaptureFileType.Image => "\uE722", // Image icon
            CaptureFileType.Video => "\uE714", // Video icon
            CaptureFileType.Audio => "\uE720", // Microphone icon
            _ => "\uE7C3" // Generic file icon
        };

        return new FontIcon
        {
            Width = RecentCaptureThumbnailSize,
            Height = RecentCaptureThumbnailSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Glyph = iconGlyph
        };
    }
}
