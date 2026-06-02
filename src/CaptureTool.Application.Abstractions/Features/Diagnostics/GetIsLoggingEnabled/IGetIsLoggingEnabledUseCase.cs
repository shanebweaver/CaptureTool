using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.Diagnostics.GetIsLoggingEnabled;

public interface IGetIsLoggingEnabledUseCase : IUseCase<GetIsLoggingEnabledRequest, GetIsLoggingEnabledResponse>
{
}