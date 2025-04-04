using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CaptureTool.UI.Xaml.UserControls;

public sealed partial class NewCaptureButton : UserControl
{
    private static readonly DependencyProperty SymbolProperty = DependencyProperty.Register(
        nameof(Symbol),
        typeof(Symbol),
        typeof(NewCaptureButton),
        new(null));

    private static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(NewCaptureButton),
        new(null));

    private static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
        nameof(Command),
        typeof(ICommand),
        typeof(NewCaptureButton),
        new(null));

    public Symbol Symbol
    {
        get => (Symbol)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public NewCaptureButton()
    {
        InitializeComponent();
    }
}
