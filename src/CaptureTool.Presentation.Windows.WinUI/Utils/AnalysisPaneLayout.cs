using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CaptureTool.Presentation.Windows.WinUI.Utils;

internal static class AnalysisPaneLayout
{
    public static void Update(object sender, SizeChangedEventArgs args)
    {
        if (sender is not SplitView splitView || args.NewSize.Width <= 0) { return; }

        splitView.OpenPaneLength = Math.Min(380, args.NewSize.Width);
        SplitViewDisplayMode mode = args.NewSize.Width >= 1000
            ? SplitViewDisplayMode.Inline
            : SplitViewDisplayMode.Overlay;
        if (splitView.DisplayMode != mode)
        {
            bool wasOpen = splitView.IsPaneOpen;
            splitView.DisplayMode = mode;
            splitView.IsPaneOpen = wasOpen;
        }
    }
}
