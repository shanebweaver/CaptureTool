using System;
using CaptureTool.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CaptureTool.UI.Xaml.Pages;

[WinRT.GeneratedBindableCustomProperty]
public sealed partial class CanvasItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ImageTemplate { get; set; }
    public DataTemplate? RectangleTemplate { get; set; }
    public DataTemplate? TextTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        DataTemplate? template = item switch
        {
            ImageCanvasItemViewModel => ImageTemplate,
            RectangleCanvasItemViewModel => RectangleTemplate,
            TextCanvasItemViewModel => TextTemplate,
            _ => base.SelectTemplateCore(item, container)
        };

        return GetOrThrowIfNull(template);
    }

    private static T GetOrThrowIfNull<T>(T? value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return value;
    }
}