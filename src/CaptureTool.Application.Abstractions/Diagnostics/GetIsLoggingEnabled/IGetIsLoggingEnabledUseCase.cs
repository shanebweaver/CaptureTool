using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Diagnostics.GetIsLoggingEnabled;

public interface IGetIsLoggingEnabledUseCase : IUseCase<GetIsLoggingEnabledRequest, GetIsLoggingEnabledResponse>
{
}