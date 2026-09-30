using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Domain.Analysis;
using CaptureTool.Presentation.Features.CaptureDetails;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Input;
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
    private static string _preferredTab = nameof(FileDetailsTab);
    private bool _updatingTabs = true;
    private CaptureTextViewModel? ActiveTextContent => ReferenceEquals(ContentTabs.SelectedItem, TextTab)
        ? ViewModel?.ShowVideoTimestamps == true ? null : ViewModel?.DisplayedTextContent :
        ReferenceEquals(ContentTabs.SelectedItem, SpeechTab) ? ViewModel?.SpeechContent : null;
    private CaptureTextPassage? _selectedPassage;
    private bool _navigationQueued;
    private bool _userNavigationQueued;
    private Task _openTask = Task.CompletedTask;
    private bool _extractTextRequested;
    public CaptureEditorTextSession? EditorText { get; set; }
    public Func<CaptureTextPassage, bool>? Navigate { get; set; }
    public Action? ClearLocation { get; set; }
    public Action<CaptureFileRename>? FileRenamed { get; set; }
    public Action<IReadOnlyList<CaptureTextPassage>?>? TextOverlayChanged { get; set; }

    public CaptureDetailsPane()
    {
        InitializeComponent();
        UpdateTabs();
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
        // Preserve filters and item bindings while the pane is temporarily closed.
        if (changed) Release();
        UpdateTabs();
        if (!active)
        {
            EditorText?.Cancel();
            TextOverlayChanged?.Invoke(null);
        }
        if (ViewModel != null)
        {
            ViewModel.IsActive = active;
            ViewModel.HasEdits = edits;
            ViewModel.SetNavigationContext(navigation);
            if (becameReady) _ = ViewModel.RefreshAllAsync();
        }
        Open();
    }

    private void UpdateTabs()
    {
        PivotItem[] tabs = _kind switch
        {
            AnalysisMediaKind.Audio => [FileDetailsTab, SpeechTab, SummaryTab],
            AnalysisMediaKind.Video => [FileDetailsTab, TextTab, SpeechTab, SummaryTab],
            _ => [FileDetailsTab, TextTab, SummaryTab]
        };
        if (ContentTabs.Items.Cast<PivotItem>().SequenceEqual(tabs)) { _updatingTabs = false; return; }
        _updatingTabs = true;
        foreach (var item in ContentTabs.Items.Cast<PivotItem>().Except(tabs).ToArray()) ContentTabs.Items.Remove(item);
        for (int index = 0; index < tabs.Length; index++)
            if (!ContentTabs.Items.Contains(tabs[index])) ContentTabs.Items.Insert(index, tabs[index]);
        ContentTabs.SelectedItem = tabs.FirstOrDefault(tab => tab.Name == _preferredTab) ?? FileDetailsTab;
        _updatingTabs = false;
        UpdateSelectedPassage();
        UpdateTextOverlay();
    }

    private void Open()
    {
        if (!_active || !IsLoaded || ViewModel != null || string.IsNullOrWhiteSpace(_path)) return;
        ViewModel = App.Current.ServiceProvider.GetService<CaptureDetailsViewModel>();
        ViewModel.FileRenamed += RenameCompleted;
        ViewModel.HasEdits = _edits;
        ViewModel.SetEditorText(EditorText);
        ViewModel.SetNavigationContext(_navigation);
        ViewModel.TextContent.ScrollRequested += ScrollToPassage;
        ViewModel.TextContent.PropertyChanged += SelectionChanged;
        ViewModel.VideoText.Content.ScrollRequested += ScrollToPassage;
        ViewModel.VideoText.Content.PropertyChanged += SelectionChanged;
        ViewModel.SpeechContent.ScrollRequested += ScrollToPassage;
        ViewModel.SpeechContent.PropertyChanged += SelectionChanged;
        ViewModel.PropertyChanged += ViewModelChanged;
        PropertyChanged?.Invoke(this, new(nameof(ViewModel)));
        _openTask = ViewModel.OpenAsync(_path, _kind, _workingPath);
        _ = ExtractRequestedTextAsync();
    }

    public void OpenText()
    {
        ContentTabs.SelectedItem = TextTab;
        _extractTextRequested = true;
        Open();
        _ = ExtractRequestedTextAsync();
    }

    private async Task ExtractRequestedTextAsync()
    {
        if (!_extractTextRequested || ViewModel == null) return;
        _extractTextRequested = false;
        var model = ViewModel;
        try
        {
            await _openTask;
            if (_active && ReferenceEquals(ContentTabs.SelectedItem, TextTab) && ReferenceEquals(model, ViewModel)) await model.EnsureTextAsync();
        }
        catch (OperationCanceledException) { }
    }

    private void Release()
    {
        _extractTextRequested = false;
        EditorText?.Cancel();
        if (_selectedPassage != null) _selectedPassage.PropertyChanged -= SelectedPassageChanged;
        _selectedPassage = null;
        if (ViewModel != null)
        {
            ViewModel.TextContent.ScrollRequested -= ScrollToPassage;
            ViewModel.TextContent.PropertyChanged -= SelectionChanged;
            ViewModel.VideoText.Content.ScrollRequested -= ScrollToPassage;
            ViewModel.VideoText.Content.PropertyChanged -= SelectionChanged;
            ViewModel.SpeechContent.ScrollRequested -= ScrollToPassage;
            ViewModel.SpeechContent.PropertyChanged -= SelectionChanged;
            ViewModel.PropertyChanged -= ViewModelChanged;
            ViewModel.Dispose();
        }
        ClearLocation?.Invoke();
        TextOverlayChanged?.Invoke(null);
        ViewModel = null;
        PropertyChanged?.Invoke(this, new(nameof(ViewModel)));
    }

    private void RenameCompleted(CaptureFileRename rename) => FileRenamed?.Invoke(rename);
    private void Close_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
    private void ContentTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingTabs || !ReferenceEquals(sender, ContentTabs) || !e.AddedItems.OfType<PivotItem>().Any(item => ReferenceEquals(item, ContentTabs.SelectedItem))) return;
        _preferredTab = ((PivotItem)ContentTabs.SelectedItem).Name;
        if (!ReferenceEquals(ContentTabs.SelectedItem, TextTab)) EditorText?.Cancel();
        _userNavigationQueued = false;
        ClearLocation?.Invoke();
        UpdateSelectedPassage();
        UpdateTextOverlay();
        RequestNavigation();
    }
    private void UpdateTextOverlay()
    {
        bool show = _active && ReferenceEquals(ContentTabs.SelectedItem, TextTab) && ViewModel?.CanShowImageTextOverlay == true;
        TextOverlayChanged?.Invoke(show ? ViewModel!.TextPassages : null);
        if (show) RequestNavigation();
    }
    private void ScrollToPassage(CaptureTextPassage passage)
    {
        if (ActiveTextContent?.SelectedPassage != passage) return;
        (passage.Source == CaptureTextSource.Speech ? SpeechPassages : Passages).ScrollIntoView(passage, ScrollIntoViewAlignment.Leading);
        RequestNavigation(userInitiated: true);
    }
    private void SelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CaptureTextViewModel.SelectedPassage) || !ReferenceEquals(sender, ActiveTextContent)) return;
        UpdateSelectedPassage();
        RequestNavigation(userInitiated: _selectedPassage != null);
    }
    private void UpdateSelectedPassage()
    {
        if (_selectedPassage != null) _selectedPassage.PropertyChanged -= SelectedPassageChanged;
        _selectedPassage = ActiveTextContent?.SelectedPassage;
        if (_selectedPassage != null) _selectedPassage.PropertyChanged += SelectedPassageChanged;
    }
    private void SelectedPassageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CaptureTextPassage.SelectedLocation) or nameof(CaptureTextPassage.CanNavigate))
            RequestNavigation(userInitiated: e.PropertyName == nameof(CaptureTextPassage.SelectedLocation));
    }
    private void RequestNavigation(bool userInitiated = false)
    {
        _userNavigationQueued |= userInitiated;
        // ItemClick and selection bindings can both fire for the same interaction.
        if (_navigationQueued) return;
        _navigationQueued = DispatcherQueue.TryEnqueue(() =>
        {
            _navigationQueued = false;
            bool requested = _userNavigationQueued;
            _userNavigationQueued = false;
            if (!_active || ActiveTextContent is not { } content) return;
            ClearLocation?.Invoke();
            if (content.SelectedPassage is { CanNavigate: true } passage &&
                Navigate?.Invoke(passage) != true)
                ViewModel?.ReportActionFailure();
            else if (requested && content.SelectedPassage is { CanNavigate: false })
                ViewModel?.ReportLocationUnavailable();
        });
    }
    private void ViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ViewModel?.IsVideo == true && e.PropertyName == nameof(CaptureDetailsViewModel.ShowVideoTimestamps))
        {
            UpdateSelectedPassage();
            ClearLocation?.Invoke();
        }
        if (e.PropertyName is nameof(CaptureDetailsViewModel.TextPassages) or nameof(CaptureDetailsViewModel.CanShowImageTextOverlay)) UpdateTextOverlay();
        if (e.PropertyName == nameof(CaptureDetailsViewModel.IsEditingName) && ViewModel?.IsEditingName == true)
            DispatcherQueue.TryEnqueue(() => { if (_active && ViewModel?.IsEditingName == true) { NameInput.Focus(FocusState.Programmatic); NameInput.SelectAll(); } });
        else if (e.PropertyName == nameof(CaptureDetailsViewModel.IsEditingName))
            DispatcherQueue.TryEnqueue(() => { if (_active) EditNameButton.Focus(FocusState.Programmatic); });
        if (e.PropertyName is nameof(CaptureDetailsViewModel.TextPassages) or nameof(CaptureDetailsViewModel.SpeechPassages) &&
            ActiveTextContent?.SelectedPassage == null)
            ClearLocation?.Invoke();
    }
    private async void NameInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (ViewModel == null) return;
        if (e.Key == global::Windows.System.VirtualKey.Enter && ViewModel.SaveNameCommand.CanExecute(null))
        {
            e.Handled = true;
            await ViewModel.SaveNameCommand.ExecuteAsync(null);
        }
        else if (e.Key == global::Windows.System.VirtualKey.Escape)
        {
            e.Handled = true;
            ViewModel.CancelNameCommand.Execute(null);
        }
    }
    private async void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string value } && ViewModel != null) await ViewModel.CopyCommand.ExecuteAsync(value);
    }
    private void Passages_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CaptureTextPassage passage && ViewModel != null)
        {
            (passage.Source == CaptureTextSource.Speech ? ViewModel.SpeechContent : ViewModel.DisplayedTextContent).SelectedPassage = passage;
            RequestNavigation(userInitiated: true);
        }
    }
    private void VideoFrames_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not CaptureVideoTextFrame frame || ViewModel == null) return;
        ViewModel.VideoText.Open(frame);
        UpdateSelectedPassage();
        RequestNavigation(userInitiated: true);
        DispatcherQueue.TryEnqueue(() => VideoFrameBack.Focus(FocusState.Programmatic));
    }
    private void VideoFrameBack_Click(object sender, RoutedEventArgs e)
    {
        var selected = ViewModel?.VideoText.SelectedFrame;
        ViewModel?.VideoText.BackCommand.Execute(null);
        DispatcherQueue.TryEnqueue(() =>
        {
            if (selected != null)
            {
                VideoFrames.SelectedItem = selected;
                VideoFrames.ScrollIntoView(selected, ScrollIntoViewAlignment.Leading);
            }
            VideoFrames.Focus(FocusState.Programmatic);
        });
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
