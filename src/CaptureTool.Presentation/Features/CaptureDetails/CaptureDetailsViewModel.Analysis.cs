using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Presentation.Features.CaptureDetails;

public sealed partial class CaptureDetailsViewModel
{
    private readonly ICaptureAnalysisOnboarding? _onboarding;
    private readonly IAnalysisFeatureAvailability? _availability;
    private bool _requesting;
    private bool _canceling;
    private CancellationTokenSource? _requestCancellation;
    private AnalysisCapability? _requestedAction;
    private Guid? _actionNoticeRun;
    private readonly HashSet<(string Message, bool Error)> _actionNotices = [];
    private readonly Dictionary<AnalysisCapability, string> _requestFailures = [];

    public bool IsGeneratingName
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsGeneratingSummary
    {
        get;
        private set => Set(ref field, value);
    }

    public bool ShowNameAction => NameAction.IsSupported || HasSuggestedName;

    public bool ShowSummarySection => SummaryAction.IsSupported || HasSummary;

    public bool ShowAltTextSection => IsImage && (AltTextAction.IsSupported || Content.AltText.Length > 0);

    public bool ShowSummaryTab => ShowSummarySection || ShowAltTextSection;

    public CaptureAnalysisAction AltTextAction { get; }

    public CaptureAnalysisAction SummaryAction { get; }

    public CaptureAnalysisAction NameAction { get; }

    public CaptureAnalysisAction TextAction { get; }

    public CaptureAnalysisAction QrAction { get; }

    public CaptureAnalysisAction ScanTextAction { get; }

    public CaptureAnalysisAction TranscriptAction { get; }

    private IEnumerable<CaptureAnalysisAction> Actions => [AltTextAction, SummaryAction, NameAction, TextAction, QrAction, TranscriptAction];

    public string SummaryHint => _text.GetString(_kind switch
    {
        AnalysisMediaKind.Image => "CaptureSummary_Hint/Text",
        AnalysisMediaKind.Video => "CaptureSummary_VideoHint",
        _ => "CaptureSummary_AudioHint"
    });

    private CaptureAnalysisAction CreateAnalysisAction(AnalysisCapability capability, string key)
    {
        string resourceKey = "CaptureAction_" + key;
        return new(capability, resourceKey, _text.GetString(resourceKey), () => RequestAnalysisAsync(capability));
    }

    private async Task RequestAnalysisAsync(AnalysisCapability capability, bool scanText = false)
    {
        if (_disposed || _canceling)
        {
            return;
        }

        CaptureAnalysisAction action = scanText ? ScanTextAction : Actions.Single(action => action.Capability == capability);
        if (action.IsRunning)
        {
            await CancelAnalysisAsync(capability);
            return;
        }

        bool requestsImageText = capability == AnalysisCapability.TextRecognition || capability == AnalysisCapability.QrCodeDetection;
        if (!CanRequestAnalysis(capability, scanText) || _path == null || _requesting ||
            _memory.State.IsDeleting || (_snapshot?.Run?.IsPending == true && !(UsesEditorText && requestsImageText)))
        {
            return;
        }

        using var request = CancellationTokenSource.CreateLinkedTokenSource(_cancellation);
        _requestCancellation = request;
        _requesting = true;
        _requestedAction = capability;
        _actionNotices.Clear();
        _requestFailures.Remove(capability);
        UpdateActions();
        try
        {
            if (!await CheckRequestSupportAsync(capability, scanText, request.Token))
            {
                return;
            }
            request.Token.ThrowIfCancellationRequested();

            if (UsesEditorText && requestsImageText)
            {
                await _editorText!.ExtractAsync();
            }
            else
            {
                await RequestSavedAnalysisAsync(capability, scanText, request.Token);
            }
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            if (!request.IsCancellationRequested)
            {
                _requestFailures[capability] = "Failed";
            }
        }
        finally
        {
            _requestCancellation = null;
            _requesting = false;
            if (!_disposed)
            {
                UpdateActions();
            }
            _requestedAction = null;
        }
    }

    private async Task CancelAnalysisAsync(AnalysisCapability capability)
    {
        if (_requestCancellation?.IsCancellationRequested == true)
        {
            return;
        }

        _canceling = true;
        _reviewSuggestedName = false;
        UpdateActions();
        try
        {
            _requestCancellation?.Cancel();
            if (_editorText?.IsRunning == true &&
                (capability == AnalysisCapability.TextRecognition || capability == AnalysisCapability.QrCodeDetection))
            {
                _editorText.Cancel();
            }
            else if (_path != null)
            {
                // Admission returns before inference finishes; cancel the actual worker too.
                await _memory.CancelAnalysisAsync(_path, _cancellation);
                await RefreshAsync();
            }
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            Notify(_text.GetString("CaptureAction_Failed"), error: true);
        }
        finally
        {
            _canceling = false;
            if (!_disposed)
            {
                UpdateActions();
            }
        }
    }

    private bool CanRequestAnalysis(AnalysisCapability capability, bool scanText)
    {
        if (_disposed)
        {
            return false;
        }

        return scanText
            ? ScanTextAction.IsSupported && ScanTextAction.Command.CanExecute(null)
            : Actions.Any(action => action.Capability == capability && action.IsSupported);
    }

    private async Task<bool> CheckRequestSupportAsync(AnalysisCapability capability, bool scanText, CancellationToken cancellationToken)
    {
        if (_availability == null || _kind is not { } kind)
        {
            return true;
        }

        _availability.Invalidate();
        bool supported = await _availability.IsSupportedAsync(kind, capability, cancellationToken);
        if (!supported && scanText)
        {
            supported = await _availability.IsSupportedAsync(kind, AnalysisCapability.QrCodeDetection, cancellationToken);
        }

        if (!supported)
        {
            await RefreshSupportAsync();
        }
        return supported;
    }

    private async Task RequestSavedAnalysisAsync(AnalysisCapability capability, bool scanText, CancellationToken cancellationToken)
    {
        bool consent = _onboarding != null
            ? await _onboarding.EnableAsync(cancellationToken)
            : await _memory.EnsureConsentAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!consent)
        {
            if (!_memory.State.PolicyAvailable)
            {
                Notify(Text("Unavailable"), error: true);
            }
            return;
        }

        if (scanText)
        {
            await _memory.ScanTextAsync(_path!, cancellationToken);
        }
        else
        {
            await _memory.AnalyzeAsync(_path!, capability, cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();

        if (_memory.State.FailureCode is { } failure && !_disposed)
        {
            _requestFailures[capability] = failure switch
            {
                "model-unavailable" => "Unavailable",
                "model-download-required" => "DownloadRequired",
                _ => "Failed"
            };
        }
        await RefreshAsync();
    }

    private void UpdateActions()
    {
        if (_actionNoticeRun != _snapshot?.Run?.Id)
        {
            _actionNoticeRun = _snapshot?.Run?.Id;
            _actionNotices.Clear();
        }

        bool pending = _snapshot?.Run?.IsPending == true;
        foreach (CaptureAnalysisAction action in Actions)
        {
            if (UsesEditorText && (action == TextAction || action == QrAction))
            {
                UpdateEditorTextAction(action);
            }
            else
            {
                UpdateSavedAction(action, pending);
            }
        }

        UpdateScanTextAction();
        RefreshActionBindings();
    }

    private void UpdateEditorTextAction(CaptureAnalysisAction action)
    {
        CaptureEditorTextSession editor = _editorText!;
        bool isText = action == TextAction;
        bool hasResult = isText
            ? editor.Document?.HasTextResults == true
            : editor.Document?.HasQrCodeResults == true;
        string status = isText ? editor.Status : string.Empty;
        if (!hasResult && !editor.CanExtract && status.Length == 0)
        {
            status = _text.GetString("CaptureAction_Unavailable");
        }

        bool running = (isText && editor.IsRunning) || (_requesting && _requestedAction == action.Capability);
        bool canRun = !_disposed && !_canceling && !_memory.State.IsDeleting &&
            (running ? _requestCancellation?.IsCancellationRequested != true :
                action.IsSupported && editor.CanExtract && !_requesting && !editor.IsRunning);
        action.Update(running, hasResult, canRun, status);
    }

    private void UpdateSavedAction(CaptureAnalysisAction action, bool pending)
    {
        AnalysisRun? run = _snapshot?.Run;
        AnalysisResult? result = _snapshot?.Record?.Results
            .SingleOrDefault(result => result.Payload.Capability == action.Capability);
        AnalysisStepCompletion? completion = run?.CompletedSteps
            .SingleOrDefault(step => step.Capability == action.Capability);
        bool running = (_requesting && _requestedAction == action.Capability) ||
            (result == null && pending && _memory.State.Policy.IsAllowed &&
                run!.Steps.Contains(action.Capability) && completion == null);

        string? status = GetSavedActionStatus(action, result, completion, running);
        string message = status == null ? string.Empty : _text.GetString("CaptureAction_" + status);
        if (completion?.FailureCode == "ocr-language-required")
        {
            message = _text.GetString("TextExtractionStatus_LanguageResourcesRequired");
        }
        NotifyActionResult(action, running, status, message);

        bool hasName = action == NameAction && (_acceptedName || Content.SuggestedName.Length > 0);
        bool canRun = !_disposed && !_canceling && !_memory.State.IsDeleting &&
            (running ? _requestCancellation?.IsCancellationRequested != true :
                action.IsSupported && !_requesting && !pending &&
                (action.Capability != AnalysisCapability.ImageAltText || IsImage));
        action.Update(running, result != null || hasName, canRun, message);
    }

    private string? GetSavedActionStatus(CaptureAnalysisAction action, AnalysisResult? result,
        AnalysisStepCompletion? completion, bool running)
    {
        string? status = result?.Payload switch
        {
            TranscriptMetadata { Segments.Count: 0 } => "NoSpeech",
            DescriptionMetadata { Descriptions.Count: 0 } => "NoDescription",
            ImageAltTextMetadata { Suggestion: null } => "NoSuggestion",
            CaptureSynopsisMetadata { Summary.Count: 0 } when action == SummaryAction => "NoSuggestion",
            CaptureNameMetadata { Suggestion: null } when !_acceptedName => "NoSuggestion",
            _ => null
        };
        if (result != null || running)
        {
            return status;
        }

        if (completion?.Outcome is { } outcome && outcome != AnalyzerOutcomeKind.Cancelled)
        {
            bool needsDownload = completion.FailureCode == "model-download-required" ||
                (completion.FailureCode == "metadata-input-empty" &&
                    _snapshot?.Run?.CompletedSteps.Any(step => step.FailureCode == "model-download-required") == true);
            status = needsDownload ? "DownloadRequired"
                : outcome == AnalyzerOutcomeKind.Unsupported ? "Unavailable" : "Failed";
        }
        if (_snapshot?.Run is { Status: AnalysisRunStatus.Failed } failed && failed.Steps.Contains(action.Capability))
        {
            status ??= "Failed";
        }
        if (_requestFailures.TryGetValue(action.Capability, out string? failure))
        {
            status = failure;
        }
        return status;
    }

    private void NotifyActionResult(CaptureAnalysisAction action, bool running, string? status, string message)
    {
        // Empty speech is already explained in the Speech tab.
        if (running || status == "NoSpeech" || message.Length == 0 || message == action.Status)
        {
            return;
        }
        if (!action.IsRunning && _requestedAction != action.Capability)
        {
            return;
        }

        bool error = status == "Failed";
        if (_actionNotices.Add((message, error)))
        {
            Notify(message, error);
        }
    }

    private void UpdateScanTextAction()
    {
        bool requestingText = _requesting && _requestedAction == AnalysisCapability.TextRecognition;
        bool running = TextAction.IsRunning || QrAction.IsRunning || requestingText;
        bool hasResult = TextAction.HasResult && QrAction.HasResult;
        bool canRun = HasVisualMedia &&
            (TextAction.Command.CanExecute(null) || QrAction.Command.CanExecute(null));

        ScanTextAction.IsSupported = TextAction.IsSupported || QrAction.IsSupported;
        ScanTextAction.Update(running, hasResult, canRun);
    }

    private void RefreshActionBindings()
    {
        SummaryAction.Label = _text.GetString(IsImage ? "CaptureAction_ScreenshotSummary" : "CaptureAction_Summary");
        foreach (CaptureAnalysisAction action in Actions.Append(ScanTextAction))
        {
            action.ToolTip = action.IsRunning ? _text.GetString("CaptureMemory_Cancel") : action.Label;
        }
        RaisePropertyChanged(nameof(IsImage));
        RaisePropertyChanged(nameof(IsVideo));
        RaisePropertyChanged(nameof(TextHint));
        RaisePropertyChanged(nameof(DisplayedTextContent));
        RaisePropertyChanged(nameof(ShowVideoTimestamps));
        RaisePropertyChanged(nameof(ShowTextPassages));
        IsGeneratingSummary = SummaryAction.IsRunning;
        IsGeneratingName = NameAction.IsRunning;
        RaisePropertyChanged(nameof(NameSuggestionToolTip));
        SuggestNameCommand.NotifyCanExecuteChanged();
        RaisePropertyChanged(nameof(HasVisualMedia));
        RaisePropertyChanged(nameof(HasAudio));
        RaisePropertyChanged(nameof(SummaryHint));
        RaisePropertyChanged(nameof(ShowTextResults));
        RaisePropertyChanged(nameof(ShowTextEmptyState));
        RaisePropertyChanged(nameof(TextEmptyDescription));
        RaisePropertyChanged(nameof(ShowScanTextAction));
        RaisePropertyChanged(nameof(ShowSpeechResults));
        RaisePropertyChanged(nameof(ShowSpeechEmptyState));
        RaisePropertyChanged(nameof(ShowSpeechAction));
        RaisePropertyChanged(nameof(ShowNameAction));
        RaisePropertyChanged(nameof(ShowSummarySection));
        RaisePropertyChanged(nameof(ShowAltTextSection));
        RaisePropertyChanged(nameof(ShowSummaryTab));
        RaisePropertyChanged(nameof(ShowTextTab));
        RaisePropertyChanged(nameof(ShowSpeechTab));
    }

    private async Task RefreshSupportAsync()
    {
        if (_disposed || _kind is not { } kind || _availability == null)
        {
            return;
        }

        try
        {
            var support = await Task.WhenAll(Actions.Select(async action =>
                (Action: action, Supported: await _availability.IsSupportedAsync(kind, action.Capability, _cancellation))));
            if (_disposed)
            {
                return;
            }
            foreach (var item in support)
            {
                item.Action.IsSupported = item.Supported;
            }
            UpdateActions();
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
        }
    }
}
