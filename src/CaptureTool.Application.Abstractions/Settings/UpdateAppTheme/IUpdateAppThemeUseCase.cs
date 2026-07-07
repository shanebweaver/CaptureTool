using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Settings.UpdateAppTheme;

public interface IUpdateAppThemeUseCase : IUseCase<UpdateAppThemeRequest, UpdateAppThemeResponse>, IConditional<UpdateAppThemeRequest>
{
}