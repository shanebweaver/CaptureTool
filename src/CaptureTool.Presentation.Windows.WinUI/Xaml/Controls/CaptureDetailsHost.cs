using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Domain.Analysis;
using CaptureTool.Presentation.Features.CaptureDetails;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

/// <summary>Shared docked editor details mode. The pane preference lasts for the app session.</summary>
public sealed partial class CaptureDetailsHost : SplitView
{
    private static bool _preferredOpen;
    private readonly CaptureDetailsPane _details;
    private AppBarToggleButton? _toggleControl;
    public static readonly DependencyProperty SourcePathProperty = DependencyProperty.Register(nameof(SourcePath), typeof(string), typeof(CaptureDetailsHost), new(null, Changed));
    public static readonly DependencyProperty MediaKindProperty = DependencyProperty.Register(nameof(MediaKind), typeof(AnalysisMediaKind), typeof(CaptureDetailsHost), new(AnalysisMediaKind.Image, Changed));
    public static readonly DependencyProperty HasEditsProperty = DependencyProperty.Register(nameof(HasEdits), typeof(bool), typeof(CaptureDetailsHost), new(false, Changed));
    public static readonly DependencyProperty WorkingPathProperty = DependencyProperty.Register(nameof(WorkingPath), typeof(string), typeof(CaptureDetailsHost), new(null, Changed));
    public static readonly DependencyProperty SourceVersionProperty = DependencyProperty.Register(nameof(SourceVersion), typeof(int), typeof(CaptureDetailsHost), new(0, Changed));
    public static readonly DependencyProperty IsSourceReadyProperty = DependencyProperty.Register(nameof(IsSourceReady), typeof(bool), typeof(CaptureDetailsHost), new(false, Changed));
    public static readonly DependencyProperty ImageEditedProperty = DependencyProperty.Register(nameof(ImageEdited), typeof(bool), typeof(CaptureDetailsHost), new(false, Changed));
    public static readonly DependencyProperty StartSecondsProperty = DependencyProperty.Register(nameof(StartSeconds), typeof(double), typeof(CaptureDetailsHost), new(0d, Changed));
    public static readonly DependencyProperty EndSecondsProperty = DependencyProperty.Register(nameof(EndSeconds), typeof(double), typeof(CaptureDetailsHost), new(double.MaxValue, Changed));
    public string? SourcePath { get => (string?)GetValue(SourcePathProperty); set => SetValue(SourcePathProperty, value); }
    public AnalysisMediaKind MediaKind { get => (AnalysisMediaKind)GetValue(MediaKindProperty); set => SetValue(MediaKindProperty, value); }
    public bool HasEdits { get => (bool)GetValue(HasEditsProperty); set => SetValue(HasEditsProperty, value); }
    public AppBarToggleButton? ToggleControl
    {
        get => _toggleControl;
        set
        {
            if (_toggleControl != null) { _toggleControl.Checked -= ToggleChanged; _toggleControl.Unchecked -= ToggleChanged; }
            _toggleControl = value;
            if (_toggleControl != null) { _toggleControl.Checked += ToggleChanged; _toggleControl.Unchecked += ToggleChanged; }
        }
    }
    public Action? ActivateMode { get; set; }
    public string? WorkingPath { get => (string?)GetValue(WorkingPathProperty); set => SetValue(WorkingPathProperty, value); }
    public int SourceVersion { get => (int)GetValue(SourceVersionProperty); set => SetValue(SourceVersionProperty, value); }
    public bool IsSourceReady { get => (bool)GetValue(IsSourceReadyProperty); set => SetValue(IsSourceReadyProperty, value); }
    public bool ImageEdited { get => (bool)GetValue(ImageEditedProperty); set => SetValue(ImageEditedProperty, value); }
    public double StartSeconds { get => (double)GetValue(StartSecondsProperty); set => SetValue(StartSecondsProperty, value); }
    public double EndSeconds { get => (double)GetValue(EndSecondsProperty); set => SetValue(EndSecondsProperty, value); }
    public Func<CaptureTextPassage, bool>? Navigate { get; set; }
    public Action? ClearLocation { get; set; }
    public Action<CaptureFileRename>? FileRenamed { get; set; }
    public Action<IReadOnlyList<CaptureTextPassage>?>? TextOverlayChanged { get; set; }
    public CaptureEditorTextSession? EditorText { get => _details.EditorText; set => _details.EditorText = value; }

    public CaptureDetailsHost()
    {
        _details = new CaptureDetailsPane();
        _details.Navigate = passage => Navigate?.Invoke(passage) == true;
        _details.FileRenamed = rename => FileRenamed?.Invoke(rename);
        _details.ClearLocation = () => ClearLocation?.Invoke();
        _details.TextOverlayChanged = passages => TextOverlayChanged?.Invoke(passages);
        Pane = _details;
        PanePlacement = SplitViewPanePlacement.Right;
        DisplayMode = SplitViewDisplayMode.Inline;
        OpenPaneLength = 380;
        IsPaneOpen = _preferredOpen;
        _details.CloseRequested += (_, _) => ClosePane();
        RegisterPropertyChangedCallback(IsPaneOpenProperty, (_, _) =>
        {
            _preferredOpen = IsPaneOpen;
            if (IsPaneOpen) ActivateMode?.Invoke();
            Update();
        });
        Loaded += (_, _) =>
        {
            if (IsPaneOpen) ActivateMode?.Invoke();
            Update();
        };
        SizeChanged += (_, _) => OpenPaneLength = Math.Min(380, Math.Max(0, ActualWidth));
    }

    public void OpenPane()
    {
        IsPaneOpen = true;
        // Selecting the current mode keeps it selected, just like opening it from another mode.
        if (ToggleControl is AppBarToggleButton toggle) toggle.IsChecked = true;
    }

    public void OpenText()
    {
        OpenPane();
        _details.OpenText();
    }

    private void ToggleChanged(object sender, RoutedEventArgs e)
    {
        if (ToggleControl?.IsChecked == true) OpenPane();
        else if (IsPaneOpen && ToggleControl != null) ToggleControl.IsChecked = true;
    }

    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((CaptureDetailsHost)sender).Update();
    private void Update() => _details?.Configure(SourcePath, MediaKind, IsPaneOpen, HasEdits, WorkingPath, SourceVersion,
        new(IsSourceReady, false, ImageEdited, StartSeconds, EndSeconds));
    private void ClosePane() { IsPaneOpen = false; ToggleControl?.Focus(FocusState.Programmatic); }
}
