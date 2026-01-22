using CaptureTool.Common.Commands;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IErrorPageViewModel
{
    RelayCommand RestartAppCommand { get; }
}
