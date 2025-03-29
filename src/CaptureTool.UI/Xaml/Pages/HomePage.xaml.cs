using CaptureTool.ViewModels;

namespace CaptureTool.UI.Xaml.Pages;

public sealed partial class HomePage : HomePageBase
{
    public HomePage()
    {
        InitializeComponent();
    }

    public override HomePageViewModel ViewModel { get; } = App.Current.Ioc.GetService<HomePageViewModel>();
}
