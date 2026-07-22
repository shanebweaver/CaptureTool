using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Presentation.Notifications;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Features.ImageEdit;

public sealed class TextExtractionToolViewModel : ViewModelBase
{
    private const string TextCopiedMessageResourceKey = "ImageEdit_TextCopiedNotification";
    private const string TextCopyFailedMessageResourceKey = "ImageEdit_TextCopyFailedNotification";

    private readonly IClipboardService _clipboardService;
    private readonly ILocalizationService _localizationService;
    private readonly IAppNotificationService _notificationService;

    public TextExtractionToolViewModel(
        IClipboardService clipboardService,
        ILocalizationService localizationService,
        IAppNotificationService notificationService)
    {
        _clipboardService = clipboardService;
        _localizationService = localizationService;
        _notificationService = notificationService;
        CopyAllTextCommand = new AsyncRelayCommand(
            CopyAllTextAsync,
            () => HasText,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
    }

    public IAsyncRelayCommand CopyAllTextCommand { get; }

    public string Text
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                HasText = !string.IsNullOrWhiteSpace(value);
            }
        }
    } = string.Empty;

    public bool HasText
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                CopyAllTextCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public void SetText(string? text)
    {
        Text = text ?? string.Empty;
    }

    public void Reset()
    {
        Text = string.Empty;
    }

    public async Task CopyAllTextAsync()
    {
        if (!HasText)
        {
            return;
        }

        try
        {
            await _clipboardService.CopyTextAsync(Text);
            _notificationService.ShowInfo(GetLocalizedString(TextCopiedMessageResourceKey));
        }
        catch (Exception)
        {
            _notificationService.ShowError(GetLocalizedString(TextCopyFailedMessageResourceKey));
        }
    }

    private string GetLocalizedString(string resourceKey)
    {
        string value = _localizationService.GetString(resourceKey);
        return string.IsNullOrWhiteSpace(value)
            ? resourceKey
            : value;
    }
}
