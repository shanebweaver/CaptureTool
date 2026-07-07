using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Settings.UpdateAppLanguage;

public interface IUpdateAppLanguageUseCase : IUseCase<UpdateAppLanguageRequest, UpdateAppLanguageResponse>, IConditional<UpdateAppLanguageRequest>
{
}