using System;
using System.Threading;
using CaptureTool.Services.Navigation;
using CaptureTool.ViewModels;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace CaptureTool.UI;

public sealed partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; } = ViewModelLocator.MainWindow;

    private readonly CancellationTokenSource _activationCts = new();

    public MainWindow()
    {
        InitializeComponent();
        Activated += OnActivated;
        Closed += OnClosed;
        SizeChanged += OnSizeChanged;
        VisibilityChanged += OnVisibilityChanged;
        ViewModel.NavigationRequested += OnViewModelNavigationRequested;
    }

    private async void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        AppWindow.MoveAndResize(new(48, 48, 540, 320), Microsoft.UI.Windowing.DisplayArea.Primary);

        if (args.WindowActivationState == WindowActivationState.CodeActivated && ViewModel.IsUnloaded)
        {
            await ViewModel.LoadAsync(null, _activationCts.Token);
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Activated -= OnActivated;
        Closed -= OnClosed;

        ViewModel.NavigationRequested -= OnViewModelNavigationRequested;

        _activationCts.Cancel();
        _activationCts.Dispose();
    }

    private void OnSizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        Size newSize = args.Size;
        // TODO: Save size to settings
    }

    private void OnVisibilityChanged(object sender, WindowVisibilityChangedEventArgs args)
    {
        // TODO: Figure out how this affects the size. We don't want to restore a minimized window to the previous size (0,0).
    }

    private void OnViewModelNavigationRequested(NavigationRequest navigationRequest)
    {
        Type viewType = ViewLocator.GetViewType(navigationRequest.Key);
        NavigationFrame.Navigate(viewType, navigationRequest.Parameter);
    }
}
