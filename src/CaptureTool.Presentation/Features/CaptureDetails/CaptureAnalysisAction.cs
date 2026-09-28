using CaptureTool.Domain.Analysis;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Features.CaptureDetails;

public sealed class CaptureAnalysisAction : ViewModelBase
{
    public AnalysisCapability Capability { get; }
    public string Label { get; internal set => Set(ref field, value); }
    public string ToolTip { get; internal set => Set(ref field, value); }
    public string Id { get; }
    public string LoadingId => Id + "_Loading";
    public bool IsRunning { get; private set => Set(ref field, value); }
    public bool HasResult { get; private set => Set(ref field, value); }
    public string Status { get; private set => Set(ref field, value); } = string.Empty;
    private bool _canRun;
    public IAsyncRelayCommand Command { get; }

    public CaptureAnalysisAction(AnalysisCapability capability, string id, string label, Func<Task> request)
    {
        Capability = capability; Id = id; Label = label; ToolTip = label;
        Command = new AsyncRelayCommand(request, () => _canRun);
    }

    public void Update(bool running, bool hasResult, bool canRun, string status = "")
    {
        IsRunning = running; HasResult = hasResult;
        Status = status;
        _canRun = canRun && !hasResult;
        Command.NotifyCanExecuteChanged();
    }
}
