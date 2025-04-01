using Microsoft.UI.Xaml.Controls;

namespace CaptureTool.UI.Xaml.Views;

public sealed partial class AppTitleBarView : AppTitleBarViewBase
{
    public AppTitleBarView()
    {
        InitializeComponent();
    }

    private void TitleBar_BackRequested(TitleBar sender, object args)
    {
        ViewModel.GoBackCommand.Execute(null);
    }
}
