using CaptureTool.Infrastructure.Abstractions.Audio;
using Windows.Devices.Enumeration;
using Windows.Media.Devices;

namespace CaptureTool.Infrastructure.Windows.Audio;

public sealed class WindowsAudioInputDetectionService : IAudioInputDetectionService, IDisposable
{
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly string _audioCaptureSelector = MediaDevice.GetAudioCaptureSelector();
    private DeviceWatcher? _deviceWatcher;
    private bool _disposed;

    public event EventHandler<AudioInputSourcesChangedEventArgs>? AudioInputSourcesChanged;

    public async Task<IReadOnlyList<AudioInputSource>> GetAudioInputSourcesAsync(CancellationToken cancellationToken = default)
    {
        string defaultAudioInputId = MediaDevice.GetDefaultAudioCaptureId(AudioDeviceRole.Default);
        DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(_audioCaptureSelector).AsTask(cancellationToken);

        return devices
            .Where(device => device.IsEnabled)
            .Select(device => new AudioInputSource(
                device.Id,
                string.IsNullOrWhiteSpace(device.Name) ? "Unknown audio input" : device.Name,
                string.Equals(device.Id, defaultAudioInputId, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(source => source.IsDefault)
            .ThenBy(source => source.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public void StartWatching()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_deviceWatcher != null)
        {
            return;
        }

        _deviceWatcher = DeviceInformation.CreateWatcher(_audioCaptureSelector);
        _deviceWatcher.Added += DeviceWatcher_Added;
        _deviceWatcher.Removed += DeviceWatcher_Removed;
        _deviceWatcher.Updated += DeviceWatcher_Updated;
        _deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
        _deviceWatcher.Stopped += DeviceWatcher_Stopped;
        _deviceWatcher.Start();
    }

    public void StopWatching()
    {
        if (_deviceWatcher == null)
        {
            return;
        }

        if (_deviceWatcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
        {
            _deviceWatcher.Stop();
        }

        _deviceWatcher.Added -= DeviceWatcher_Added;
        _deviceWatcher.Removed -= DeviceWatcher_Removed;
        _deviceWatcher.Updated -= DeviceWatcher_Updated;
        _deviceWatcher.EnumerationCompleted -= DeviceWatcher_EnumerationCompleted;
        _deviceWatcher.Stopped -= DeviceWatcher_Stopped;
        _deviceWatcher = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopWatching();
        _refreshLock.Dispose();
        _disposed = true;
    }

    private void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
    {
        _ = RaiseAudioInputSourcesChangedAsync(AudioInputSourcesChangeReason.Added, args.Id);
    }

    private void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        _ = RaiseAudioInputSourcesChangedAsync(AudioInputSourcesChangeReason.Removed, args.Id);
    }

    private void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        _ = RaiseAudioInputSourcesChangedAsync(AudioInputSourcesChangeReason.Updated, args.Id);
    }

    private void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object args)
    {
        _ = RaiseAudioInputSourcesChangedAsync(AudioInputSourcesChangeReason.EnumerationCompleted);
    }

    private void DeviceWatcher_Stopped(DeviceWatcher sender, object args)
    {
        _ = RaiseAudioInputSourcesChangedAsync(AudioInputSourcesChangeReason.Stopped);
    }

    private async Task RaiseAudioInputSourcesChangedAsync(AudioInputSourcesChangeReason reason, string? affectedSourceId = null)
    {
        await _refreshLock.WaitAsync();

        try
        {
            IReadOnlyList<AudioInputSource> sources = await GetAudioInputSourcesAsync();
            AudioInputSourcesChanged?.Invoke(this, new AudioInputSourcesChangedEventArgs(reason, sources, affectedSourceId));
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}
