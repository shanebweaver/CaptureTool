namespace CaptureTool.Services.Settings;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Services.Logging;
using CaptureTool.Services.Settings.Definitions;
using CaptureTool.Services.Storage;

public partial class SettingsService : ISettingsService, IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly object _accessLock = new();
    private readonly ILogService _logService;
    private readonly IJsonStorageService _jsonStorageService;

    private volatile bool _isInitialized;
    private volatile string? _filePath;
    private volatile Dictionary<string, SettingDefinition> _settings;
    private bool _disposed;

    public event Action<ICollection<SettingDefinition>>? SettingsChanged;

    public SettingsService(
        ILogService logService, 
        IJsonStorageService jsonStorageService)
    {
        _logService = logService;
        _jsonStorageService = jsonStorageService;
        _settings = [];
    }

    public T Get<T>(SettingDefinition<T> settingDefinition)
    {
        lock (_accessLock)
        {
            ThrowIfNotInitialized();

            return
                settingDefinition.Key != null &&
                _settings.TryGetValue(settingDefinition.Key, out SettingDefinition? storedSetting) &&
                storedSetting is SettingDefinition<T> tSetting
                    ? tSetting.Value
                    : settingDefinition.Value;
        }
    }

    public bool IsSet(SettingDefinition settingDefinition)
    {
        lock (_accessLock)
        {
            ThrowIfNotInitialized();

            return settingDefinition.Key != null && _settings.ContainsKey(settingDefinition.Key);
        }
    }

    public void Set(BoolSettingDefinition settingDefinition, bool newValue) => LockAndSet(new BoolSettingDefinition(settingDefinition.Key, newValue));
    public void Set(DoubleSettingDefinition settingDefinition, double newValue) => LockAndSet(new DoubleSettingDefinition(settingDefinition.Key, newValue));
    public void Set(IntSettingDefinition settingDefinition, int newValue) => LockAndSet(new IntSettingDefinition(settingDefinition.Key, newValue));
    public void Set(StringSettingDefinition settingDefinition, string newValue) => LockAndSet(new StringSettingDefinition(settingDefinition.Key, newValue));
    public void Set(PointSettingDefinition settingDefinition, Point newValue) => LockAndSet(new PointSettingDefinition(settingDefinition.Key, newValue));
    public void Set(SizeSettingDefinition settingDefinition, Size newValue) => LockAndSet(new SizeSettingDefinition(settingDefinition.Key, newValue));
    public void Set(StringArraySettingDefinition settingDefinition, string[] newValue) => LockAndSet(new StringArraySettingDefinition(settingDefinition.Key, newValue));

    public async Task InitializeAsync(string filePath)
    {
        await _semaphore.WaitAsync();

        try
        {
            if (_isInitialized)
            {
                return;
            }

            List<SettingDefinition>? settingsList = null;
            try
            {
                settingsList = await _jsonStorageService.ReadAsync(filePath, SettingDefinitionContext.Default.ListSettingDefinition);
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
            Interlocked.Exchange(ref _filePath, filePath);

            FireSettingsChangedEvent(_settings.Values);

            _isInitialized = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Unset(SettingDefinition settingDefinition)
    {
        lock (_accessLock)
        {
            ThrowIfNotInitialized();

            if (settingDefinition.Key != null)
            {
                _settings.Remove(settingDefinition.Key);
                FireSettingsChangedEvent(settingDefinition);
            }
        }
    }

    public void Unset(ICollection<SettingDefinition> settingDefinitions)
    {
        lock (_accessLock)
        {
            ThrowIfNotInitialized();

            int preCount = _settings.Count;
            foreach (SettingDefinition settingDefinition in settingDefinitions)
            {
                if (settingDefinition.Key != null)
                {
                    _settings.Remove(settingDefinition.Key);
                }
            }

            if (preCount > _settings.Count)
            {
                FireSettingsChangedEvent(settingDefinitions);
            }
        }
    }

    public async Task<bool> TrySaveAsync()
    {
        await _semaphore.WaitAsync();

        try
        {
            ThrowIfNotInitialized();

            try
            {
                List<SettingDefinition> settingsList = [.. _settings.Values];
                await _jsonStorageService.WriteAsync(GetFilePath(), settingsList, SettingDefinitionContext.Default.ListSettingDefinition);
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
                    existingSetting is SettingDefinition<T> existingSettingT &&
                    EqualityComparer<T>.Default.Equals(existingSettingT.Value, settingDefinition.Value))
                {
                    // Values are the same.
                    return;
                }

                _settings[settingDefinition.Key] = settingDefinition;
                FireSettingsChangedEvent(settingDefinition);
            }
        }
    }

    private string GetFilePath()
    {
        ThrowIfNotInitialized();

        if (_filePath == null)
        {
            throw new InvalidOperationException("SettingsService has not been initialized with a file path.");
        }

        return _filePath;
    }

    private void ThrowIfNotInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("SettingsService must be initialized before it can be queried.");
        }
    }

    private void LogException(Exception e, string? message = null) => _logService.LogException(e, message);

    private void FireSettingsChangedEvent(SettingDefinition settingDefinition) => SettingsChanged?.Invoke(new List<SettingDefinition>() { settingDefinition });
    private void FireSettingsChangedEvent(ICollection<SettingDefinition> settingDefinitions) => SettingsChanged?.Invoke(settingDefinitions);

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
}