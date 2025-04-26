using System;
using System.Collections.Generic;

namespace CaptureTool.Services.Navigation;

public class NavigationService : INavigationService
{
    private readonly Stack<NavigationRequest> _navigationStack = new();
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
        _navigationStack.Pop();
        NavigationRequest backRequest = _navigationStack.Peek();
        Navigate(new NavigationRequest(backRequest.Route, backRequest.Parameter, true));
    }

    public void Navigate(NavigationRoute route, object? parameter = null, bool clearHistory = false)
    {
        if (clearHistory)
        {
            _navigationStack.Clear();
        }

        NavigationRequest request = new(route, parameter);
        _navigationStack.Push(request);
        Navigate(request);
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