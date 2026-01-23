using System.Windows.Input;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IErrorPageViewModel
{
    ICommand RestartAppCommand { get; }
}
