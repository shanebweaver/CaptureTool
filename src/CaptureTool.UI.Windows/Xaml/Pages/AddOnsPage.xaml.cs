using CaptureTool.ViewModels;
using Microsoft.UI.Xaml.Media.Imaging;
using System.ComponentModel;

namespace CaptureTool.UI.Windows.Xaml.Pages;

public sealed partial class AddOnsPage : AddOnsPageBase
{
    public AddOnsPage()
    {
        InitializeComponent();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AddOnsPageViewModel.ChromaKeyAddOnLogoImage):
                if (ViewModel.ChromaKeyAddOnLogoImage != null)
                {
                    ChromaKeyAddOnLogoImage.Source = new BitmapImage(ViewModel.ChromaKeyAddOnLogoImage);
                }
                else
                {
                    ChromaKeyAddOnLogoImage.Source = null;
                }
                break;
        }
    }
}