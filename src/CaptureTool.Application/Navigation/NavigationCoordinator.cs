using CaptureTool.Application.Abstractions.Capture.Audio;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Navigation;

namespace CaptureTool.Application.Navigation;

internal sealed class NavigationCoordinator : INavigationCoordinator
{
    private readonly INavigationService _navigationService;
    private readonly IEditSessionGuard _editSessionGuard;
    private readonly IAudioCaptureNavigationGuard _audioCaptureNavigationGuard;
    private readonly SemaphoreSlim _transitionGate = new(1, 1);
    private readonly AsyncLocal<int> _transitionDepth = new();

    public NavigationCoordinator(
        INavigationService navigationService,
        IEditSessionGuard editSessionGuard,
        IAudioCaptureNavigationGuard audioCaptureNavigationGuard)
    {
        _navigationService = navigationService;
        _editSessionGuard = editSessionGuard;
        _audioCaptureNavigationGuard = audioCaptureNavigationGuard;
    }

    public INavigationRequest? CurrentRequest => _navigationService.CurrentRequest;

    public bool CanGoBack => _navigationService.CanGoBack;

    public Task<bool> ExecuteTransitionAsync(
        Func<CancellationToken, Task<bool>> transition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transition);

        return ExecuteGuardedTransitionAsync(
            canSkipLeavePolicy: static () => false,
            transition,
            cancellationToken);
    }

    public Task<bool> NavigateAsync(
        object route,
        object? parameter = null,
        bool clearHistory = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(route);

        return ExecuteGuardedTransitionAsync(
            canSkipLeavePolicy: () => RequestsMatch(_navigationService.CurrentRequest, route, parameter),
            transition: async token =>
                await _navigationService.NavigateAsync(route, parameter, clearHistory, token) == NavigationResult.Accepted,
            cancellationToken);
    }

    public Task<bool> TryGoBackAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteGuardedTransitionAsync(
            canSkipLeavePolicy: () => !_navigationService.CanGoBack,
            transition: async token =>
                await _navigationService.TryGoBackAsync(token) == NavigationResult.Accepted,
            cancellationToken,
            skippedResult: false);
    }

    public Task<bool> TryGoBackToAsync(
        Func<INavigationRequest, bool> assessRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assessRequest);

        return ExecuteGuardedTransitionAsync(
            canSkipLeavePolicy: () => !_navigationService.CanGoBack,
            transition: async token =>
                await _navigationService.TryGoBackToAsync(assessRequest, token) == NavigationResult.Accepted,
            cancellationToken,
            skippedResult: false);
    }

    private async Task<bool> ExecuteGuardedTransitionAsync(
        Func<bool> canSkipLeavePolicy,
        Func<CancellationToken, Task<bool>> transition,
        CancellationToken cancellationToken,
        bool skippedResult = true)
    {
        if (_transitionDepth.Value > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await transition(cancellationToken);
        }

        await _transitionGate.WaitAsync(cancellationToken);
        try
        {
            if (canSkipLeavePolicy())
            {
                return skippedResult;
            }

            if (!await CanLeaveCoreAsync(cancellationToken))
            {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            _transitionDepth.Value++;
            try
            {
                return await transition(cancellationToken);
            }
            finally
            {
                _transitionDepth.Value--;
            }
        }
        finally
        {
            _transitionGate.Release();
        }
    }

    private async Task<bool> CanLeaveCoreAsync(CancellationToken cancellationToken)
    {
        if (!await _editSessionGuard.CanLeaveCurrentSessionAsync(cancellationToken))
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return await _audioCaptureNavigationGuard.CanNavigateAwayFromActiveCaptureAsync(cancellationToken);
    }

    private static bool RequestsMatch(INavigationRequest? request, object route, object? parameter)
    {
        return Equals(request?.Route, route) && Equals(request?.Parameter, parameter);
    }
}
