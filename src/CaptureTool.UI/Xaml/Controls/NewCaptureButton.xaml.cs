using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CaptureTool.UI.Xaml.Controls;

public sealed partial class NewCaptureButton : UserControlBase
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
        get => Get<Symbol>(SymbolProperty);
        set => Set(SymbolProperty, value);
    }

    public string Text
    {
        get => Get<string>(TextProperty);
        set => Set(TextProperty, value);
    }

    public ICommand Command
    {
        get => Get<ICommand>(CommandProperty);
        set => Set(CommandProperty, value);
    }

    public NewCaptureButton()
    {
        InitializeComponent();
    }
}
