using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel()
    {

    }

    public override Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        StartLoading();
        return base.LoadAsync(parameter, cancellationToken);
    }

    public override void Unload()
    {
        base.Unload();
    }
}
