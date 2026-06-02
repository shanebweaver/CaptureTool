using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.Settings.OpenSettingsPage;

public interface IOpenSettingsPageUseCase : IUseCase<OpenSettingsPageRequest, OpenSettingsPageResponse>, IConditional<OpenSettingsPageRequest>
{
}