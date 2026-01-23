using CaptureTool.Infrastructure.Implementations.Windows.Commands;
using CaptureTool.Infrastructure.Interfaces.Commands;
using System.Windows.Input;

namespace CaptureTool.Presentation.Windows.WinUI.Helpers;

/// <summary>
/// Static helper class for converting app commands to ICommand for XAML binding.
/// Usage in XAML: Command="{x:Bind helpers:XamlCommandHelpers.ToICommand(ViewModel.SomeCommand)}"
/// </summary>
public static class XamlCommandHelpers
{
    public static ICommand ToICommand(IAppCommand cmd) => cmd.ToICommand();
    public static ICommand ToICommand<T>(IAppCommand<T> cmd) => cmd.ToICommand();
    public static ICommand ToICommand(IAsyncAppCommand cmd) => cmd.ToICommand();
    public static ICommand ToICommand<T>(IAsyncAppCommand<T> cmd) => cmd.ToICommand();
}
