using CaptureTool.Common;
using CaptureTool.Infrastructure.Interfaces.Navigation;
using CaptureTool.Infrastructure.Interfaces.Themes;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IMainWindowViewModel : IViewModel, INavigationHandler
{
    event EventHandler<INavigationRequest>? NavigationRequested;
    
    AppTheme CurrentAppTheme { get; }
    AppTheme DefaultAppTheme { get; }
}
