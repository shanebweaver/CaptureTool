using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Telemetry;

namespace CaptureTool.Infrastructure.Navigation;

public class NavigationService : INavigationService
{
    private readonly Stack<NavigationRequest> _navigationStack = new();
    private readonly Lock _stateLock = new();
    private readonly SemaphoreSlim _transitionGate = new(1, 1);
    private readonly ITelemetryService? _telemetryService;
    private INavigationHandler? _navigationHandler;

    public event EventHandler<INavigationEventArgs>? Navigated;

    public INavigationRequest? CurrentRequest
    {
        get
        {
            lock (_stateLock)
            {
                return _navigationStack.Count == 0 ? null : _navigationStack.Peek();
            }
        }
    }

    public bool CanGoBack
    {
        get
        {
            lock (_stateLock)
            {
                return _navigationStack.Count > 1;
            }
        }
    }

    public NavigationService(ITelemetryService? telemetryService = null)
    {
        _telemetryService = telemetryService;
    }

    public void SetNavigationHandler(INavigationHandler navigationHandler)
    {
        ArgumentNullException.ThrowIfNull(navigationHandler);

        lock (_stateLock)
        {
            _navigationHandler = navigationHandler;
        }
    }

    public async Task<NavigationResult> NavigateAsync(
        object route,
        object? parameter = null,
        bool clearHistory = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(route);

        await _transitionGate.WaitAsync(cancellationToken);
        try
        {
            NavigationRequest? currentRequest;
            lock (_stateLock)
            {
                currentRequest = _navigationStack.Count == 0 ? null : _navigationStack.Peek();
            }

            NavigationRequest newRequest = new(route, parameter, false, clearHistory);
            if (CompareRequests(currentRequest, newRequest))
            {
                return NavigationResult.NoChange;
            }

            NavigationResult result = await DispatchAsync(newRequest, cancellationToken);
            if (result != NavigationResult.Accepted)
            {
                return result;
            }

            lock (_stateLock)
            {
                if (clearHistory)
                {
                    _navigationStack.Clear();
                }

                _navigationStack.Push(newRequest);
            }

            CompleteNavigation(newRequest, currentRequest?.Route);
            return NavigationResult.Accepted;
        }
        finally
        {
            _transitionGate.Release();
        }
    }

    public async Task<NavigationResult> TryGoBackAsync(CancellationToken cancellationToken = default)
    {
        await _transitionGate.WaitAsync(cancellationToken);
        try
        {
            NavigationRequest currentRequest;
            NavigationRequest backRequest;
            lock (_stateLock)
            {
                if (_navigationStack.Count <= 1)
                {
                    return NavigationResult.NoChange;
                }

                NavigationRequest[] entries = _navigationStack.ToArray();
                currentRequest = entries[0];
                backRequest = entries[1];
            }

            NavigationRequest candidate = new(
                backRequest.Route,
                backRequest.Parameter,
                isBackNavigation: true,
                clearHistory: false);
            NavigationResult result = await DispatchAsync(candidate, cancellationToken);
            if (result != NavigationResult.Accepted)
            {
                return result;
            }

            lock (_stateLock)
            {
                _navigationStack.Pop();
            }

            CompleteNavigation(candidate, currentRequest.Route);
            return NavigationResult.Accepted;
        }
        finally
        {
            _transitionGate.Release();
        }
    }

    public async Task<NavigationResult> TryGoBackToAsync(
        Func<INavigationRequest, bool> assessRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assessRequest);

        await _transitionGate.WaitAsync(cancellationToken);
        try
        {
            NavigationRequest currentRequest;
            NavigationRequest targetRequest;
            NavigationRequest[] entries;
            int targetIndex;
            lock (_stateLock)
            {
                if (_navigationStack.Count <= 1)
                {
                    return NavigationResult.NoChange;
                }

                entries = _navigationStack.ToArray();
            }

            targetIndex = Array.FindIndex(entries, 1, request => assessRequest(request));
            if (targetIndex == -1)
            {
                return NavigationResult.NoChange;
            }

            currentRequest = entries[0];
            targetRequest = entries[targetIndex];

            NavigationRequest candidate = new(
                targetRequest.Route,
                targetRequest.Parameter,
                isBackNavigation: true,
                clearHistory: false);
            NavigationResult result = await DispatchAsync(candidate, cancellationToken);
            if (result != NavigationResult.Accepted)
            {
                return result;
            }

            lock (_stateLock)
            {
                for (int i = 0; i < targetIndex; i++)
                {
                    _navigationStack.Pop();
                }
            }

            CompleteNavigation(candidate, currentRequest.Route);
            return NavigationResult.Accepted;
        }
        finally
        {
            _transitionGate.Release();
        }
    }

    private Task<NavigationResult> DispatchAsync(
        NavigationRequest request,
        CancellationToken cancellationToken)
    {
        INavigationHandler handler;
        lock (_stateLock)
        {
            handler = _navigationHandler ??
                throw new InvalidOperationException("Unable to navigate. No navigation handler is set.");
        }

        return handler.HandleNavigationRequestAsync(request, cancellationToken);
    }

    private void CompleteNavigation(NavigationRequest request, object? fromRoute)
    {
        TrackNavigation(request, fromRoute);
        Navigated?.Invoke(this, new NavigationEventArgs(request));
    }

    private void TrackNavigation(NavigationRequest request, object? fromRoute)
    {
        var properties = new Dictionary<string, object?>
        {
            [TelemetryProperties.ToRoute] = GetSafeRouteName(request.Route),
            [TelemetryProperties.IsBackNavigation] = request.IsBackNavigation,
            [TelemetryProperties.ClearHistory] = request.ClearHistory
        };

        if (fromRoute is not null)
        {
            properties[TelemetryProperties.FromRoute] = GetSafeRouteName(fromRoute);
        }

        if (request.Parameter is not null)
        {
            properties[TelemetryProperties.ParameterType] = request.Parameter.GetType().Name;
        }

        _telemetryService?.TrackEvent(TelemetryEvents.NavigationCompleted, properties);
    }

    private static string GetSafeRouteName(object route)
    {
        return route is Enum
            ? route.ToString() ?? route.GetType().Name
            : route.GetType().Name;
    }

    private static bool CompareRequests(INavigationRequest? requestA, INavigationRequest? requestB)
    {
        return Equals(requestA?.Route, requestB?.Route) && Equals(requestA?.Parameter, requestB?.Parameter);
    }
}
