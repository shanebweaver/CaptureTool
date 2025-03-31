using CaptureTool.ViewModels;

namespace CaptureTool.UI.Xaml.Views;

public sealed partial class AppMenuView : AppMenuViewBase
{
    public AppMenuView()
    {
        InitializeComponent();
    }

    public override AppMenuViewModel ViewModel { get; } = ViewModelLocator.AppMenuView;
}
