using CaptureTool.Common.Commands;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IAddOnsPageViewModel
{
    AsyncRelayCommand GetChromaKeyAddOnCommand { get; }
    RelayCommand GoBackCommand { get; }
    bool IsChromaKeyAddOnOwned { get; }
    string ChromaKeyAddOnPrice { get; }
    Uri? ChromaKeyAddOnLogoImage { get; }
    bool IsChromaKeyAddOnAvailable { get; }
    
    Task LoadAsync(CancellationToken cancellationToken);
}
