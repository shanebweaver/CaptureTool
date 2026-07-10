using CaptureTool.Application.Abstractions.Edit.External;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Edit.External;

internal sealed class OpenExternalEditorUseCase : IOpenExternalEditorUseCase
{
    private const string ActivityId = "OpenExternalEditor";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IExternalMediaEditorLauncher _launcher;
    private readonly IFileSystem _fileSystem;

    public OpenExternalEditorUseCase(
        IExternalMediaEditorLauncher launcher,
        IFileSystem fileSystem,
        IUseCaseExecutor useCaseExecutor)
    {
        _launcher = launcher;
        _fileSystem = fileSystem;
        _useCaseExecutor = useCaseExecutor;
    }

    public bool CanExecute(OpenExternalEditorRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.MediaPath) &&
            _fileSystem.FileExists(request.MediaPath);
    }

    public Task<UseCaseResponse<OpenExternalEditorResponse>> ExecuteAsync(
        OpenExternalEditorRequest request,
        CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                if (!CanExecute(request) || cancellationToken.IsCancellationRequested)
                {
                    return new OpenExternalEditorResponse(false);
                }

                bool opened = await _launcher.TryOpenFileAsync(
                    request.MediaPath,
                    request.Editor,
                    cancellationToken);

                return new OpenExternalEditorResponse(opened);
            },
            cancellationToken: cancellationToken);
    }
}
