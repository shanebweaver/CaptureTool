using CaptureTool.MetadataScanner.Windows.WinUI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace CaptureTool.MetadataScanner.Windows.WinUI.Xaml.Pages;

public sealed partial class MainPage : Page
{
    public MainPage(MainPageViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public MainPageViewModel ViewModel { get; }
}
