namespace CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;

public interface IImageSuperResolutionPreparationConsentService
{
    Task<bool> ConfirmPreparationAsync(CancellationToken cancellationToken = default);
}
