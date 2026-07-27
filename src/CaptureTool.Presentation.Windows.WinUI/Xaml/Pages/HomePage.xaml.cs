using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Presentation.Features.RecentCaptures;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Pages;

public sealed partial class HomePage : HomePageBase
{
    private const uint RecentCaptureThumbnailSize = 360;
    private const double RecentCaptureMinimumCompactSlotWidth = 136;
    private const double RecentCaptureMinimumSlotWidth = 176;
    private const double RecentCaptureMaximumSlotWidth = 248;
    private const double RecentCaptureMinimumSlotHeight = 176;
    private const double RecentCaptureMaximumSlotHeight = 232;
    private const int RecentCaptureMaximumColumns = 4;

    private readonly ILogService _logService = App.Current.ServiceProvider.GetService<ILogService>();
    private readonly Dictionary<string, BitmapImage> _thumbnailCache = new(StringComparer.OrdinalIgnoreCase);
    private ContentDialog? _activeStoreReviewDialog;
    private bool _storeReviewPromptPending;
    private int _storeReviewPromptGeneration;

    public HomePage()
    {
        InitializeComponent();
        Loaded += HomePage_Loaded;
        ViewModel.StoreReviewPromptRequested += ViewModel_StoreReviewPromptRequested;
    }

    private void HomePage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        AmbientMotionStoryboard.Begin();
        DispatcherQueue.TryEnqueue(() => UpdateRecentCaptureGridItemSize(RecentCapturesGridView.ActualWidth));

        if (!_storeReviewPromptPending)
        {
            return;
        }

        _storeReviewPromptPending = false;
        _ = ShowStoreReviewPromptAsync();
    }

    private void ViewModel_StoreReviewPromptRequested(object? sender, EventArgs e)
    {
        if (!IsLoaded)
        {
            _storeReviewPromptPending = true;
            return;
        }

        _ = ShowStoreReviewPromptAsync();
    }

    internal void SuppressStoreReviewPrompt()
    {
        _storeReviewPromptPending = false;
        _storeReviewPromptGeneration++;

        try
        {
            _activeStoreReviewDialog?.Hide();
        }
        catch (InvalidOperationException)
        {
            // The dialog can finish closing between the null check and Hide.
        }
    }

    private void HomeScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        const double LoadMoreThreshold = 520;

        if (e.IsIntermediate || HomeScrollViewer.ScrollableHeight <= 0)
        {
            return;
        }

        double distanceToBottom = HomeScrollViewer.ScrollableHeight - HomeScrollViewer.VerticalOffset;
        if (distanceToBottom <= LoadMoreThreshold && ViewModel.LoadMoreRecentCapturesCommand.CanExecute(null))
        {
            _ = ViewModel.LoadMoreRecentCapturesCommand.ExecuteAsync(null);
        }
    }

    private void RecentCapturesGridView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateRecentCaptureGridItemSize(e.NewSize.Width);
    }

    private void UpdateRecentCaptureGridItemSize(double availableWidth)
    {
        if (availableWidth <= 0 || RecentCapturesGridView.ItemsPanelRoot is not ItemsWrapGrid itemsWrapGrid)
        {
            return;
        }

        double usableWidth = Math.Floor(availableWidth);
        int columnCount = Math.Max(1, (int)Math.Floor(usableWidth / RecentCaptureMinimumSlotWidth));
        columnCount = Math.Min(columnCount, RecentCaptureMaximumColumns);

        double itemWidth = Math.Floor(usableWidth / columnCount);
        itemWidth = Math.Min(itemWidth, RecentCaptureMaximumSlotWidth);
        if (usableWidth < RecentCaptureMinimumCompactSlotWidth)
        {
            itemWidth = usableWidth;
        }
        else
        {
            itemWidth = Math.Max(itemWidth, RecentCaptureMinimumCompactSlotWidth);
        }

        double itemHeight = Math.Clamp(
            itemWidth * .94,
            RecentCaptureMinimumSlotHeight,
            RecentCaptureMaximumSlotHeight);

        itemsWrapGrid.ItemWidth = itemWidth;
        itemsWrapGrid.ItemHeight = itemHeight;
    }

    private void RecentCapturesGridView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is RecentCaptureViewModel recentCapture)
        {
            _ = ViewModel.OpenRecentCaptureCommand.ExecuteAsync(recentCapture);
        }
    }

    private void RecentCapturesGridView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.Item is not RecentCaptureViewModel recentCapture || args.ItemContainer is not SelectorItem itemContainer)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() => _ = LoadRecentCaptureThumbnailAsync(itemContainer, recentCapture));
    }

    private async Task LoadRecentCaptureThumbnailAsync(SelectorItem itemContainer, RecentCaptureViewModel recentCapture)
    {
        Image? previewImage = FindDescendant<Image>(itemContainer, "RecentCapturePreviewImage");
        Grid? fallback = FindDescendant<Grid>(itemContainer, "RecentCaptureFallback");

        if (previewImage == null || fallback == null)
        {
            return;
        }

        previewImage.Opacity = 0;
        previewImage.Source = null;
        fallback.Visibility = Visibility.Visible;

        if (!recentCapture.CanLoadThumbnail)
        {
            return;
        }

        try
        {
            BitmapImage? thumbnail = await GetThumbnailImageAsync(recentCapture.FilePath);
            if (thumbnail == null || RecentCapturesGridView.ItemFromContainer(itemContainer) != recentCapture)
            {
                return;
            }

            previewImage.Source = thumbnail;
            previewImage.Opacity = 1;
            fallback.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, $"Failed to load recent capture thumbnail for '{recentCapture.FilePath}'.");
        }
    }

    private async Task<BitmapImage?> GetThumbnailImageAsync(string filePath)
    {
        if (_thumbnailCache.TryGetValue(filePath, out BitmapImage? cachedThumbnail))
        {
            return cachedThumbnail;
        }

        StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
        using var thumbnail = await file.GetThumbnailAsync(
            ThumbnailMode.SingleItem,
            RecentCaptureThumbnailSize,
            ThumbnailOptions.UseCurrentScale);

        if (thumbnail.Size == 0)
        {
            return null;
        }

        BitmapImage thumbnailImage = new();
        await thumbnailImage.SetSourceAsync(thumbnail);
        _thumbnailCache[filePath] = thumbnailImage;
        return thumbnailImage;
    }

    private static T? FindDescendant<T>(DependencyObject parent, string name)
        where T : FrameworkElement
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int index = 0; index < childCount; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is T element && element.Name == name)
            {
                return element;
            }

            T? descendant = FindDescendant<T>(child, name);
            if (descendant != null)
            {
                return descendant;
            }
        }

        return null;
    }

    private async Task ShowStoreReviewPromptAsync()
    {
        if (_activeStoreReviewDialog is not null)
        {
            return;
        }

        int promptGeneration = _storeReviewPromptGeneration;
        Microsoft.Windows.ApplicationModel.Resources.ResourceLoader resourceLoader = new();
        Microsoft.UI.Xaml.Controls.ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = resourceLoader.GetString("StoreReviewPrompt_Title"),
            Content = new Microsoft.UI.Xaml.Controls.TextBlock
            {
                Text = resourceLoader.GetString("StoreReviewPrompt_Content"),
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords
            },
            PrimaryButtonText = resourceLoader.GetString("StoreReviewPrompt_ReviewButton"),
            SecondaryButtonText = resourceLoader.GetString("StoreReviewPrompt_DontShowAgainButton"),
            CloseButtonText = resourceLoader.GetString("StoreReviewPrompt_RemindLaterButton"),
            Style = Microsoft.UI.Xaml.Application.Current.Resources["DefaultContentDialogStyle"] as Microsoft.UI.Xaml.Style,
            DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Primary
        };
        _activeStoreReviewDialog = dialog;

        try
        {
            Microsoft.UI.Xaml.Controls.ContentDialogResult result = await dialog.ShowAsync();
            if (promptGeneration != _storeReviewPromptGeneration)
            {
                return;
            }

            switch (result)
            {
                case Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary:
                    await ViewModel.LeaveStoreReviewCommand.ExecuteAsync(null);
                    break;

                case Microsoft.UI.Xaml.Controls.ContentDialogResult.Secondary:
                    await ViewModel.DisableStoreReviewRemindersCommand.ExecuteAsync(null);
                    break;

                default:
                    await ViewModel.RemindStoreReviewLaterCommand.ExecuteAsync(null);
                    break;
            }
        }
        finally
        {
            _activeStoreReviewDialog = null;
        }
    }
}
