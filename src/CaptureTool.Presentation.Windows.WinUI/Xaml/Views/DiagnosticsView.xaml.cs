using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Views;

public sealed partial class DiagnosticsView : DiagnosticsViewBase
{
    public DiagnosticsView()
    {
        InitializeComponent();
    }

    private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle)
        {
            _ = ViewModel.UpdateLoggingEnablementCommand.ExecuteAsync(toggle.IsOn);
        }
    }
}
