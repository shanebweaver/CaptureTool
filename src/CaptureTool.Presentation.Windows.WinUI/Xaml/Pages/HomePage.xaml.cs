namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Pages;

public sealed partial class HomePage : HomePageBase
{
    private bool _storeReviewPromptPending;

    public HomePage()
    {
        InitializeComponent();
        Loaded += HomePage_Loaded;
        ViewModel.StoreReviewPromptRequested += ViewModel_StoreReviewPromptRequested;
    }

    private void HomePage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
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

    private async Task ShowStoreReviewPromptAsync()
    {
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

        Microsoft.UI.Xaml.Controls.ContentDialogResult result = await dialog.ShowAsync();
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
}
