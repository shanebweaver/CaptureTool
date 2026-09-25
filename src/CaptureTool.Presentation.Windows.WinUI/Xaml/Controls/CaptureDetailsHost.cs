using CaptureTool.Domain.Analysis;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

/// <summary>Shared adaptive editor layout. The pane preference lasts for the app session.</summary>
public sealed partial class CaptureDetailsHost : SplitView
{
    private static bool _preferredOpen;
    private readonly CaptureDetailsPane _details;
    public static readonly DependencyProperty SourcePathProperty = DependencyProperty.Register(nameof(SourcePath), typeof(string), typeof(CaptureDetailsHost), new(null, Changed));
    public static readonly DependencyProperty MediaKindProperty = DependencyProperty.Register(nameof(MediaKind), typeof(AnalysisMediaKind), typeof(CaptureDetailsHost), new(AnalysisMediaKind.Image, Changed));
    public static readonly DependencyProperty HasEditsProperty = DependencyProperty.Register(nameof(HasEdits), typeof(bool), typeof(CaptureDetailsHost), new(false, Changed));
    public string? SourcePath { get => (string?)GetValue(SourcePathProperty); set => SetValue(SourcePathProperty, value); }
    public AnalysisMediaKind MediaKind { get => (AnalysisMediaKind)GetValue(MediaKindProperty); set => SetValue(MediaKindProperty, value); }
    public bool HasEdits { get => (bool)GetValue(HasEditsProperty); set => SetValue(HasEditsProperty, value); }
    public Control? ToggleControl { get; set; }

    public CaptureDetailsHost()
    {
        _details = new CaptureDetailsPane();
        Pane = _details;
        PanePlacement = SplitViewPanePlacement.Right;
        DisplayMode = SplitViewDisplayMode.Overlay;
        OpenPaneLength = 380;
        IsPaneOpen = _preferredOpen;
        _details.CloseRequested += (_, _) => ClosePane();
        PaneOpening += (_, _) => { _preferredOpen = true; Update(); };
        PaneClosed += (_, _) => { _preferredOpen = false; Update(); };
        Loaded += (_, _) => Update();
        SizeChanged += (_, _) =>
        {
            OpenPaneLength = Math.Min(380, Math.Max(0, ActualWidth));
            DisplayMode = ActualWidth >= 1000 ? SplitViewDisplayMode.Inline : SplitViewDisplayMode.Overlay;
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == VirtualKey.Escape && IsPaneOpen) { ClosePane(); e.Handled = true; }
        };
    }

    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((CaptureDetailsHost)sender).Update();
    private void Update() => _details?.Configure(SourcePath, MediaKind, IsPaneOpen, HasEdits);
    private void ClosePane() { IsPaneOpen = false; ToggleControl?.Focus(FocusState.Programmatic); }
}
