using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CaptureTool.UI.Windows.Xaml.Views;

public sealed partial class DiagnosticsView : DiagnosticsViewBase
{
    public DiagnosticsView()
    {
        InitializeComponent();
    }

    private async void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle)
        {
            await ViewModel.UpdateLoggingEnablementCommand.ExecuteAsync(toggle.IsOn);
        }
    }
}
