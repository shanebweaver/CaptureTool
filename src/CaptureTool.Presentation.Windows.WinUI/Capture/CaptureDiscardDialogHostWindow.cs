using CaptureTool.Presentation.Windows.WinUI.Utils;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Numerics;
using Rectangle = System.Drawing.Rectangle;

namespace CaptureTool.Presentation.Windows.WinUI.Capture;

internal sealed class CaptureDiscardDialogHostWindow : Window
{
    private const double HostWidth = 520;
    private const double HostHeight = 248;

    private readonly Rectangle? _windowBounds;

    private readonly Grid _root = new()
    {
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
    };

    private readonly TaskCompletionSource _loadedCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _resultCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public CaptureDiscardDialogHostWindow(Rectangle? windowBounds = null)
    {
        _windowBounds = windowBounds;
        Content = _root;
        _root.Loaded += Root_Loaded;
        Closed += Window_Closed;
    }

    public async Task<bool> ShowConfirmationAsync(
        string title,
        string content,
        string discardButtonText,
        string cancelButtonText)
    {
        _root.Children.Add(CreateConfirmationCard(title, content, discardButtonText, cancelButtonText));
        ConfigureWindow();
        Activate();
        this.SetForegroundWindow();

        await _loadedCompletion.Task;

        try
        {
            return await _resultCompletion.Task;
        }
        finally
        {
            CloseHost();
        }
    }

    private void ConfigureWindow()
    {
        this.MakeBorderlessToolWindow();
        this.ExcludeFromScreenCapture();

        if (_windowBounds is { } windowBounds)
        {
            this.MoveAndResize(windowBounds);
        }
        else
        {
            this.CenterOnScreen(HostWidth, HostHeight);
        }
    }

    private UIElement CreateConfirmationCard(
        string title,
        string content,
        string discardButtonText,
        string cancelButtonText)
    {
        var card = new Border
        {
            Margin = new Thickness(10),
            Padding = new Thickness(22),
            Background = GetBrush("AcrylicBackgroundFillColorDefaultBrush", Colors.White),
            BorderBrush = GetBrush("CardStrokeColorDefaultBrush", Colors.Transparent),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Shadow = new ThemeShadow(),
            Translation = new Vector3(0, 0, 32)
        };

        var layout = new Grid
        {
            RowSpacing = 16,
            ColumnSpacing = 16
        };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition());
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var iconHost = new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(18),
            Background = GetBrush("SystemFillColorCriticalBackgroundBrush", ColorHelper.FromArgb(0x33, 0xFF, 0x33, 0x40)),
            Child = new SymbolIcon(Symbol.Important)
            {
                Foreground = GetBrush("SystemFillColorCriticalBrush", ColorHelper.FromArgb(0xFF, 0xC4, 0x2B, 0x1C))
            }
        };
        Grid.SetRowSpan(iconHost, 2);
        layout.Children.Add(iconHost);

        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = GetBrush("TextFillColorPrimaryBrush", Colors.Black),
            TextWrapping = TextWrapping.WrapWholeWords
        };
        Grid.SetColumn(titleText, 1);
        layout.Children.Add(titleText);

        var contentText = new TextBlock
        {
            Text = content,
            FontSize = 14,
            Foreground = GetBrush("TextFillColorSecondaryBrush", Colors.DimGray),
            TextWrapping = TextWrapping.WrapWholeWords,
            LineHeight = 20
        };
        Grid.SetRow(contentText, 1);
        Grid.SetColumn(contentText, 1);
        layout.Children.Add(contentText);

        var buttons = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        var cancelButton = new Button
        {
            Content = cancelButtonText,
            MinWidth = 118,
            CornerRadius = new CornerRadius(4)
        };
        cancelButton.Click += (_, _) => _resultCompletion.TrySetResult(false);

        var discardButton = new Button
        {
            Content = discardButtonText,
            MinWidth = 132,
            Background = GetBrush("SystemFillColorCriticalBrush", ColorHelper.FromArgb(0xFF, 0xC4, 0x2B, 0x1C)),
            BorderBrush = GetBrush("SystemFillColorCriticalBrush", ColorHelper.FromArgb(0xFF, 0xC4, 0x2B, 0x1C)),
            Foreground = GetBrush("TextOnAccentFillColorPrimaryBrush", Colors.White),
            CornerRadius = new CornerRadius(4)
        };
        discardButton.Click += (_, _) => _resultCompletion.TrySetResult(true);

        buttons.Children.Add(cancelButton);
        buttons.Children.Add(discardButton);

        Grid.SetRow(buttons, 2);
        Grid.SetColumnSpan(buttons, 2);
        layout.Children.Add(buttons);

        card.Child = layout;
        return card;
    }

    private void Root_Loaded(object sender, RoutedEventArgs e)
    {
        _root.Loaded -= Root_Loaded;
        _loadedCompletion.TrySetResult();
    }

    private void Window_Closed(object sender, WindowEventArgs args)
    {
        Closed -= Window_Closed;
        _resultCompletion.TrySetResult(false);
    }

    private void CloseHost()
    {
        try
        {
            Close();
        }
        catch
        {
        }
    }

    private static Microsoft.UI.Xaml.Media.Brush GetBrush(string key, global::Windows.UI.Color fallback)
    {
        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(key, out object value) &&
            value is Microsoft.UI.Xaml.Media.Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(fallback);
    }
}
