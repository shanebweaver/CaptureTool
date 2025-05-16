using CaptureTool.Services.Navigation;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels;

public sealed partial class ErrorPageViewModel : LoadableViewModelBase
{
    private readonly INavigationService _navigationService;

    public ErrorPageViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public override Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Debug.Assert(IsUnloaded);
        StartLoading();

        return LoadAsync(parameter, cancellationToken);
    }
}
