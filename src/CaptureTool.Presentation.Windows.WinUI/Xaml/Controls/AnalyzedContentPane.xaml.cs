using CaptureTool.Presentation.Features.AnalyzedContent;
using Microsoft.UI.Xaml;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class AnalyzedContentPane : UserControlBase
{
    private readonly AnalyzedContentViewModel _fallbackViewModel = new();
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(AnalyzedContentViewModel),
        typeof(AnalyzedContentPane),
        new PropertyMetadata(null, OnViewModelChanged));

    public AnalyzedContentPane()
    {
        InitializeComponent();
    }

    public AnalyzedContentViewModel ViewModel
    {
        get => Get<AnalyzedContentViewModel?>(ViewModelProperty) ?? _fallbackViewModel;
        set => Set(ViewModelProperty, value);
    }

    private static void OnViewModelChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is AnalyzedContentPane pane)
        {
            pane.DataContext = args.NewValue;
        }
    }
}
