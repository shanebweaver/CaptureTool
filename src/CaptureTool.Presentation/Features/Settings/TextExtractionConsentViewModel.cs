using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Presentation.Notifications;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Features.Settings;

public sealed class TextExtractionConsentViewModel : ViewModelBase
{
    private readonly ITextExtractionConsentService _consent;
    private readonly ITextExtractionFeatureAvailability _availability;
    private readonly ITaskEnvironment _ui;
    private readonly ILocalizationService _localization;
    private readonly IAppNotificationService _notifications;
    private bool _disposed;

    public bool ConsentGranted { get; private set => Set(ref field, value); }
    public bool IsVisible { get; private set => Set(ref field, value); }
    public IAsyncRelayCommand<bool> SetConsentCommand { get; }

    public TextExtractionConsentViewModel(ITextExtractionConsentService consent, ITextExtractionFeatureAvailability availability,
        ITaskEnvironment ui, ILocalizationService localization, IAppNotificationService notifications)
    {
        _consent = consent; _availability = availability; _ui = ui; _localization = localization; _notifications = notifications;
        SetConsentCommand = new AsyncRelayCommand<bool>(SetConsentAsync);
        _consent.StateChanged += Refresh;
        Refresh();
    }
    public void Refresh() => _ui.TryExecute(() =>
    {
        if (_disposed) return;
        ConsentGranted = _consent.State == AiFeatureConsentState.Granted;
        IsVisible = ConsentGranted || _availability.IsTextExtractionEnabled;
    });
    private async Task SetConsentAsync(bool granted)
    {
        try
        {
            if (granted) await _consent.EnsureConsentAsync();
            else if (!await _consent.RevokeConsentAsync()) ShowSaveFailure();
        }
        catch (OperationCanceledException) { }
        catch (Exception) { ShowSaveFailure(); }
        finally { Refresh(); }
    }
    private void ShowSaveFailure() => _notifications.ShowError(_localization.GetString("TextExtractionSettings_SaveFailed"));
    public override void Dispose()
    {
        _disposed = true;
        _consent.StateChanged -= Refresh;
        base.Dispose();
    }
}
