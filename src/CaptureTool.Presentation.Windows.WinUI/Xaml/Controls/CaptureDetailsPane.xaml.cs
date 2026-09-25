using CaptureTool.Domain.Analysis;
using CaptureTool.Presentation.Features.CaptureDetails;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation.Peers;
using System.ComponentModel;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class CaptureDetailsPane : UserControl, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? CloseRequested;
    public CaptureDetailsViewModel? ViewModel { get; private set; }
    private string? _path;
    private AnalysisMediaKind _kind;
    private bool _active;
    private bool _edits;

    public CaptureDetailsPane()
    {
        InitializeComponent();
        Loaded += (_, _) => Open();
        Unloaded += (_, _) => Release();
    }

    public void Configure(string? path, AnalysisMediaKind kind, bool active, bool edits)
    {
        bool changed = !string.Equals(_path, path, StringComparison.OrdinalIgnoreCase) || _kind != kind;
        _path = path; _kind = kind; _active = active; _edits = edits;
        if (changed || !active) Release();
        if (ViewModel != null) ViewModel.HasEdits = edits;
        Open();
    }

    private void Open()
    {
        if (!_active || !IsLoaded || ViewModel != null || string.IsNullOrWhiteSpace(_path)) return;
        ViewModel = App.Current.ServiceProvider.GetService<CaptureDetailsViewModel>();
        ViewModel.HasEdits = _edits;
        PropertyChanged?.Invoke(this, new(nameof(ViewModel)));
        _ = ViewModel.OpenAsync(_path, _kind);
    }

    private void Release()
    {
        ViewModel?.Dispose();
        ViewModel = null;
        PropertyChanged?.Invoke(this, new(nameof(ViewModel)));
    }

    private void Close_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
    private Visibility TextVisibility(string? text) => string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    protected override AutomationPeer OnCreateAutomationPeer() => new CaptureDetailsPanePeer(this);
}

internal sealed partial class CaptureDetailsPanePeer(CaptureDetailsPane owner) : FrameworkElementAutomationPeer(owner)
{
    protected override string GetClassNameCore() => nameof(CaptureDetailsPane);
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;
}
