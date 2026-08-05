using CaptureTool.Application.Abstractions.Edit.Audio.OpenAudioEditPage;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Edit.Audio.OpenAudioEditPage;

internal sealed class OpenAudioEditPageUseCase : IOpenAudioEditPageUseCase
{
    private const string ActivityId = "OpenAudioEditPage";

    private readonly INavigationCoordinator _navigationCoordinator;
    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IFileSystem _fileSystem;

    public OpenAudioEditPageUseCase(
        INavigationCoordinator navigationCoordinator,
        IFileSystem fileSystem,
        IUseCaseExecutor useCaseExecutor)
    {
        _navigationCoordinator = navigationCoordinator;
        _fileSystem = fileSystem;
        _useCaseExecutor = useCaseExecutor;
    }

    public bool CanExecute(OpenAudioEditPageRequest request)
    {
        bool canExecute = _fileSystem.FileExists(request.AudioFile.FilePath);
        return canExecute;
    }

    public Task<UseCaseResponse<OpenAudioEditPageResponse>> ExecuteAsync(OpenAudioEditPageRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                bool navigated = await _navigationCoordinator.NavigateAsync(
                    NavigationRoute.AudioEdit,
                    request.AudioFile,
                    cancellationToken: cancellationToken);
                return new OpenAudioEditPageResponse(navigated);
            },
            cancellationToken: cancellationToken);
    }
}
