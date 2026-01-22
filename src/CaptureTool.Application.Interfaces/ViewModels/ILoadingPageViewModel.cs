using CaptureTool.Common.Commands;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface ILoadingPageViewModel
{
    RelayCommand GoBackCommand { get; }
}
