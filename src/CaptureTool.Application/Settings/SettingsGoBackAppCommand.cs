using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Settings;

namespace CaptureTool.Application.Settings;

public sealed partial class SettingsGoBackAppCommand : ISettingsGoBackAppCommand
{
    private readonly IAppNavigation _appNavigation;

    public SettingsGoBackAppCommand(IAppNavigation appNavigation)
    {
        _appNavigation = appNavigation;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute()
    {
        return true;
    }

    public void Execute()
    {
        _appNavigation.GoBackOrGoHome();
    }
}
