using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Capture.Windows;

namespace CaptureTool.ViewModels;

internal sealed partial class CaptureOverlayViewModel : LoadableViewModelBase
{
    private bool _showOptions;
    public bool ShowOptions
    {
        get => _showOptions;
        set => Set(ref _showOptions, value);
    }

    private ObservableCollection<MonitorCaptureResult> _monitors;
    public ObservableCollection<MonitorCaptureResult> Monitors
    {
        get => _monitors;
        set => Set(ref _monitors, value);
    }

    public CaptureOverlayViewModel()
    {
        _monitors = [];
    }

    public override Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Monitors.Clear();
        //var monitors = MonitorCaptureHelper.
        //foreach (var monitor in monitors)
        //{
        //    Monitors.Add(monitor);
        //}


        return base.LoadAsync(parameter, cancellationToken);
    }

    public override void Unload()
    {
        base.Unload();
    }
}
