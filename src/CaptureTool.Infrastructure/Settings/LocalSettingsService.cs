using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.Definitions;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Infrastructure.Settings.Serialization;
using CaptureTool.Infrastructure.Storage;

namespace CaptureTool.Infrastructure.Settings;

public partial class LocalSettingsService : ISettingsService, IDisposable
{
    private sealed class SettingsFile(string filePath) : FileReference(filePath);

    private static readonly IReadOnlySet<string> TelemetrySettingKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        CaptureToolSettings.Settings_AiConsent_ImageDescription.Key,
        CaptureToolSettings.Settings_AiConsent_ImageForegroundExtraction.Key,
        CaptureToolSettings.Settings_AiConsent_ImageObjectErase.Key,
        CaptureToolSettings.Settings_AiConsent_ImageObjectExtraction.Key,
        CaptureToolSettings.Settings_AiConsent_ImageSuperResolution.Key,
        CaptureToolSettings.Settings_AiConsent_TextExtraction.Key,
        CaptureToolSettings.Settings_AiConsent_VideoSuperResolution.Key,
        CaptureToolSettings.Settings_AudioCapture_AutoCopy.Key,
        CaptureToolSettings.Settings_AudioCapture_AutoSave.Key,
        CaptureToolSettings.Settings_AudioCapture_DefaultLocalAudioEnabled.Key,
        CaptureToolSettings.Settings_Capture_WarnBeforeDiscard.Key,
        CaptureToolSettings.Settings_Edit_WarnBeforeDiscard.Key,
        CaptureToolSettings.Settings_ImageCapture_AutoCopy.Key,
        CaptureToolSettings.Settings_ImageCapture_AutoSave.Key,
        CaptureToolSettings.Settings_LanguageOverride.Key,
        CaptureToolSettings.Settings_VideoCapture_AutoCopy.Key,
        CaptureToolSettings.Settings_VideoCapture_AutoSave.Key,
        CaptureToolSettings.Settings_VideoCapture_DefaultLocalAudioEnabled.Key,
        CaptureToolSettings.VerboseLogging.Key
    };

    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly Lock _accessLock = new();
    private readonly ILogService _logService;
    private readonly IJsonStorageService _jsonStorageService;
    private readonly ITelemetryService? _telemetryService;

    private Dictionary<string, SettingDefinition> _settings;
    private SettingsFile? _settingsFile;
    private bool _isInitialized;
    private bool _disposed;

    public event Action<ISettingDefinition[]>? SettingsChanged;

    public LocalSettingsService(
        ILogService logService,
        IJsonStorageService jsonStorageService,
        ITelemetryService? telemetryService = null)
    {
        _logService = logService;
        _jsonStorageService = jsonStorageService;
        _telemetryService = telemetryService;
        _settings = [];
    }

    public T Get<T>(ISettingDefinitionWithValue<T> settingDefinition)
    {
        lock (_accessLock)
        {
            ThrowIfNotInitialized();

            return
                settingDefinition.Key != null &&
                _settings.TryGetValue(settingDefinition.Key, out SettingDefinition? storedSetting) &&
                storedSetting is ISettingDefinitionWithValue<T> tSetting
                    ? tSetting.Value
                    : settingDefinition.Value;
        }
    }

    public bool IsSet(ISettingDefinition settingDefinition)
    {
        lock (_accessLock)
        {
            ThrowIfNotInitialized();

            return settingDefinition.Key != null && _settings.ContainsKey(settingDefinition.Key);
        }
    }

    public void Set(IBoolSettingDefinition settingDefinition, bool value) =>
        LockAndSet(new BoolSettingDefinition(settingDefinition.Key, value));

    public void Set(IDoubleSettingDefinition settingDefinition, double value)
        => LockAndSet(new DoubleSettingDefinition(settingDefinition.Key, value));

    public void Set(IIntSettingDefinition settingDefinition, int value)
        => LockAndSet(new IntSettingDefinition(settingDefinition.Key, value));

    public void Set(IStringSettingDefinition settingDefinition, string value)
        => LockAndSet(new StringSettingDefinition(settingDefinition.Key, value));

    public Task<SettingsMutationResult> TrySetAndSaveAsync(
        IBoolSettingDefinition settingDefinition,
        bool value,
        CancellationToken cancellationToken) =>
        TryMutateAndSaveAsync(
            settings => SetCandidate(settings, new BoolSettingDefinition(settingDefinition.Key, value)),
            cancellationToken);

    public Task<SettingsMutationResult> TrySetAndSaveAsync(
        IDoubleSettingDefinition settingDefinition,
        double value,
        CancellationToken cancellationToken) =>
        TryMutateAndSaveAsync(
            settings => SetCandidate(settings, new DoubleSettingDefinition(settingDefinition.Key, value)),
            cancellationToken);

    public Task<SettingsMutationResult> TrySetAndSaveAsync(
        IIntSettingDefinition settingDefinition,
        int value,
        CancellationToken cancellationToken) =>
        TryMutateAndSaveAsync(
            settings => SetCandidate(settings, new IntSettingDefinition(settingDefinition.Key, value)),
            cancellationToken);

    public Task<SettingsMutationResult> TrySetAndSaveAsync(
        IStringSettingDefinition settingDefinition,
        string value,
        CancellationToken cancellationToken) =>
        TryMutateAndSaveAsync(
            settings => SetCandidate(settings, new StringSettingDefinition(settingDefinition.Key, value)),
            cancellationToken);

    public Task<SettingsMutationResult> TryUnsetAndSaveAsync(
        ISettingDefinition settingDefinition,
        CancellationToken cancellationToken) =>
        TryMutateAndSaveAsync(
            settings => UnsetCandidate(settings, settingDefinition),
            cancellationToken);

    public Task<SettingsMutationResult> TryClearAllAndSaveAsync(CancellationToken cancellationToken) =>
        TryMutateAndSaveAsync(ClearCandidate, cancellationToken);

    public async Task InitializeAsync(string filePath, CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return;
        }

        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            if (_isInitialized)
            {
                return;
            }

            SettingsFile? settingsFile = null;
            List<SettingDefinition>? settingsList = null;
            try
            {
                settingsFile = new(filePath);
                settingsList = await _jsonStorageService.ReadAsync(settingsFile, SettingDefinitionContext.Default.ListSettingDefinition);
            }
            catch (FileNotFoundException)
            {
                // The Settings.json file doesn't exist yet. That's fine, no-op.
            }
            catch (Exception e)
            {
                LogException(e, "Failed to load Settings file.");
            }

            Dictionary<string, SettingDefinition> settings = [];
            if (settingsList != null)
            {
                foreach (SettingDefinition setting in settingsList)
                {
                    if (!string.IsNullOrEmpty(setting.Key))
                    {
                        settings[setting.Key] = setting;
                    }
                }
            }

            Interlocked.Exchange(ref _settings, settings);
            Interlocked.Exchange(ref _settingsFile, settingsFile);

            FireSettingsChangedEvent([.. _settings.Values]);

            _isInitialized = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Unset(ISettingDefinition settingDefinition)
    {
        lock (_accessLock)
        {
            ThrowIfNotInitialized();

            if (settingDefinition.Key != null)
            {
                bool removed = _settings.Remove(settingDefinition.Key);
                FireSettingsChangedEvent(settingDefinition);
                if (removed)
                {
                    TrackSettingChanged(settingDefinition.Key, "default");
                }
            }
        }
    }

    public void Unset(ISettingDefinition[] settingDefinitions)
    {
        lock (_accessLock)
        {
            ThrowIfNotInitialized();

            List<ISettingDefinition> removedSettings = [];
            foreach (ISettingDefinition settingDefinition in settingDefinitions)
            {
                if (settingDefinition.Key != null &&
                    _settings.Remove(settingDefinition.Key))
                {
                    removedSettings.Add(settingDefinition);
                }
            }

            if (removedSettings.Count > 0)
            {
                FireSettingsChangedEvent([.. settingDefinitions]);
                foreach (ISettingDefinition settingDefinition in removedSettings)
                {
                    TrackSettingChanged(settingDefinition.Key, "default");
                }
            }
        }
    }

    public async Task<bool> TrySaveAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return false;
        }

        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            ThrowIfNotInitialized();

            try
            {
                List<SettingDefinition> settingsList;
                lock (_accessLock)
                {
                    settingsList = [.. _settings.Values];
                }

                await _jsonStorageService.WriteAsync(GetSettingsFile(), settingsList, SettingDefinitionContext.Default.ListSettingDefinition);
                return true;
            }
            catch (Exception e)
            {
                LogException(e, "Unable to perform save operation.");
            }

            return false;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void LockAndSet<T>(SettingDefinition<T> settingDefinition)
    {
        lock (_accessLock)
        {
            ThrowIfNotInitialized();

            if (settingDefinition.Key != null)
            {
                if (_settings.TryGetValue(settingDefinition.Key, out SettingDefinition? existingSetting) &&
                    existingSetting is ISettingDefinitionWithValue<T> existingSettingT &&
                    EqualityComparer<T>.Default.Equals(existingSettingT.Value, settingDefinition.Value))
                {
                    // Values are the same.
                    return;
                }

                _settings[settingDefinition.Key] = settingDefinition;
                FireSettingsChangedEvent(settingDefinition);
                TrackSettingChanged(settingDefinition.Key, GetSafeTelemetryValue(settingDefinition));
            }
        }
    }

    private async Task<SettingsMutationResult> TryMutateAndSaveAsync(
        Func<Dictionary<string, SettingDefinition>, ISettingDefinition[]> mutate,
        CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return SettingsMutationResult.ServiceUnavailable;
        }

        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            Dictionary<string, SettingDefinition> originalSettings;
            Dictionary<string, SettingDefinition> candidateSettings;
            ISettingDefinition[] changedSettings;
            lock (_accessLock)
            {
                ThrowIfNotInitialized();
                originalSettings = new Dictionary<string, SettingDefinition>(_settings, StringComparer.Ordinal);
                candidateSettings = new Dictionary<string, SettingDefinition>(_settings, StringComparer.Ordinal);
                changedSettings = mutate(candidateSettings);
            }

            try
            {
                await _jsonStorageService.WriteAsync(
                    GetSettingsFile(),
                    candidateSettings.Values.ToList(),
                    SettingDefinitionContext.Default.ListSettingDefinition);
            }
            catch (Exception e)
            {
                LogException(e, "Unable to perform settings mutation save operation.");
                return SettingsMutationResult.PersistenceFailed;
            }

            ISettingDefinition[] committedSettings;
            lock (_accessLock)
            {
                committedSettings = CommitCandidateChanges(
                    originalSettings,
                    candidateSettings,
                    changedSettings);
                if (committedSettings.Length > 0)
                {
                    FireSettingsChangedEvent(committedSettings);
                    TrackCommittedSettings(candidateSettings, committedSettings);
                }
            }

            return SettingsMutationResult.Saved;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static ISettingDefinition[] SetCandidate<T>(
        Dictionary<string, SettingDefinition> settings,
        SettingDefinition<T> settingDefinition)
    {
        if (settings.TryGetValue(settingDefinition.Key, out SettingDefinition? existingSetting) &&
            existingSetting is ISettingDefinitionWithValue<T> existingSettingT &&
            EqualityComparer<T>.Default.Equals(existingSettingT.Value, settingDefinition.Value))
        {
            return [];
        }

        settings[settingDefinition.Key] = settingDefinition;
        return [settingDefinition];
    }

    private static ISettingDefinition[] UnsetCandidate(
        Dictionary<string, SettingDefinition> settings,
        ISettingDefinition settingDefinition)
    {
        return settingDefinition.Key != null && settings.Remove(settingDefinition.Key)
            ? [settingDefinition]
            : [];
    }

    private static ISettingDefinition[] ClearCandidate(Dictionary<string, SettingDefinition> settings)
    {
        ISettingDefinition[] removedSettings = [.. settings.Values];
        settings.Clear();
        return removedSettings;
    }

    private ISettingDefinition[] CommitCandidateChanges(
        Dictionary<string, SettingDefinition> originalSettings,
        Dictionary<string, SettingDefinition> candidateSettings,
        ISettingDefinition[] changedSettings)
    {
        List<ISettingDefinition> committedSettings = [];
        foreach (ISettingDefinition changedSetting in changedSettings)
        {
            if (changedSetting.Key == null)
            {
                continue;
            }

            bool originallySet = originalSettings.TryGetValue(
                changedSetting.Key,
                out SettingDefinition? originalSetting);
            bool currentlySet = _settings.TryGetValue(
                changedSetting.Key,
                out SettingDefinition? currentSetting);
            bool currentMatchesOriginal = originallySet
                ? currentlySet && ReferenceEquals(currentSetting, originalSetting)
                : !currentlySet;
            if (!currentMatchesOriginal)
            {
                continue;
            }

            if (candidateSettings.TryGetValue(changedSetting.Key, out SettingDefinition? candidateSetting))
            {
                _settings[changedSetting.Key] = candidateSetting;
            }
            else
            {
                _settings.Remove(changedSetting.Key);
            }

            committedSettings.Add(changedSetting);
        }

        return [.. committedSettings];
    }

    private void TrackCommittedSettings(
        Dictionary<string, SettingDefinition> candidateSettings,
        ISettingDefinition[] changedSettings)
    {
        foreach (ISettingDefinition changedSetting in changedSettings)
        {
            object? value = candidateSettings.TryGetValue(
                changedSetting.Key,
                out SettingDefinition? candidateSetting)
                ? GetSafeTelemetryValue(candidateSetting)
                : "default";
            TrackSettingChanged(changedSetting.Key, value);
        }
    }

    private SettingsFile GetSettingsFile()
    {
        ThrowIfNotInitialized();

        if (_settingsFile == null)
        {
            throw new InvalidOperationException("SettingsService has not been initialized with a file path.");
        }

        return _settingsFile;
    }

    private void ThrowIfNotInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("SettingsService must be initialized before it can be queried.");
        }
    }

    private void LogException(Exception e, string? message = null) => _logService.LogException(e, message);

    private void FireSettingsChangedEvent(ISettingDefinition settingDefinition) => SettingsChanged?.Invoke([settingDefinition]);
    private void FireSettingsChangedEvent(ISettingDefinition[] settingDefinitions) => SettingsChanged?.Invoke(settingDefinitions);

    private void TrackSettingChanged(string settingKey, object? value)
    {
        if (!TelemetrySettingKeys.Contains(settingKey))
        {
            return;
        }

        _telemetryService?.TrackEvent(
            TelemetryEvents.SettingsChanged,
            new Dictionary<string, object?>
            {
                [TelemetryProperties.Setting] = settingKey,
                [TelemetryProperties.Value] = value
            });
    }

    private static object? GetSafeTelemetryValue(ISettingDefinition settingDefinition)
    {
        if (settingDefinition is ISettingDefinitionWithValue<bool> boolSetting)
        {
            return boolSetting.Value;
        }

        if (settingDefinition.Key == CaptureToolSettings.Settings_LanguageOverride.Key)
        {
            return settingDefinition is not ISettingDefinitionWithValue<string> stringSetting ||
                stringSetting.Value is not string languageOverride ||
                string.IsNullOrWhiteSpace(languageOverride)
                ? "system_default"
                : "override";
        }

        return "changed";
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _semaphore.Dispose();
            }

            _disposed = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public void ClearAllSettings()
    {
        lock (_accessLock)
        {
            ThrowIfNotInitialized();
            ISettingDefinition[] removedSettings = [.. _settings.Values];
            _settings.Clear();
            if (removedSettings.Length > 0)
            {
                FireSettingsChangedEvent(removedSettings);
                foreach (ISettingDefinition removedSetting in removedSettings)
                {
                    TrackSettingChanged(removedSetting.Key, "default");
                }
            }
        }
    }
}
