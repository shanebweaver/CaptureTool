using Microsoft.UI.Xaml;

namespace CaptureTool.UI.Windows.Utils;

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
