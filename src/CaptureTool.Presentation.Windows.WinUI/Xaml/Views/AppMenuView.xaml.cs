using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Domain.Capture;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
#if DEBUG
using CaptureTool.Presentation.Windows.WinUI.Debugging;
using Microsoft.UI.Xaml.Input;
using Windows.System;
#endif

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Views;

public sealed partial class AppMenuView : AppMenuViewBase
{
    private const uint RecentCaptureThumbnailSize = 32;
    private readonly ILogService _logService = App.Current.ServiceProvider.GetService<ILogService>();

    public AppMenuView()
    {
        InitializeComponent();
#if DEBUG
        AddDebugMenu();
#endif
        ViewModel.RecentCapturesUpdated += ViewModel_RecentCapturesUpdated;
        Loaded += AppMenuView_Loaded;
    }

#if DEBUG
    private void AddDebugMenu()
    {
        var modelLabItem = new MenuFlyoutItem
        {
            Text = "AI Model Lab…",
            Icon = new SymbolIcon(Symbol.Setting),
        };
        modelLabItem.KeyboardAccelerators.Add(new KeyboardAccelerator
        {
            Key = VirtualKey.M,
            Modifiers = VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift,
        });
        modelLabItem.Click += ShowAiModelLab_Click;

        var debugMenu = new MenuBarItem { Title = "Debug" };
        debugMenu.Items.Add(modelLabItem);
        MainMenuBar.Items.Add(debugMenu);
    }

    private async void ShowAiModelLab_Click(object sender, RoutedEventArgs e)
    {
        AiModelLabDialogService service =
            App.Current.ServiceProvider.GetService<AiModelLabDialogService>();
        await service.ShowAsync(XamlRoot);
    }
#endif

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

    private async Task LoadRecentCaptureThumbnailAsync(
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
            _logService.LogException(ex, $"Failed to load recent capture thumbnail for '{filePath}'.");
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
