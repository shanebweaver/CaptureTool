using System.Windows.Input;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IAddOnsPageViewModel
{
    ICommand GetChromaKeyAddOnCommand { get; }
    ICommand GoBackCommand { get; }
    bool IsChromaKeyAddOnOwned { get; }
    string ChromaKeyAddOnPrice { get; }
    Uri? ChromaKeyAddOnLogoImage { get; }
    bool IsChromaKeyAddOnAvailable { get; }
    
    Task LoadAsync(CancellationToken cancellationToken);
}
