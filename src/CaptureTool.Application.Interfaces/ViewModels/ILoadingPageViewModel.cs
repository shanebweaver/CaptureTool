using System.Windows.Input;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface ILoadingPageViewModel
{
    ICommand GoBackCommand { get; }
}
