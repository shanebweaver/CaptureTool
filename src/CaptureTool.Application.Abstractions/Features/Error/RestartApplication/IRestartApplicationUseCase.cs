using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.Error.RestartApplication;

public interface IRestartApplicationUseCase : IUseCase<RestartApplicationRequest, RestartApplicationResponse>, IConditional<RestartApplicationRequest>
{
}