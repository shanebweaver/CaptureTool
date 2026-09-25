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
    private string? _workingPath;
    private int _sourceVersion;
    private CaptureTextNavigationContext _navigation = new(false, false);
    private static int _preferredTab;
    public Func<CaptureTextLocation, bool>? Navigate { get; set; }
    public Action? ClearLocation { get; set; }

    public CaptureDetailsPane()
    {
        InitializeComponent();
        ContentTabs.SelectedIndex = _preferredTab;
        Loaded += (_, _) => Open();
        Unloaded += (_, _) => Release();
    }

    public void Configure(string? path, AnalysisMediaKind kind, bool active, bool edits, string? workingPath,
        int sourceVersion, CaptureTextNavigationContext navigation)
    {
        bool changed = !string.Equals(_path, path, StringComparison.OrdinalIgnoreCase) || _kind != kind ||
            _workingPath != workingPath || _sourceVersion != sourceVersion;
        if (changed || !active || navigation != _navigation) ClearLocation?.Invoke();
        _path = path; _kind = kind; _active = active; _edits = edits;
        _workingPath = workingPath; _sourceVersion = sourceVersion;
        bool becameReady = !_navigation.IsReady && navigation.IsReady;
        _navigation = navigation;
        if (changed || !active) Release();
        if (ViewModel != null)
        {
            ViewModel.HasEdits = edits;
            ViewModel.SetNavigationContext(navigation);
            if (becameReady) _ = ViewModel.RefreshAllAsync();
        }
        Open();
    }

    private void Open()
    {
        if (!_active || !IsLoaded || ViewModel != null || string.IsNullOrWhiteSpace(_path)) return;
        ViewModel = App.Current.ServiceProvider.GetService<CaptureDetailsViewModel>();
        ViewModel.HasEdits = _edits;
        ViewModel.SetNavigationContext(_navigation);
        ViewModel.TextContent.ScrollRequested += ScrollToPassage;
        ViewModel.TextContent.PropertyChanged += SelectionChanged;
        ViewModel.PropertyChanged += ViewModelChanged;
        PropertyChanged?.Invoke(this, new(nameof(ViewModel)));
        _ = ViewModel.OpenAsync(_path, _kind, _workingPath);
    }

    private void Release()
    {
        if (ViewModel != null)
        {
            ViewModel.TextContent.ScrollRequested -= ScrollToPassage;
            ViewModel.TextContent.PropertyChanged -= SelectionChanged;
            ViewModel.PropertyChanged -= ViewModelChanged;
            ViewModel.Dispose();
        }
        ClearLocation?.Invoke();
        ViewModel = null;
        PropertyChanged?.Invoke(this, new(nameof(ViewModel)));
    }

    private void Close_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
    private void ContentTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, ContentTabs) || !ReferenceEquals(e.OriginalSource, ContentTabs)) return;
        _preferredTab = ContentTabs.SelectedIndex;
        if (_preferredTab != 1) ClearLocation?.Invoke();
    }
    private void ScrollToPassage(CaptureTextPassage passage) => Passages.ScrollIntoView(passage, ScrollIntoViewAlignment.Leading);
    private void SelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CaptureTextViewModel.SelectedPassage)) ClearLocation?.Invoke();
    }
    private void ViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CaptureDetailsViewModel.Content) &&
            (ViewModel?.TextContent.SelectedPassage is not { } selected || !ViewModel.Content.Passages.Any(passage => passage.Id == selected.Id)))
            ClearLocation?.Invoke();
    }
    private async void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string value } && ViewModel != null) await ViewModel.CopyCommand.ExecuteAsync(value);
    }
    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: CaptureTextPassage { CanNavigate: true, SelectedLocation: { } location } passage })
        {
            if (ViewModel != null) ViewModel.TextContent.SelectedPassage = passage;
            if (Navigate?.Invoke(location) != true) ViewModel?.ReportActionFailure();
        }
    }
    private async void OpenLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: CaptureTextPassage { WebUri: { } uri } }) return;
        try { if (!await global::Windows.System.Launcher.LaunchUriAsync(uri)) ViewModel?.ReportActionFailure(); }
        catch (Exception) { ViewModel?.ReportActionFailure(); }
    }
    private Visibility TextVisibility(string? text) => string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    protected override AutomationPeer OnCreateAutomationPeer() => new CaptureDetailsPanePeer(this);
}

internal sealed partial class CaptureDetailsPanePeer(CaptureDetailsPane owner) : FrameworkElementAutomationPeer(owner)
{
    protected override string GetClassNameCore() => nameof(CaptureDetailsPane);
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;
}
