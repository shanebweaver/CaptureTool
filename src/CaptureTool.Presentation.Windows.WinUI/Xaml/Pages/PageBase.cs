using CaptureTool.Common;
using CaptureTool.Common.Loading;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Pages;

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
            if (ViewModel.IsReadyToLoad)
            {
                switch (ViewModel)
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
        }
        catch (OperationCanceledException ex)
        {
            AppServiceLocator.Logging.LogException(ex, "Page load canceled.");
        }
        catch (Exception ex)
        {
            AppServiceLocator.Logging.LogException(ex, "Failed to load page.");
            AppServiceLocator.Navigation.GoToError(ex);
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
