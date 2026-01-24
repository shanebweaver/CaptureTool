using CaptureTool.Infrastructure.Interfaces.Commands;
using CaptureTool.Infrastructure.Interfaces.ViewModels;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IErrorPageViewModel : IViewModel
{
    IAppCommand RestartAppCommand { get; }
}
