using CaptureTool.Application.Abstractions.Capture.Overlay.OpenSelectionOverlay;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Capture.Overlay.OpenSelectionOverlay;

internal sealed class OpenSelectionOverlayUseCase : IOpenSelectionOverlayUseCase
{
    private const string ActivityId = "OpenSelectionOverlay";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly INavigationCoordinator _navigationCoordinator;
    private readonly IActiveEditSessionService _activeEditSessionService;

    public OpenSelectionOverlayUseCase(
        INavigationCoordinator navigationCoordinator,
        IActiveEditSessionService activeEditSessionService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _navigationCoordinator = navigationCoordinator;
        _activeEditSessionService = activeEditSessionService;
    }

    public Task<UseCaseResponse<OpenSelectionOverlayResponse>> ExecuteAsync(OpenSelectionOverlayRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async token =>
            {
                IEditableSession? editSessionToRetire = _activeEditSessionService.CurrentSession is { HasUnsavedChanges: true } session
                    ? session
                    : null;

                bool navigated = editSessionToRetire is null
                    ? await NavigateToSelectionOverlayAsync(request, token)
                    : await _navigationCoordinator.ExecuteTransitionAsync(
                        async transitionToken =>
                        {
                            bool homeCreated = await _navigationCoordinator.NavigateAsync(
                                NavigationRoute.Home,
                                clearHistory: true,
                                cancellationToken: transitionToken);

                            return homeCreated && await NavigateToSelectionOverlayAsync(request, transitionToken);
                        },
                        token);

                return new OpenSelectionOverlayResponse(navigated);
            },
            cancellationToken: cancellationToken);
    }

    private Task<bool> NavigateToSelectionOverlayAsync(
        OpenSelectionOverlayRequest request,
        CancellationToken cancellationToken)
    {
        return _navigationCoordinator.NavigateAsync(
            NavigationRoute.SelectionOverlay,
            request.CaptureOptions,
            cancellationToken: cancellationToken);
    }
}
