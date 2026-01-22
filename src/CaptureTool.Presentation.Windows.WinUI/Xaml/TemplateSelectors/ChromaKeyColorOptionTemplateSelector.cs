using CaptureTool.Domain.Edit.Interfaces.ChromaKey;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.TemplateSelectors;

public sealed partial class ChromaKeyColorOptionTemplateSelector : DataTemplateSelector
{
    public DataTemplate? EmptyTemplate { get; set; }
    public DataTemplate? ColorTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        if (item is ChromaKeyColorOption option)
        {
            if (option.IsEmpty)
            {
                if (EmptyTemplate != null)
                {
                    return EmptyTemplate;
                }
            }
            else if (ColorTemplate != null)
            {
                return ColorTemplate;
            }
        }

        return base.SelectTemplateCore(item, container);
    }
}