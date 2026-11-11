using CaptureTool.Application.Abstractions.Capture;
using System.Runtime.InteropServices;
using Windows.Devices.Enumeration;
using Windows.Security.Authorization.AppCapabilityAccess;

namespace CaptureTool.Infrastructure.Capture.Windows;

public sealed class WindowsMicrophoneAccessService : IMicrophoneAccessService
{
    private readonly Func<AppCapabilityAccessStatus> _check;
    private readonly Func<CancellationToken, Task<AppCapabilityAccessStatus>> _request;
    private readonly Func<DeviceAccessStatus> _deviceAccess;

    public WindowsMicrophoneAccessService() : this(
        () => AppCapability.Create("microphone").CheckAccess(),
        ct => AppCapability.Create("microphone").RequestAccessAsync().AsTask(ct),
        () => DeviceAccessInformation.CreateFromDeviceClass(DeviceClass.AudioCapture).CurrentStatus)
    {
    }

    internal WindowsMicrophoneAccessService(Func<AppCapabilityAccessStatus> check,
        Func<CancellationToken, Task<AppCapabilityAccessStatus>> request, Func<DeviceAccessStatus> deviceAccess)
    {
        _check = check;
        _request = request;
        _deviceAccess = deviceAccess;
    }

    public async Task<MicrophoneAccessResult> RequestAccessAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            AppCapabilityAccessStatus status = _check();
            if (status == AppCapabilityAccessStatus.UserPromptRequired)
            {
                // Windows requires the foreground UI apartment for its consent prompt.
                status = await _request(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (status == AppCapabilityAccessStatus.DeniedByUser)
                {
                    // Respect a declined native prompt without immediately showing another dialog.
                    return MicrophoneAccessResult.Declined;
                }
            }

            if (status is not (AppCapabilityAccessStatus.Allowed or AppCapabilityAccessStatus.NotDeclaredByApp))
            {
                return MicrophoneAccessResult.SettingsRequired;
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException || exception.HResult == unchecked((int)0x80070005))
        {
            return MicrophoneAccessResult.SettingsRequired;
        }
        catch (Exception exception) when (exception is COMException or NotSupportedException)
        {
            // Some unpackaged desktop configurations do not expose app capabilities.
            // Use their device privacy status; the recorder also checks before native capture.
        }

        cancellationToken.ThrowIfCancellationRequested();
        return _deviceAccess() is DeviceAccessStatus.DeniedByUser or DeviceAccessStatus.DeniedBySystem
            ? MicrophoneAccessResult.SettingsRequired
            : MicrophoneAccessResult.Granted;
    }
}
