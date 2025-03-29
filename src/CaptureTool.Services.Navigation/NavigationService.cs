using System;
using System.Collections.Generic;

namespace CaptureTool.Services.Navigation;

public class NavigationService : INavigationService
{
    private readonly Stack<NavigationRequest> _navigationStack = new();
    private INavigationHandler? _navigationHandler;

    public bool CanGoBack => _navigationStack.Count > 1;

    public void SetNavigationHandler(INavigationHandler navigationHandler)
    {
        _navigationHandler = navigationHandler;
    }

    public void ClearNavigationHistory()
    {
        NavigationRequest current = _navigationStack.Pop();
        _navigationStack.Clear();
        _navigationStack.Push(current);
    }

    public void GoBack()
    {
        _navigationStack.Pop();
        NavigationRequest backRequest = _navigationStack.Peek();
        Navigate(new NavigationRequest(backRequest.Key, backRequest.Parameter, true));
    }

    public void Navigate(string key, object? parameter = null)
    {
        NavigationRequest request = new(key, parameter);
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
    }
}