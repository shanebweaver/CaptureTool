namespace CaptureTool.Domain.Audio.Interfaces;

public interface IAudioInputService
{
    Task<IReadOnlyList<AudioInputDevice>> GetAudioInputDevicesAsync();
}
