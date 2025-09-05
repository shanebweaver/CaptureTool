using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;

namespace CaptureTool.ViewModels;

public sealed partial class CaptureOverlayViewModel : ViewModelBase
{
    private readonly IAppController _appController;

    public RelayCommand CloseOverlayCommand => new(CloseOverlay);

    public CaptureOverlayViewModel(IAppController appController) 
    {
        _appController = appController;
    }

    private void CloseOverlay()
    {
        _appController.CloseCaptureOverlay();
        _appController.ShowSelectionOverlay();
    }
}
