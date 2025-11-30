using CaptureTool.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace CaptureTool.UI.Windows.Xaml.Views;

public sealed partial class AppMenuView : AppMenuViewBase
{
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
                    string iconGlyph = recentCapture.CaptureFileType switch
                    {
                        CaptureFileType.Image => "\uE722", // Image icon
                        CaptureFileType.Video => "\uE714", // Video icon
                        _ => "\uE7C3" // Generic file icon
                    };

                    MenuFlyoutItem recentCaptureItem = new()
                    {
                        Icon = new FontIcon { Glyph = iconGlyph },
                        Text = recentCapture.FileName,
                        Command = ViewModel.OpenRecentCaptureCommand,
                        CommandParameter = recentCapture
                    };

                    RecentCapturesSubMenu.Items.Add(recentCaptureItem);
                }
            }
        });
    }
}
