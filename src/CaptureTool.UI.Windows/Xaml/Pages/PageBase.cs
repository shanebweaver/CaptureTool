using CaptureTool.Common.Loading;
using CaptureTool.Core.Navigation;
using CaptureTool.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Threading;

namespace CaptureTool.UI.Windows.Xaml.Pages;

public abstract class PageBase<VM> : Page where VM : ViewModelBase
{
    private CancellationTokenSource? _loadCts;

    public VM ViewModel { get; } = App.Current.ServiceProvider.GetService<VM>();

    public PageBase()
    {
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        _loadCts ??= new();

        try
        {
            switch(ViewModel)
            {
                case ILoadable loadable:
                    loadable.Load();
                    break;

                case IAsyncLoadable asyncLoadable:
                    await asyncLoadable.LoadAsync(_loadCts.Token);
                    break;

                case ILoadableWithParam loadableWithParam:
                    loadableWithParam.Load(e.Parameter);
                    break;

                case IAsyncLoadableWithParam asyncLoadableWithParam:
                    await asyncLoadableWithParam.LoadAsync(e.Parameter, _loadCts.Token);
                    break;
            }
        }
        catch (OperationCanceledException ex)
        {
            ServiceLocator.Logging.LogException(ex, "Page load canceled.");
        }
        catch (Exception ex)
        {
            ServiceLocator.Logging.LogException(ex, "Failed to load page.");
            ServiceLocator.Navigation.GoToError(ex);
        }

        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        if (_loadCts != null)
        {
            _loadCts.Cancel();
            _loadCts.Dispose();
            _loadCts = null;
        }

        ViewModel.Dispose();

        base.OnNavigatedFrom(e);
    }
}
