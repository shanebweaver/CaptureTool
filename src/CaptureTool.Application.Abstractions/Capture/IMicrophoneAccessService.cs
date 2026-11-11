namespace CaptureTool.Application.Abstractions.Capture;

public enum MicrophoneAccessResult
{
    Granted,
    Declined,
    SettingsRequired
}

public interface IMicrophoneAccessService
{
    /// <summary>Call from the foreground UI thread to request Windows consent before starting microphone capture.</summary>
    Task<MicrophoneAccessResult> RequestAccessAsync(CancellationToken cancellationToken = default);
}

public interface IMicrophoneAccessPrompt
{
    /// <summary>Offers to open microphone privacy settings. Does not grant access or start recording.</summary>
    Task ShowAsync(CancellationToken cancellationToken = default);
}
