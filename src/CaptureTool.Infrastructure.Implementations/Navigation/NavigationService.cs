using CaptureTool.Infrastructure.Interfaces.Navigation;

namespace CaptureTool.Infrastructure.Implementations.Navigation;

public class NavigationService : INavigationService
{
    private readonly Stack<NavigationRequest> _navigationStack = new();
    private readonly Lock _navigationLock = new();
    private INavigationHandler? _navigationHandler;

    public event EventHandler<INavigationEventArgs>? Navigated;

    public INavigationRequest? CurrentRequest => _navigationStack.Count == 0 ? null : _navigationStack.Peek();
    public bool CanGoBack => _navigationStack.Count > 1;

    public void SetNavigationHandler(INavigationHandler navigationHandler)
    {
        _navigationHandler = navigationHandler;
    }

    public bool TryGoBack()
    {
        lock (_navigationLock)
        {
            if (_navigationStack.Count <= 1)
            {
                return false;
            }

            INavigationRequest currentRequest = _navigationStack.Pop();
            INavigationRequest backRequest = _navigationStack.Peek();

            bool requestsMatch = CompareRequests(currentRequest, backRequest);
            if (!requestsMatch)
            {
                RequestNavigation(new NavigationRequest(backRequest.Route, backRequest.Parameter, true, false));
            }

            return true;
        }
    }

    public bool TryGoBackTo(Func<INavigationRequest, bool> assesRequest)
    {
        lock (_navigationLock)
        {
            if (_navigationStack.Count <= 1)
            {
                return false;
            }

            var entries = _navigationStack.ToArray();
            int targetIndex = -1;
            for (int i = 1; i < entries.Length; i++)
            {
                if (assesRequest(entries[i]))
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex == -1)
            {
                return false;
            }

            var currentRequest = _navigationStack.Peek();
            var targetRequest = entries[targetIndex];
            if (CompareRequests(currentRequest, targetRequest))
            {
                return false;
            }

            for (int i = 0; i < targetIndex; i++)
            {
                _navigationStack.Pop();
            }

            NavigationRequest actualTop = _navigationStack.Peek();
            RequestNavigation(new NavigationRequest(actualTop.Route, actualTop.Parameter, isBackNavigation: true));
            return true;
        }
    }

    public void Navigate(object route, object? parameter = null, bool clearHistory = false)
    {
        lock (_navigationLock)
        {
            NavigationRequest? currentRequest = _navigationStack.Count == 0 ? null : _navigationStack.Peek();
            NavigationRequest newRequest = new(route, parameter, false, clearHistory);

            bool requestsMatch = CompareRequests(currentRequest, newRequest);
            if (requestsMatch)
            {
                return;
            }

            if (clearHistory)
            {
                _navigationStack.Clear();
            }
            
            _navigationStack.Push(newRequest);

            RequestNavigation(newRequest);
        }
    }

    private void RequestNavigation(NavigationRequest request)
    {
        if (_navigationHandler is null)
        {
            throw new InvalidOperationException("Unable to navigate. No navigation handler is set.");
        }

        _navigationHandler.HandleNavigationRequest(request);
        Navigated?.Invoke(this, new NavigationEventArgs(request));
    }

    private static bool CompareRequests(INavigationRequest? requestA, INavigationRequest? requestB)
    {
        return Equals(requestA?.Route, requestB?.Route) && Equals(requestA?.Parameter, requestB?.Parameter);
    }
}