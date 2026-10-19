using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Application.Abstractions.Library.CaptureDetails;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Features.CaptureDetails;

public sealed partial class CaptureDetailsViewModel
{
    private CaptureTextNavigationContext _navigation = new(false, false);
    private CaptureEditorTextSession? _editorText;
    private RecognizedTextDocument? _editorDocument;
    private IReadOnlyList<CaptureTextPassage> _editorPassages = [];

    public bool ShowTextTab => HasVisualMedia && (TextAction.IsSupported || QrAction.IsSupported || TextPassages.Count > 0);

    public bool ShowSpeechTab => HasAudio && (TranscriptAction.IsSupported || SpeechPassages.Count > 0);

    public string TextHint => _text.GetString(IsVideo ? "CaptureVideoText_Hint" : "CaptureText_Hint/Text");

    private bool UsesEditorText
    {
        get
        {
            if (!IsImage || _editorText == null)
            {
                return false;
            }

            bool savedSourceUnavailable = _snapshot?.Status is
                CaptureDetailsStatus.SourceChanged or CaptureDetailsStatus.SourceUnavailable;
            bool savedSourceChanged = _snapshot?.Record != null && !_sourceMatches;
            return _editorText.HasChanges || _editorText.Document != null ||
                savedSourceUnavailable || savedSourceChanged;
        }
    }

    public IReadOnlyList<CaptureTextPassage> TextPassages => TextContent.All;

    public IReadOnlyList<CaptureTextPassage> SpeechPassages => SpeechContent.All;

    public bool ShowTextResults => !ScanTextAction.IsRunning && TextPassages.Count > 0;

    public bool ShowVideoTimestamps => IsVideo && !VideoText.HasSelectedFrame;

    public bool ShowTextPassages => !ShowVideoTimestamps;

    public bool ShowTextEmptyState => HasVisualMedia && !ScanTextAction.IsRunning && TextPassages.Count == 0 && ScanTextAction.HasResult;

    public string TextEmptyDescription => _text.GetString("CapturePane_EmptyTextDescription");

    public bool ShowScanTextAction => HasVisualMedia && ScanTextAction.IsSupported && !ScanTextAction.HasResult && !ShowTextResults;

    public bool ShowSpeechResults => !TranscriptAction.IsRunning && SpeechPassages.Count > 0;

    public bool ShowSpeechEmptyState => HasAudio && !TranscriptAction.IsRunning && SpeechPassages.Count == 0 && TranscriptAction.HasResult;

    public bool ShowSpeechAction => HasAudio && TranscriptAction.IsSupported && !TranscriptAction.HasResult && !ShowSpeechResults;

    public bool CanShowImageTextOverlay
    {
        get
        {
            if (!IsImage || !_navigation.IsReady || _memory.State.IsDeleting)
            {
                return false;
            }

            return UsesEditorText
                ? _editorText!.Document != null
                : _sourceMatches && !_navigation.ImageEdited;
        }
    }

    public CaptureTextViewModel TextContent { get; }

    public CaptureTextViewModel SpeechContent { get; }

    public CaptureVideoTextViewModel VideoText { get; }

    public CaptureTextViewModel DisplayedTextContent => IsVideo ? VideoText.Content : TextContent;

    public IAsyncRelayCommand CopyResultsCommand { get; }

    public IAsyncRelayCommand CopySpeechResultsCommand { get; }

    public void SetNavigationContext(CaptureTextNavigationContext context)
    {
        _navigation = context;
        RefreshText();
    }

    public void SetEditorText(CaptureEditorTextSession? session)
    {
        if (ReferenceEquals(_editorText, session))
        {
            return;
        }

        if (_editorText != null)
        {
            _editorText.Changed -= EditorTextChanged;
        }

        _editorText = session;
        if (_editorText != null)
        {
            _editorText.Changed += EditorTextChanged;
        }

        EditorTextChanged();
    }

    public async Task EnsureTextAsync()
    {
        if (_disposed || TextAction.HasResult || TextAction.IsRunning)
        {
            return;
        }

        if (ScanTextAction.Command.CanExecute(null))
        {
            await ScanTextAction.Command.ExecuteAsync(null);
        }
    }

    private void EditorTextChanged()
    {
        if (_disposed)
        {
            return;
        }

        if (!ReferenceEquals(_editorDocument, _editorText?.Document))
        {
            _editorDocument = _editorText?.Document;
            _editorPassages = _editorDocument == null ? [] : CaptureTextPassage.From(_editorDocument, _text);
        }

        RefreshText();
        UpdateActions();
    }

    private void RefreshText()
    {
        var passages = UsesEditorText ? _editorPassages : Content.Passages;
        TextContent.Replace(passages.Where(passage => passage.Source != CaptureTextSource.Speech).ToArray());
        SpeechContent.Replace(passages.Where(passage => passage.Source == CaptureTextSource.Speech).ToArray());
        var savedNavigation = _navigation with { SourceMatches = _sourceMatches };
        var textNavigation = UsesEditorText
            ? new CaptureTextNavigationContext(_navigation.IsReady, _editorText!.Document != null)
            : savedNavigation;
        TextContent.SetNavigationContext(textNavigation);
        SpeechContent.SetNavigationContext(savedNavigation);
        VideoText.Replace(IsVideo ? TextPassages : []);
        VideoText.SetNavigationContext(savedNavigation);
        RaisePropertyChanged(nameof(TextPassages));
        RaisePropertyChanged(nameof(SpeechPassages));
        RaisePropertyChanged(nameof(CanShowImageTextOverlay));
    }

    private void VideoTextChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CaptureVideoTextViewModel.HasSelectedFrame))
        {
            return;
        }

        RaisePropertyChanged(nameof(ShowVideoTimestamps));
        RaisePropertyChanged(nameof(ShowTextPassages));
    }
}
