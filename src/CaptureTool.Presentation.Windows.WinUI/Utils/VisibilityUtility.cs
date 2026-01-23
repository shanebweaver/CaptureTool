using Microsoft.UI.Xaml;

namespace CaptureTool.Presentation.Windows.WinUI.Utils;

internal static partial class VisibilityUtility
{
    public static Visibility BoolToVisibility(bool? value)
    {
        return value == true ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility InverseBoolToVisibility(bool? value)
    {
        return value != true ? Visibility.Visible : Visibility.Collapsed;
    }
}
