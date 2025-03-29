using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.ViewModels;
using CaptureTool.ViewModels.Loading;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace CaptureTool.UI.Xaml.Pages;

public abstract class PageBase<VM> : Page where VM : ViewModelBase
{
    private CancellationTokenSource? _loadCancellationTokenSource;

    public abstract VM ViewModel { get; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        //DispatcherQueue.TryEnqueue(() =>
        //{
            try
            {
                _loadCancellationTokenSource ??= new();
                if (ViewModel.LoadState != LoadState.Loaded)
                {
                    ViewModel.LoadAsync(e.Parameter, _loadCancellationTokenSource.Token);
                }
            }
            catch (TaskCanceledException)
            {
                //Debug.Fail("Task was canceled.");
            }
            catch (Exception)
            {
                //Debug.Fail("Page navigation failed.");
            }
        //});

        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _loadCancellationTokenSource?.Cancel();
        _loadCancellationTokenSource?.Dispose();
        _loadCancellationTokenSource = null;

        if (e.NavigationMode == NavigationMode.Back)
        {
            ViewModel.Unload();
        }

        base.OnNavigatedFrom(e);
    }
}
