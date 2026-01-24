using CaptureTool.Infrastructure.Interfaces.Commands;
using CaptureTool.Infrastructure.Interfaces.ViewModels;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface ILoadingPageViewModel : IViewModel
{
    IAppCommand GoBackCommand { get; }
}
