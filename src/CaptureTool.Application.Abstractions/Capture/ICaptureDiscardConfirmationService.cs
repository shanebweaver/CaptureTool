namespace CaptureTool.Application.Abstractions.Capture;

public interface ICaptureDiscardConfirmationService
{
    Task<bool> ConfirmDiscardActiveCaptureAsync(CancellationToken cancellationToken = default);
}
