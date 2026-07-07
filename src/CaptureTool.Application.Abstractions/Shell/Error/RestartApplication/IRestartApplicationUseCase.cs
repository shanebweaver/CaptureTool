using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Shell.Error.RestartApplication;

public interface IRestartApplicationUseCase : IUseCase<RestartApplicationRequest, RestartApplicationResponse>, IConditional<RestartApplicationRequest>
{
}