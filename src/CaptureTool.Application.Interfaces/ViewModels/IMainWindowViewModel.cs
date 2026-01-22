using CaptureTool.Infrastructure.Interfaces.Navigation;
using CaptureTool.Infrastructure.Interfaces.Themes;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IMainWindowViewModel : INavigationHandler
{
    event EventHandler<INavigationRequest>? NavigationRequested;
    
    AppTheme CurrentAppTheme { get; }
    AppTheme DefaultAppTheme { get; }
}
