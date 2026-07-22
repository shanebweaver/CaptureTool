using Microsoft.UI.Xaml;
using System.Windows.Input;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class ImageDescriptionToolbar : UserControlBase
{
    public static readonly DependencyProperty BriefCommandProperty = RegisterCommand(nameof(BriefCommand));
    public static readonly DependencyProperty DetailedCommandProperty = RegisterCommand(nameof(DetailedCommand));
    public static readonly DependencyProperty DiagramCommandProperty = RegisterCommand(nameof(DiagramCommand));
    public static readonly DependencyProperty AccessibleCommandProperty = RegisterCommand(nameof(AccessibleCommand));
    public static readonly DependencyProperty IsBriefSelectedProperty = RegisterSelection(nameof(IsBriefSelected));
    public static readonly DependencyProperty IsDetailedSelectedProperty = RegisterSelection(nameof(IsDetailedSelected));
    public static readonly DependencyProperty IsDiagramSelectedProperty = RegisterSelection(nameof(IsDiagramSelected));
    public static readonly DependencyProperty IsAccessibleSelectedProperty = RegisterSelection(nameof(IsAccessibleSelected));

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

    public bool IsBriefSelected
    {
        get => Get<bool>(IsBriefSelectedProperty);
        set => Set(IsBriefSelectedProperty, value);
    }

    public bool IsDetailedSelected
    {
        get => Get<bool>(IsDetailedSelectedProperty);
        set => Set(IsDetailedSelectedProperty, value);
    }

    public bool IsDiagramSelected
    {
        get => Get<bool>(IsDiagramSelectedProperty);
        set => Set(IsDiagramSelectedProperty, value);
    }

    public bool IsAccessibleSelected
    {
        get => Get<bool>(IsAccessibleSelectedProperty);
        set => Set(IsAccessibleSelectedProperty, value);
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

    private static DependencyProperty RegisterSelection(string propertyName)
    {
        return DependencyProperty.Register(
            propertyName,
            typeof(bool),
            typeof(ImageDescriptionToolbar),
            new PropertyMetadata(false, OnSelectionChanged));
    }

    private static void OnSelectionChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is ImageDescriptionToolbar toolbar)
        {
            toolbar.RefreshSelectionVisuals();
        }
    }

    private void ImageDescriptionButton_Click(object sender, RoutedEventArgs args)
    {
        // ToggleButton changes IsChecked locally before executing its command. Reapply the
        // displayed-result selection so a pending request never appears selected.
        RefreshSelectionVisuals();
    }

    private void RefreshSelectionVisuals()
    {
        RaisePropertyChanged(nameof(IsBriefSelected));
        RaisePropertyChanged(nameof(IsDetailedSelected));
        RaisePropertyChanged(nameof(IsDiagramSelected));
        RaisePropertyChanged(nameof(IsAccessibleSelected));
    }
}
