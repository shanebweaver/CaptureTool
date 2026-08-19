using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Shutdown;
using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Application.Abstractions.Windowing;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Presentation.Shell;
using CaptureTool.Presentation.Windows.WinUI.AudioCapture;
using CaptureTool.Presentation.Windows.WinUI.Capture;
using CaptureTool.Presentation.Windows.WinUI.Edit;
using CaptureTool.Presentation.Windows.WinUI.EditSessions;
using CaptureTool.Presentation.Windows.WinUI.Telemetry;
using CaptureTool.Presentation.Windows.WinUI.UiTests;
using CaptureTool.Presentation.Windows.WinUI.Utils;
using CaptureTool.Presentation.Windows.WinUI.Xaml.Pages;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Runtime.InteropServices;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Windows;

public sealed partial class MainWindow : Window
{
    private static readonly SizeInt32 DefaultWindowSize = new(1000, 750);
    private static readonly SizeInt32 MinWindowSize = new(500, 374);
    private const int NoPackageIdentityHResult = unchecked((int)0x80073D54);

    private readonly INavigationCoordinator _navigationCoordinator;
    private readonly ILogService _logService;
    private readonly IMainWindowActivationService _mainWindowActivationService;
    private readonly IShutdownHandler _shutdownHandler;
    private readonly WinUIAudioCaptureNavigationConfirmationService _audioCaptureNavigationConfirmationService;
    private readonly WinUICaptureDiscardConfirmationService _captureDiscardConfirmationService;
    private readonly WinUIEditSessionConfirmationService _editSessionConfirmationService;
    private readonly AiFeatureConsentDialogService _aiFeatureConsentDialogService;
    private readonly ImageSuperResolutionPreparationConsentService _imageSuperResolutionPreparationConsentService;
    private readonly TelemetryConsentDialogService _telemetryConsentDialogService;
    private readonly DispatcherQueueTimer _notificationTimer;

    public MainWindowViewModel ViewModel { get; } = ViewModelLocator.GetViewModel<MainWindowViewModel>();
    private bool _closeConfirmed;
    private bool _closeConfirmationInProgress;
    private bool _isShown;
    private bool _uiTestLaunchNavigationHandled;

    public MainWindow()
    {
        _navigationCoordinator = App.Current.ServiceProvider.GetService<INavigationCoordinator>();
        _logService = App.Current.ServiceProvider.GetService<ILogService>();
        _mainWindowActivationService = App.Current.ServiceProvider.GetService<IMainWindowActivationService>();
        _shutdownHandler = App.Current.ServiceProvider.GetService<IShutdownHandler>();
        _audioCaptureNavigationConfirmationService = App.Current.ServiceProvider.GetService<WinUIAudioCaptureNavigationConfirmationService>();
        _captureDiscardConfirmationService = App.Current.ServiceProvider.GetService<WinUICaptureDiscardConfirmationService>();
        _editSessionConfirmationService = App.Current.ServiceProvider.GetService<WinUIEditSessionConfirmationService>();
        _aiFeatureConsentDialogService = App.Current.ServiceProvider.GetService<AiFeatureConsentDialogService>();
        _imageSuperResolutionPreparationConsentService = App.Current.ServiceProvider.GetService<ImageSuperResolutionPreparationConsentService>();
        _telemetryConsentDialogService = App.Current.ServiceProvider.GetService<TelemetryConsentDialogService>();

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = MinWindowSize.Width;
            presenter.PreferredMinimumHeight = MinWindowSize.Height;
        }

        InitializeComponent();
        _notificationTimer = DispatcherQueue.CreateTimer();
        _notificationTimer.Interval = TimeSpan.FromSeconds(6);
        _notificationTimer.Tick += NotificationTimer_Tick;
        RootGrid.Loaded += RootGrid_Loaded;

        AppTitleBar.Loaded += AppTitleBar_Loaded;
        AppTitleBar.SizeChanged += AppTitleBar_SizeChanged;
        ExtendsContentIntoTitleBar = true;
        UpdateAppTitle();

        Activated += OnActivated;
        Activated += OnActivationStateChanged;
        AppWindow.Closing += OnAppWindowClosing;
        Closed += OnClosed;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        UpdateRequestedAppTheme();
        UpdateTitleBarColors();
        RestartNotificationTimer();
    }

    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        _editSessionConfirmationService.XamlRoot = RootGrid.XamlRoot;
        _audioCaptureNavigationConfirmationService.XamlRoot = RootGrid.XamlRoot;
        _captureDiscardConfirmationService.XamlRoot = RootGrid.XamlRoot;
        _aiFeatureConsentDialogService.XamlRoot = RootGrid.XamlRoot;
        _imageSuperResolutionPreparationConsentService.XamlRoot = RootGrid.XamlRoot;
        _telemetryConsentDialogService.XamlRoot = RootGrid.XamlRoot;
        NavigateToUiTestImageWhenRequested();

        if (_isShown)
        {
            RequestTelemetryConsentIfNeeded();
        }
    }

    internal void NotifyShown()
    {
        _isShown = true;
        _telemetryConsentDialogService.AllowPrompt();

        if (RootGrid.XamlRoot is not null)
        {
            RequestTelemetryConsentIfNeeded();
        }
    }

    internal void NotifyHidden()
    {
        _isShown = false;
        _mainWindowActivationService.SetActive(false);
        _telemetryConsentDialogService.SuppressPrompt();

        if (NavigationFrame.Content is HomePage homePage)
        {
            homePage.SuppressStoreReviewPrompt();
        }
    }

    private async void RequestTelemetryConsentIfNeeded()
    {
        if (UiTestLaunchOptions.Current.IsEnabled)
        {
            return;
        }

        try
        {
            await _telemetryConsentDialogService.RequestConsentIfNeededAsync();
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to request telemetry consent.");
        }
    }

    private void NavigateToUiTestImageWhenRequested()
    {
        UiTestLaunchOptions options = UiTestLaunchOptions.Current;
        if (_uiTestLaunchNavigationHandled ||
            !options.IsEnabled ||
            string.IsNullOrWhiteSpace(options.ImageFilePath))
        {
            return;
        }

        _uiTestLaunchNavigationHandled = true;
        DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                INavigationService navigationService = App.Current.ServiceProvider.GetService<INavigationService>();
                await navigationService.NavigateAsync(
                    NavigationRoute.ImageEdit,
                    new ImageFile(options.ImageFilePath),
                    true);
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, "Failed to navigate to the UI test image.");
            }
        });
    }

    private void UpdateAppTitle()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            string appTitle = "Capture Tool";

            if (!UiTestLaunchOptions.Current.IsEnabled &&
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            {
                try
                {
                    appTitle = AppInfo.Current.DisplayInfo.DisplayName;
                }
                catch (InvalidOperationException ex) when (ex.HResult == NoPackageIdentityHResult)
                {
                    appTitle = "Capture Tool";
                }
                catch (COMException ex) when (ex.HResult == NoPackageIdentityHResult)
                {
                    appTitle = "Capture Tool";
                }
            }

            Title = appTitle;
            TitleBarTextBlock.Text = appTitle;
        });
    }

    private void AppTitleBar_Loaded(object sender, RoutedEventArgs e)
    {
        if (ExtendsContentIntoTitleBar == true)
        {
            // Set the initial interactive regions.
            SetRegionsForCustomTitleBar();
        }
    }

    private void AppTitleBar_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ExtendsContentIntoTitleBar == true)
        {
            // Update interactive regions if the size of the window changes.
            SetRegionsForCustomTitleBar();
        }
    }

    private void SetRegionsForCustomTitleBar()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            double scaleAdjustment = AppTitleBar.XamlRoot.RasterizationScale;

            var rightInset = AppWindow.TitleBar.RightInset;
            var leftInset = AppWindow.TitleBar.LeftInset;

            RightPaddingColumn.Width = new GridLength(rightInset);
            LeftPaddingColumn.Width = new GridLength(leftInset);

            int offsetX = (int)GetElementOffsetFromWindowLeftInPixels(DraggablePanel);
            int offsetY = 0; // At the top

            var width = (int)(AppWindow.ClientSize.Width - (rightInset + leftInset + AppMenuColumn.ActualWidth));
            var height = AppWindow.TitleBar.Height;

            RectInt32 draggableRect = new(offsetX, offsetY, width, height);
            AppWindow.TitleBar.SetDragRectangles([draggableRect]);
        });
    }

    // Returns the X offset in physical pixels from the element to the left edge of the window
    private static double GetElementOffsetFromWindowLeftInPixels(FrameworkElement element)
    {
        // Transform (0,0) of the element to the window's coordinate space
        GeneralTransform transform = element.TransformToVisual(null); // 'null' means root visual (window)
        Point offset = transform.TransformPoint(new Point(0, 0));
        return offset.X;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.CurrentAppTheme))
        {
            UpdateRequestedAppTheme();
            UpdateTitleBarColors();
        }

        if (e.PropertyName == nameof(MainWindowViewModel.CurrentNotification))
        {
            RestartNotificationTimer();
        }
    }

    private void RestartNotificationTimer()
    {
        _notificationTimer.Stop();

        if (ViewModel.HasNotification)
        {
            _notificationTimer.Start();
        }
    }

    private void NotificationTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        _notificationTimer.Stop();
        ViewModel.DismissNotificationCommand.Execute(null);
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.CodeActivated)
        {
            Activated -= OnActivated;
            RestoreAppWindowSizeAndPosition();
        }
    }

    private void OnActivationStateChanged(object sender, WindowActivatedEventArgs args)
    {
        _mainWindowActivationService.SetActive(
            args.WindowActivationState != WindowActivationState.Deactivated);
    }

    private void BeginCloseConfirmation()
    {
        if (_closeConfirmationInProgress)
        {
            return;
        }

        _closeConfirmationInProgress = true;
        if (!DispatcherQueue.TryEnqueue(() => _ = ConfirmCloseAsync()))
        {
            _closeConfirmationInProgress = false;
        }
    }

    private void OnAppWindowClosing(Microsoft.UI.Windowing.AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_closeConfirmed)
        {
            return;
        }

        args.Cancel = true;
        BeginCloseConfirmation();
    }

    private async Task ConfirmCloseAsync()
    {
        try
        {
            await _navigationCoordinator.ExecuteTransitionAsync(
                _ =>
                {
                    _closeConfirmed = true;
                    Close();
                    return Task.FromResult(true);
                });
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to confirm window close.");
        }
        finally
        {
            _closeConfirmationInProgress = false;
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _mainWindowActivationService.SetActive(false);
        Activated -= OnActivated;
        Activated -= OnActivationStateChanged;
        AppWindow.Closing -= OnAppWindowClosing;
        Closed -= OnClosed;
        RootGrid.Loaded -= RootGrid_Loaded;
        AppTitleBar.Loaded -= AppTitleBar_Loaded;
        AppTitleBar.SizeChanged -= AppTitleBar_SizeChanged;
        _notificationTimer.Stop();
        _notificationTimer.Tick -= NotificationTimer_Tick;

        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;

        ViewModel.Dispose();

        _shutdownHandler.Shutdown();
    }

    public void SuspendMediaPlayback()
    {
        switch (NavigationFrame.Content)
        {
            case AudioEditPage audioEditPage:
                audioEditPage.SuspendMediaPlayback();
                break;

            case VideoEditPage videoEditPage:
                videoEditPage.SuspendMediaPlayback();
                break;
        }
    }

    public void ResumeMediaPlayback()
    {
        switch (NavigationFrame.Content)
        {
            case AudioEditPage audioEditPage:
                audioEditPage.ResumeMediaPlayback();
                break;

            case VideoEditPage videoEditPage:
                videoEditPage.ResumeMediaPlayback();
                break;
        }
    }

    public async Task<NavigationResult> HandleNavigationRequestAsync(
        INavigationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (ViewModel.IsCurrentNavigationRequest(request))
        {
            DispatcherQueue.TryEnqueue(ResumeMediaPlayback);
            return NavigationResult.NoChange;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool enqueued = DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                Type pageType = PageLocator.GetPageType(request.Route);
                int backTargetIndex = request.IsBackNavigation
                    ? FindBackTargetIndex(pageType, request.Parameter)
                    : -1;
                if (backTargetIndex >= 0)
                {
                    while (NavigationFrame.BackStack.Count - 1 > backTargetIndex)
                    {
                        NavigationFrame.BackStack.RemoveAt(NavigationFrame.BackStack.Count - 1);
                    }

                    NavigationFrame.GoBack();
                    NavigationFrame.ForwardStack.Clear();
                    GC.Collect();
                }
                else
                {
                    NavigationFrame.Navigate(pageType, request.Parameter);
                }

                if (request.ClearHistory)
                {
                    NavigationFrame.ForwardStack.Clear();
                    NavigationFrame.BackStack.Clear();
                    GC.Collect();
                }

                ResumeMediaPlayback();
                completion.SetResult();
            }
            catch (OperationCanceledException ex)
            {
                completion.SetCanceled(ex.CancellationToken);
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });

        if (!enqueued)
        {
            return NavigationResult.Rejected;
        }

        await completion.Task;
        ViewModel.CommitNavigationRequest(request);
        return NavigationResult.Accepted;
    }

    private int FindBackTargetIndex(Type pageType, object? parameter)
    {
        for (int i = NavigationFrame.BackStack.Count - 1; i >= 0; i--)
        {
            var entry = NavigationFrame.BackStack[i];
            if (entry.SourcePageType == pageType && Equals(entry.Parameter, parameter))
            {
                return i;
            }
        }

        return -1;
    }

    private void RestoreAppWindowSizeAndPosition()
    {
        AppWindow.Move(new PointInt32(1, 1));
        AppWindow.Resize(DefaultWindowSize);
        this.CenterOnScreen();
    }

    private void UpdateRequestedAppTheme()
    {
        object theme = ViewModel.CurrentAppTheme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            AppTheme.SystemDefault => ConvertToElementTheme(ViewModel.DefaultAppTheme),
            _ => DependencyProperty.UnsetValue
        };

        RootGrid.SetValue(FrameworkElement.RequestedThemeProperty, theme);
    }

    private static ElementTheme ConvertToElementTheme(AppTheme appTheme)
    {
        return appTheme switch
        {
            AppTheme.SystemDefault => ElementTheme.Default,
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }

    private void UpdateTitleBarColors()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var titleBar = AppWindow.TitleBar;

            if (titleBar is null)
                return;

            AppTheme appTheme = ViewModel.CurrentAppTheme == AppTheme.SystemDefault
                ? ViewModel.DefaultAppTheme
                : ViewModel.CurrentAppTheme;

            Color foregroundColor = appTheme switch
            {
                AppTheme.Dark => Colors.White,
                _ => Colors.Black,
            };

            titleBar.ForegroundColor = foregroundColor;
            titleBar.ButtonForegroundColor = foregroundColor;

            titleBar.InactiveForegroundColor = foregroundColor;
            titleBar.ButtonInactiveForegroundColor = foregroundColor;
        });
    }
}
