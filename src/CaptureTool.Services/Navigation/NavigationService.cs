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

    public bool CanGoBack => _navigationStack.Count > 1;

    public NavigationRoute CurrentRoute => _navigationStack.Peek().Route;

    public void SetNavigationHandler(INavigationHandler navigationHandler)
    {
        _navigationHandler = navigationHandler;
    }

    public void GoBack()
    {
        lock (_navigationLock)
        {
            if (_navigationStack.Count <= 1)
                throw new InvalidOperationException("Cannot go back. No previous navigation entry exists.");

            _navigationStack.Pop();
            NavigationRequest backRequest = _navigationStack.Peek();
            Navigate(new NavigationRequest(backRequest.Route, backRequest.Parameter, true));
        }
    }

    public void Navigate(NavigationRoute route, object? parameter = null, bool clearHistory = false)
    {
        lock (_navigationLock)
        {
            NavigationRequest request = new(route, parameter);
            if (_navigationStack.TryPeek(out NavigationRequest lastRequest) &&
                lastRequest.Route == request.Route &&
                lastRequest.Parameter == request.Parameter)
            {
                // If the last request is the same as the current one, do not navigate again.
                return;
            }

            if (clearHistory)
            {
                _navigationStack.Clear();
            }

            _navigationStack.Push(request);
            Navigate(request);
        }
    }

    private void Navigate(NavigationRequest request)
    {
        if (_navigationHandler is null)
        {
            throw new InvalidOperationException("Unable to navigate. No navigation handler is set.");
        }

        _navigationHandler.HandleNavigationRequest(request);
        Navigated?.Invoke(this, new NavigationEventArgs(request));
    }
}