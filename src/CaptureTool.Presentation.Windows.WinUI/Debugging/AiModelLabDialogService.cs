#if DEBUG
using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Maintenance;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Domain.Analysis;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CaptureTool.Presentation.Windows.WinUI.Debugging;

internal sealed class AiModelLabDialogService
{
    private static readonly CapabilitySpec[] CapabilitySpecs =
    [
        new(
            AnalysisCapabilities.OcrDocumentV1,
            CaptureMediaKind.Image,
            "Image OCR",
            "Text extracted from image captures."),
        new(
            AnalysisCapabilities.ImageDescriptionV1,
            CaptureMediaKind.Image,
            "Image description",
            "Natural-language descriptions generated for image captures."),
        new(
            AnalysisCapabilities.VideoOcrTrackV1,
            CaptureMediaKind.Video,
            "Video frame OCR",
            "Timestamped text extracted from sampled video frames."),
        new(
            AnalysisCapabilities.VideoDescriptionTrackV1,
            CaptureMediaKind.Video,
            "Video frame description",
            "Timestamped descriptions generated for sampled video frames."),
        new(
            AnalysisCapabilities.SpeechTranscriptV1,
            CaptureMediaKind.Audio,
            "Audio and video speech",
            "Timestamped transcription for audio captures and video soundtracks."),
    ];

    private static readonly ModeOption[] ModeOptions =
    [
        new(CaptureAnalyzerSelectionMode.Automatic, "Auto", "Use normal quality ordering and fallback."),
        new(CaptureAnalyzerSelectionMode.Prefer, "Prefer", "Try the selected analyzer first, then allow fallback."),
        new(CaptureAnalyzerSelectionMode.Force, "Force", "Use only the selected analyzer; do not fall back."),
        new(CaptureAnalyzerSelectionMode.Off, "Off", "Do not produce this metadata capability."),
    ];

    private readonly SemaphoreSlim _dialogGate = new(1, 1);
    private readonly ICaptureAnalyzerCatalog _catalog;
    private readonly ICaptureAnalyzerSelectionService _selections;
    private readonly ICaptureAnalysisMaintenanceService _maintenance;
    private readonly CaptureAnalysisInspectorDialogService _inspector;

    public AiModelLabDialogService(
        ICaptureAnalyzerCatalog catalog,
        ICaptureAnalyzerSelectionService selections,
        ICaptureAnalysisMaintenanceService maintenance,
        CaptureAnalysisInspectorDialogService inspector)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(selections);
        ArgumentNullException.ThrowIfNull(maintenance);
        ArgumentNullException.ThrowIfNull(inspector);
        _catalog = catalog;
        _selections = selections;
        _maintenance = maintenance;
        _inspector = inspector;
    }

    public async Task ShowAsync(XamlRoot xamlRoot)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);
        if (!await _dialogGate.WaitAsync(0).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            List<CapabilityRow> rows = await CreateRowsAsync().ConfigureAwait(true);
            bool inspectRequested = false;
            ContentDialog? dialog = null;
            var inspectButton = new Button
            {
                Content = "Open Capture Analysis Inspector…",
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            inspectButton.Click += (_, _) =>
            {
                inspectRequested = true;
                dialog?.Hide();
            };
            var reanalyze = new CheckBox
            {
                Content = "Reanalyze all enrolled captures after applying",
                IsChecked = false,
            };
            var content = new StackPanel
            {
                Width = 720,
                Spacing = 12,
            };
            content.Children.Add(new TextBlock
            {
                Text = "Choose how Capture Tool resolves each metadata capability. " +
                    "These controls are stored locally and exist only in Debug builds. " +
                    "Platform and provider kill switches remain authoritative.",
                TextWrapping = TextWrapping.WrapWholeWords,
            });
            content.Children.Add(CreateBuildInfoBar());
            content.Children.Add(new TextBlock
            {
                Text = "Inspect the protected metadata generated for each capture, including " +
                    "extracted text, timestamps, outcomes, and exact model provenance.",
                TextWrapping = TextWrapping.WrapWholeWords,
            });
            content.Children.Add(inspectButton);
            foreach (CapabilityRow row in rows)
            {
                content.Children.Add(CreateCapabilityPanel(row));
            }
            content.Children.Add(reanalyze);

            dialog = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = "AI Model Lab",
                PrimaryButtonText = "Apply",
                SecondaryButtonText = "Reset to Auto",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                Style = Microsoft.UI.Xaml.Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                Content = new ScrollViewer
                {
                    MaxHeight = 640,
                    Content = content,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                },
            };

            ContentDialogResult result = await dialog.ShowAsync();
            if (inspectRequested)
            {
                await _inspector.ShowAsync(xamlRoot);
                return;
            }

            if (result == ContentDialogResult.None)
            {
                return;
            }

            CaptureAnalyzerSelection[] next = result == ContentDialogResult.Secondary
                ? [.. rows.Select(row => CaptureAnalyzerSelection.Automatic(row.Spec.Capability))]
                : [.. rows.Select(CreateSelection)];
            CaptureAnalyzerSelectionSaveResult saved = await _selections
                .SaveAsync(next, CancellationToken.None);
            if (!saved.Succeeded)
            {
                await ShowMessageAsync(
                    xamlRoot,
                    "AI Model Lab",
                    $"The model selections could not be saved ({saved.Status}).");
                return;
            }

            if (reanalyze.IsChecked == true)
            {
                CaptureAnalysisMaintenanceResult reanalysis = await _maintenance
                    .ReanalyzeCapturesAsync(new CaptureAnalysisReanalysisRequest(
                        CaptureAnalysisReanalysisScope.AllEnrolledCaptures));
                await ShowMessageAsync(
                    xamlRoot,
                    "AI Model Lab",
                    reanalysis.Status == CaptureAnalysisMaintenanceStatus.Succeeded
                        ? $"Selections applied. Scheduled {reanalysis.AffectedCaptureCount} capture(s) for reanalysis."
                        : $"Selections applied, but reanalysis finished with {reanalysis.Status}.");
            }
        }
        finally
        {
            _dialogGate.Release();
        }
    }

    private async Task<List<CapabilityRow>> CreateRowsAsync()
    {
        var rows = new List<CapabilityRow>(CapabilitySpecs.Length);
        foreach (CapabilitySpec spec in CapabilitySpecs)
        {
            ICaptureAnalyzer[] analyzers =
            [
                .. _catalog.Analyzers
                    .Where(analyzer =>
                        analyzer.Descriptor.Capability == spec.Capability &&
                        analyzer.Descriptor.SupportedMediaKinds.Contains(spec.MediaKind))
                    .OrderByDescending(analyzer => analyzer.Descriptor.QualityTier)
                    .ThenBy(analyzer => analyzer.Descriptor.Identity.AnalyzerId, StringComparer.Ordinal),
            ];
            var options = new List<AnalyzerOption>(analyzers.Length);
            foreach (ICaptureAnalyzer analyzer in analyzers)
            {
                options.Add(new(
                    analyzer.Descriptor.Identity.ProviderId,
                    analyzer.Descriptor.Identity.AnalyzerId,
                    GetAnalyzerDisplayName(analyzer.Descriptor.Identity.AnalyzerId),
                    await GetAvailabilityLabelAsync(analyzer, spec.MediaKind).ConfigureAwait(true)));
            }

            CaptureAnalyzerSelection current = _selections.GetSelection(spec.Capability);
            rows.Add(new(spec, current, options));
        }

        return rows;
    }

    private static FrameworkElement CreateBuildInfoBar()
    {
        const string message = "Debug and Release use the same stable AI provider inventory. " +
            "Only the developer selection controls are Debug-only.";
        return new InfoBar
        {
            IsOpen = true,
            IsClosable = false,
            Severity = InfoBarSeverity.Informational,
            Title = "Build inventory",
            Message = message,
        };
    }

    private static FrameworkElement CreateCapabilityPanel(CapabilityRow row)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = row.Spec.Title,
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        panel.Children.Add(new TextBlock
        {
            Text = row.Spec.Description,
            Opacity = 0.72,
            TextWrapping = TextWrapping.WrapWholeWords,
        });

        var selectionGrid = new Grid { ColumnSpacing = 12 };
        selectionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        selectionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ModeComboBox.Header = "Behavior";
        row.AnalyzerComboBox.Header = "Analyzer";
        Grid.SetColumn(row.AnalyzerComboBox, 1);
        selectionGrid.Children.Add(row.ModeComboBox);
        selectionGrid.Children.Add(row.AnalyzerComboBox);
        panel.Children.Add(selectionGrid);
        panel.Children.Add(row.ModeDescriptionText);

        foreach (AnalyzerOption option in row.Analyzers)
        {
            panel.Children.Add(new TextBlock
            {
                Text = $"{option.DisplayName} — {option.Availability}",
                FontSize = 12,
                Opacity = 0.72,
                TextWrapping = TextWrapping.WrapWholeWords,
            });
        }
        if (row.Analyzers.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "No analyzer for this capability is compiled into this build.",
                FontSize = 12,
                Opacity = 0.72,
                TextWrapping = TextWrapping.WrapWholeWords,
            });
        }

        return new Border
        {
            Padding = new Thickness(12),
            BorderThickness = new Thickness(1),
            BorderBrush = Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"] as Brush,
            Background = Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"] as Brush,
            CornerRadius = new CornerRadius(8),
            Child = panel,
        };
    }

    private async Task<string> GetAvailabilityLabelAsync(
        ICaptureAnalyzer analyzer,
        CaptureMediaKind mediaKind)
    {
        try
        {
            CaptureAnalyzerAvailability availability = await analyzer.GetAvailabilityAsync(
                new CaptureAnalyzerAvailabilityRequest(
                    analyzer.Descriptor,
                    mediaKind,
                    sourceLength: 1,
                    CaptureAnalysisPolicyDefaults.CaptureMemorySearchPurpose,
                    CaptureAnalysisPolicyDefaults.CreateLocalOnlyPolicy()));
            return availability.Status switch
            {
                CaptureAnalyzerAvailabilityStatus.Available => "Ready",
                CaptureAnalyzerAvailabilityStatus.PreparationRequired => "Model preparation required",
                CaptureAnalyzerAvailabilityStatus.Unsupported => "Unsupported on this device",
                CaptureAnalyzerAvailabilityStatus.Disabled => "Disabled by Windows or the user",
                CaptureAnalyzerAvailabilityStatus.TemporarilyUnavailable => "Temporarily unavailable",
                _ => "Availability unknown",
            };
        }
        catch
        {
            return "Availability probe failed";
        }
    }

    private static CaptureAnalyzerSelection CreateSelection(CapabilityRow row)
    {
        CaptureAnalyzerSelectionMode mode = row.SelectedMode;
        if (mode is CaptureAnalyzerSelectionMode.Automatic or CaptureAnalyzerSelectionMode.Off)
        {
            return new(row.Spec.Capability, mode);
        }

        AnalyzerOption analyzer = row.SelectedAnalyzer ?? throw new InvalidOperationException(
            "A selected analyzer is required for Prefer and Force modes.");
        return new(
            row.Spec.Capability,
            mode,
            new CaptureAnalyzerSelectionTarget(analyzer.ProviderId, analyzer.AnalyzerId));
    }

    private static string GetAnalyzerDisplayName(string analyzerId) => analyzerId switch
    {
        "windows-ai-ocr-document" => "Windows AI Text Recognition",
        "windows-ocr-document" => "Windows Media OCR (legacy)",
        "windows-image-description" => "Windows AI Image Description",
        "windows-ai-video-frame-ocr" => "Windows AI Text Recognition",
        "windows-video-frame-ocr" => "Windows Media OCR (legacy)",
        "windows-video-frame-description" => "Windows AI Image Description",
        "foundry-local-speech-transcript" => "Foundry Local Whisper Tiny",
        "foundry-local-nemotron-multilingual-speech-transcript" =>
            "Foundry Local Nemotron Multilingual ASR",
        _ => analyzerId,
    };

    private static async Task ShowMessageAsync(
        XamlRoot xamlRoot,
        string title,
        string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.WrapWholeWords,
            },
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
            Style = Microsoft.UI.Xaml.Application.Current.Resources["DefaultContentDialogStyle"] as Style,
        };
        await dialog.ShowAsync();
    }

    private sealed record CapabilitySpec(
        CapabilityDefinition Capability,
        CaptureMediaKind MediaKind,
        string Title,
        string Description);

    private sealed record ModeOption(
        CaptureAnalyzerSelectionMode Mode,
        string DisplayName,
        string Description)
    {
        public string Label => $"{DisplayName} — {Description}";
    }

    private sealed record AnalyzerOption(
        string ProviderId,
        string AnalyzerId,
        string DisplayName,
        string Availability)
    {
        public string Label => $"{DisplayName} ({ProviderId})";
    }

    private sealed class CapabilityRow
    {
        private readonly IReadOnlyList<ModeOption> _availableModes;

        public CapabilityRow(
            CapabilitySpec spec,
            CaptureAnalyzerSelection selection,
            IReadOnlyList<AnalyzerOption> analyzers)
        {
            Spec = spec;
            Analyzers = analyzers;
            _availableModes = analyzers.Count == 0
                ? [.. ModeOptions.Where(option => option.Mode is
                    CaptureAnalyzerSelectionMode.Automatic or CaptureAnalyzerSelectionMode.Off)]
                : ModeOptions;
            ModeComboBox = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            foreach (ModeOption option in _availableModes)
            {
                // WinUI cannot reliably project private nested CLR records through ItemsSource.
                // Keep the UI boundary to strings and map selections by index instead.
                ModeComboBox.Items.Add(option.DisplayName);
            }

            int selectedModeIndex = FindModeIndex(selection.Mode);
            ModeComboBox.SelectedIndex = selectedModeIndex >= 0
                ? selectedModeIndex
                : FindModeIndex(CaptureAnalyzerSelectionMode.Automatic);

            AnalyzerComboBox = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            foreach (AnalyzerOption option in analyzers)
            {
                AnalyzerComboBox.Items.Add(option.Label);
            }

            int selectedAnalyzerIndex = FindAnalyzerIndex(option =>
                    string.Equals(option.ProviderId, selection.Target?.ProviderId, StringComparison.Ordinal) &&
                    string.Equals(option.AnalyzerId, selection.Target?.AnalyzerId, StringComparison.Ordinal));
            AnalyzerComboBox.SelectedIndex = selectedAnalyzerIndex >= 0
                ? selectedAnalyzerIndex
                : analyzers.Count > 0 ? 0 : -1;

            ModeDescriptionText = new TextBlock
            {
                FontSize = 12,
                Opacity = 0.72,
                TextWrapping = TextWrapping.WrapWholeWords,
            };
            ModeComboBox.SelectionChanged += (_, _) => UpdateAnalyzerEnablement();
            UpdateAnalyzerEnablement();
        }

        public CapabilitySpec Spec { get; }

        public IReadOnlyList<AnalyzerOption> Analyzers { get; }

        public ComboBox ModeComboBox { get; }

        public ComboBox AnalyzerComboBox { get; }

        public TextBlock ModeDescriptionText { get; }

        public CaptureAnalyzerSelectionMode SelectedMode => ModeComboBox.SelectedIndex is var index &&
            index >= 0 && index < _availableModes.Count
                ? _availableModes[index].Mode
                : CaptureAnalyzerSelectionMode.Automatic;

        public AnalyzerOption? SelectedAnalyzer => AnalyzerComboBox.SelectedIndex is var index &&
            index >= 0 && index < Analyzers.Count
                ? Analyzers[index]
                : null;

        private int FindModeIndex(CaptureAnalyzerSelectionMode mode)
        {
            for (int index = 0; index < _availableModes.Count; index++)
            {
                if (_availableModes[index].Mode == mode)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindAnalyzerIndex(Func<AnalyzerOption, bool> predicate)
        {
            for (int index = 0; index < Analyzers.Count; index++)
            {
                if (predicate(Analyzers[index]))
                {
                    return index;
                }
            }

            return -1;
        }

        private void UpdateAnalyzerEnablement()
        {
            AnalyzerComboBox.IsEnabled = SelectedMode is CaptureAnalyzerSelectionMode.Prefer or
                CaptureAnalyzerSelectionMode.Force;
            int index = ModeComboBox.SelectedIndex;
            ModeDescriptionText.Text = index >= 0 && index < _availableModes.Count
                ? _availableModes[index].Description
                : string.Empty;
        }
    }
}
#endif
