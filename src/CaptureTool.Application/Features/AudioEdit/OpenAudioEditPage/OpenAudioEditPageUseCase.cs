using CaptureTool.Application.Abstractions.Features.AudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioEdit.OpenAudioEditPage;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Features.AudioEdit.OpenAudioEditPage;

internal sealed class OpenAudioEditPageUseCase : IOpenAudioEditPageUseCase
{
    private const string ActivityId = "OpenAudioEditPage";

    private readonly INavigationService _navigationService;
    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IFileSystem _fileSystem;
    private readonly IAudioCaptureNavigationGuard _audioCaptureNavigationGuard;

    public OpenAudioEditPageUseCase(
        INavigationService navigationService,
        IFileSystem fileSystem,
        IUseCaseExecutor useCaseExecutor,
        IAudioCaptureNavigationGuard audioCaptureNavigationGuard)
    {
        _navigationService = navigationService;
        _fileSystem = fileSystem;
        _audioCaptureNavigationGuard = audioCaptureNavigationGuard;
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
                if (!await _audioCaptureNavigationGuard.CanNavigateAwayFromActiveCaptureAsync(cancellationToken))
                {
                    return new OpenAudioEditPageResponse(false);
                }

                _navigationService.Navigate(NavigationRoute.AudioEdit, request.AudioFile);
                return new OpenAudioEditPageResponse();
            },
            cancellationToken: cancellationToken);
    }
}
