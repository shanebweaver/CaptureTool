using Microsoft.UI.Xaml;
using System.Windows.Input;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class ConfirmationCard : UserControlBase
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(ConfirmationCard),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message),
        typeof(string),
        typeof(ConfirmationCard),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ConfirmButtonTextProperty = DependencyProperty.Register(
        nameof(ConfirmButtonText),
        typeof(string),
        typeof(ConfirmationCard),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty CancelButtonTextProperty = DependencyProperty.Register(
        nameof(CancelButtonText),
        typeof(string),
        typeof(ConfirmationCard),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty PrimaryButtonTextProperty = DependencyProperty.Register(
        nameof(PrimaryButtonText),
        typeof(string),
        typeof(ConfirmationCard),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ConfirmCommandProperty = DependencyProperty.Register(
        nameof(ConfirmCommand),
        typeof(ICommand),
        typeof(ConfirmationCard),
        new PropertyMetadata(null));

    public static readonly DependencyProperty CancelCommandProperty = DependencyProperty.Register(
        nameof(CancelCommand),
        typeof(ICommand),
        typeof(ConfirmationCard),
        new PropertyMetadata(null));

    public static readonly DependencyProperty PrimaryCommandProperty = DependencyProperty.Register(
        nameof(PrimaryCommand),
        typeof(ICommand),
        typeof(ConfirmationCard),
        new PropertyMetadata(null));

    public ConfirmationCard()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => Get<string>(TitleProperty);
        set => Set(TitleProperty, value);
    }

    public string Message
    {
        get => Get<string>(MessageProperty);
        set => Set(MessageProperty, value);
    }

    public string ConfirmButtonText
    {
        get => Get<string>(ConfirmButtonTextProperty);
        set => Set(ConfirmButtonTextProperty, value);
    }

    public string CancelButtonText
    {
        get => Get<string>(CancelButtonTextProperty);
        set => Set(CancelButtonTextProperty, value);
    }

    public string PrimaryButtonText
    {
        get => Get<string>(PrimaryButtonTextProperty);
        set
        {
            Set(PrimaryButtonTextProperty, value);
            RaisePropertyChanged(nameof(PrimaryButtonVisibility));
        }
    }

    public Visibility PrimaryButtonVisibility
    {
        get => string.IsNullOrWhiteSpace(PrimaryButtonText)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    public ICommand? ConfirmCommand
    {
        get => Get<ICommand?>(ConfirmCommandProperty);
        set => Set(ConfirmCommandProperty, value);
    }

    public ICommand? CancelCommand
    {
        get => Get<ICommand?>(CancelCommandProperty);
        set => Set(CancelCommandProperty, value);
    }

    public ICommand? PrimaryCommand
    {
        get => Get<ICommand?>(PrimaryCommandProperty);
        set => Set(PrimaryCommandProperty, value);
    }
}
