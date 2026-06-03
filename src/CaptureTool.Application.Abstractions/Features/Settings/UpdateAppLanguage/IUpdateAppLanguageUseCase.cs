using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.Settings.UpdateAppLanguage;

public interface IUpdateAppLanguageUseCase : IUseCase<UpdateAppLanguageRequest, UpdateAppLanguageResponse>, IConditional<UpdateAppLanguageRequest>
{
}