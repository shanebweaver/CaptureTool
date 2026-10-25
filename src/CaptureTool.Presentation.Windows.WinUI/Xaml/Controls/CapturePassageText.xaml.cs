using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class CapturePassageText : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(CapturePassageText), new(string.Empty, Changed));
    public static readonly DependencyProperty QueryProperty = DependencyProperty.Register(nameof(Query), typeof(string), typeof(CapturePassageText), new(string.Empty, Changed));
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string Query { get => (string)GetValue(QueryProperty); set => SetValue(QueryProperty, value); }
    public CapturePassageText() { InitializeComponent(); }
    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((CapturePassageText)sender).Update();
    private void Update()
    {
        if (ContentText == null) return;
        ContentText.Text = Text;
        var ranges = ContentText.TextHighlighters[0].Ranges;
        ranges.Clear();
        if (string.IsNullOrEmpty(Query)) return;
        for (int start = 0; start <= Text.Length - Query.Length;)
        {
            int index = Text.IndexOf(Query, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0) break;
            ranges.Add(new TextRange { StartIndex = index, Length = Query.Length });
            start = index + Query.Length;
        }
    }
}
