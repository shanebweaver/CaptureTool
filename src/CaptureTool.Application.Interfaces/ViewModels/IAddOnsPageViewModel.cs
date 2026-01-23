using CaptureTool.Common.Commands;
using System.Windows.Input;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IAddOnsPageViewModel
{
    IAsyncCommand GetChromaKeyAddOnCommand { get; }
    ICommand GoBackCommand { get; }
    bool IsChromaKeyAddOnOwned { get; }
    string ChromaKeyAddOnPrice { get; }
    Uri? ChromaKeyAddOnLogoImage { get; }
    bool IsChromaKeyAddOnAvailable { get; }
    
    Task LoadAsync(CancellationToken cancellationToken);
}
