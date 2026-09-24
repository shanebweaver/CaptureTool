using CaptureTool.Domain.Analysis;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Documents;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class HighlightedTextBlock : UserControlBase
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(HighlightedTextBlock), new PropertyMetadata(string.Empty, Changed));
    public static readonly DependencyProperty HighlightsProperty = DependencyProperty.Register(
        nameof(Highlights), typeof(object), typeof(HighlightedTextBlock), new PropertyMetadata(null, Changed));
    public static readonly DependencyProperty IsTextSelectionEnabledProperty = DependencyProperty.Register(
        nameof(IsTextSelectionEnabled), typeof(bool), typeof(HighlightedTextBlock), new PropertyMetadata(false));

    public HighlightedTextBlock() { InitializeComponent(); }
    public string Text { get => Get<string>(TextProperty); set => Set(TextProperty, value); }
    public object? Highlights { get => Get<object?>(HighlightsProperty); set => Set(HighlightsProperty, value); }
    public bool IsTextSelectionEnabled { get => Get<bool>(IsTextSelectionEnabledProperty); set => Set(IsTextSelectionEnabledProperty, value); }

    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var control = (HighlightedTextBlock)sender;
        if (control.ContentText == null) { return; }
        control.ContentText.Text = control.Text;
        TextHighlighter highlighter = control.ContentText.TextHighlighters[0];
        highlighter.Ranges.Clear();
        foreach (CaptureTextRange range in control.Highlights as IReadOnlyList<CaptureTextRange> ?? [])
        {
            if (range.Start >= 0 && range.Length > 0 && (long)range.Start + range.Length <= control.Text.Length)
            {
                highlighter.Ranges.Add(new TextRange { StartIndex = range.Start, Length = range.Length });
            }
        }
    }
}
