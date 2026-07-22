using Microsoft.UI.Xaml;
using System.Windows.Input;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class TextExtractionToolbar : UserControlBase
{
    public static readonly DependencyProperty CopyAllTextCommandProperty = DependencyProperty.Register(
        nameof(CopyAllTextCommand),
        typeof(ICommand),
        typeof(TextExtractionToolbar),
        new PropertyMetadata(null, OnCopyAllTextCommandChanged));

    public TextExtractionToolbar()
    {
        InitializeComponent();
    }

    public ICommand? CopyAllTextCommand
    {
        get => Get<ICommand?>(CopyAllTextCommandProperty);
        set => Set(CopyAllTextCommandProperty, value);
    }

    private static void OnCopyAllTextCommandChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is TextExtractionToolbar toolbar)
        {
            toolbar.RaisePropertyChanged(nameof(CopyAllTextCommand));
        }
    }
}
