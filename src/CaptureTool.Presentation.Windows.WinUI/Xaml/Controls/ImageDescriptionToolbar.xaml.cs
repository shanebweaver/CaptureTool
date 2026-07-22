using Microsoft.UI.Xaml;
using System.Windows.Input;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class ImageDescriptionToolbar : UserControlBase
{
    public static readonly DependencyProperty BriefCommandProperty = RegisterCommand(nameof(BriefCommand));
    public static readonly DependencyProperty DetailedCommandProperty = RegisterCommand(nameof(DetailedCommand));
    public static readonly DependencyProperty DiagramCommandProperty = RegisterCommand(nameof(DiagramCommand));
    public static readonly DependencyProperty AccessibleCommandProperty = RegisterCommand(nameof(AccessibleCommand));

    public ImageDescriptionToolbar()
    {
        InitializeComponent();
    }

    public ICommand? BriefCommand
    {
        get => Get<ICommand?>(BriefCommandProperty);
        set => Set(BriefCommandProperty, value);
    }

    public ICommand? DetailedCommand
    {
        get => Get<ICommand?>(DetailedCommandProperty);
        set => Set(DetailedCommandProperty, value);
    }

    public ICommand? DiagramCommand
    {
        get => Get<ICommand?>(DiagramCommandProperty);
        set => Set(DiagramCommandProperty, value);
    }

    public ICommand? AccessibleCommand
    {
        get => Get<ICommand?>(AccessibleCommandProperty);
        set => Set(AccessibleCommandProperty, value);
    }

    private static DependencyProperty RegisterCommand(string propertyName)
    {
        return DependencyProperty.Register(
            propertyName,
            typeof(ICommand),
            typeof(ImageDescriptionToolbar),
            new PropertyMetadata(null, OnCommandChanged));
    }

    private static void OnCommandChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is ImageDescriptionToolbar toolbar)
        {
            toolbar.RaisePropertyChanged(nameof(BriefCommand));
            toolbar.RaisePropertyChanged(nameof(DetailedCommand));
            toolbar.RaisePropertyChanged(nameof(DiagramCommand));
            toolbar.RaisePropertyChanged(nameof(AccessibleCommand));
        }
    }
}
