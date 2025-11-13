using System;
using System.Collections.Generic;
using System.Threading;

namespace CaptureTool.Services.Navigation;

public class NavigationService : INavigationService
{
    private readonly Stack<NavigationRequest> _navigationStack = new();
    private readonly Lock _navigationLock = new();
    private INavigationHandler? _navigationHandler;

    public event EventHandler<NavigationEventArgs>? Navigated;

    public NavigationRequest? CurrentRequest => _navigationStack.Count == 0 ? null : _navigationStack.Peek();
    public bool CanGoBack => _navigationStack.Count > 1;

    public void SetNavigationHandler(INavigationHandler navigationHandler)
    {
        _navigationHandler = navigationHandler;
    }

    public void GoBack()
    {
        lock (_navigationLock)
        {
            if (_navigationStack.Count <= 1)
            {
                throw new InvalidOperationException("Cannot go back. No previous navigation entry exists.");
            }

            NavigationRequest currentRequest = _navigationStack.Pop();
            NavigationRequest backRequest = _navigationStack.Peek();
            bool requestsMatch = CompareRequests(currentRequest, backRequest);
            if (requestsMatch)
            {
                return;
            }

            RequestNavigation(new NavigationRequest(backRequest.Route, backRequest.Parameter, true, false));
        }
    }

    public bool TryGoBackWhile(Func<NavigationRequest, bool> assesRequest)
    {
        lock (_navigationLock)
        {
            if (_navigationStack.Count <= 1)
            {
                throw new InvalidOperationException("Cannot go back. No previous navigation entry exists.");
            }

            NavigationRequest? currentRequest = _navigationStack.Count == 0 ? null : _navigationStack.Peek();
            NavigationRequest backRequest;
            do
            {
                if (_navigationStack.Count == 0)
                {
                    return false;
                }

                _navigationStack.Pop();
                backRequest = _navigationStack.Peek();
            }
            while (assesRequest(backRequest));

            bool requestsMatch = CompareRequests(currentRequest, backRequest);
            if (requestsMatch)
            {
                return false;
            }

            RequestNavigation(new NavigationRequest(backRequest.Route, backRequest.Parameter, true));
            return true;
        }
    }

    public void Navigate(NavigationRoute route, object? parameter = null, bool clearHistory = false)
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

    private static bool CompareRequests(NavigationRequest? requestA, NavigationRequest? requestB)
    {
        return requestA?.Route == requestB?.Route && requestA?.Parameter == requestB?.Parameter;
    }
}