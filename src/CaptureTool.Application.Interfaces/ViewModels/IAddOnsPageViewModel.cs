using CaptureTool.Common;
using CaptureTool.Infrastructure.Interfaces.Commands;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IAddOnsPageViewModel : IViewModel
{
    IAsyncAppCommand GetChromaKeyAddOnCommand { get; }
    IAppCommand GoBackCommand { get; }
    bool IsChromaKeyAddOnOwned { get; }
    string ChromaKeyAddOnPrice { get; }
    Uri? ChromaKeyAddOnLogoImage { get; }
    bool IsChromaKeyAddOnAvailable { get; }
    
    Task LoadAsync(CancellationToken cancellationToken);
}
