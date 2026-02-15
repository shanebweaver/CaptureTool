using CaptureTool.Domain.Audio.Interfaces;
using Windows.Devices.Enumeration;
using Windows.Media.Devices;

namespace CaptureTool.Domain.Audio.Implementations.Windows;

public sealed class WindowsAudioInputService : IAudioInputService
{
    public async Task<IReadOnlyList<AudioInputDevice>> GetAudioInputDevicesAsync()
    {
        var devices = new List<AudioInputDevice>();

        try
        {
            // Get the default audio capture device selector
            string deviceSelector = MediaDevice.GetAudioCaptureSelector();

            // Find all audio capture devices
            var deviceInfoCollection = await DeviceInformation.FindAllAsync(deviceSelector);

            foreach (var deviceInfo in deviceInfoCollection)
            {
                if (deviceInfo.IsEnabled)
                {
                    devices.Add(new AudioInputDevice(deviceInfo.Id, deviceInfo.Name));
                }
            }
        }
        catch
        {
            // If enumeration fails, return empty list
            // This can happen if there are no audio devices or permissions are denied
        }

        return devices;
    }
}
