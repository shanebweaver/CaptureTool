using CaptureTool.Common;
using CaptureTool.Infrastructure.Interfaces.Commands;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface ILoadingPageViewModel : IViewModel
{
    IAppCommand GoBackCommand { get; }
}
