using System;
using System.Threading;
using CaptureTool.Services.Navigation;
using CaptureTool.ViewModels;
using Microsoft.UI.Xaml;

namespace CaptureTool.UI;

public sealed partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; } = ViewModelLocator.MainWindow;

    public MainWindow()
    {
        InitializeComponent();
        Activated += OnActivated;
        Closed += OnClosed;
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Activated -= OnActivated;
        Closed -= OnClosed;

        ViewModel.NavigationRequested -= OnViewModelNavigationRequested;
    }

    private async void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.CodeActivated)
        {
            Activated -= OnActivated;

            ViewModel.NavigationRequested += OnViewModelNavigationRequested;
            await ViewModel.LoadAsync(null, CancellationToken.None);
        }
    }

    private void OnViewModelNavigationRequested(NavigationRequest navigationRequest)
    {
        Type viewType = ViewLocator.GetViewType(navigationRequest.Key);
        NavigationFrame.Navigate(viewType, navigationRequest.Parameter);
    }
}
