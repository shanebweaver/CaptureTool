using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Settings.RestoreDefaults;

public interface IRestoreDefaultsUseCase : IUseCase<RestoreDefaultsRequest, RestoreDefaultsResponse>, IConditional<RestoreDefaultsRequest>
{
}