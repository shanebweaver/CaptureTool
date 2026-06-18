using CaptureTool.Domain.Edit.Drawable;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;
using System.Drawing;
using System.Numerics;

namespace CaptureTool.Presentation.Features.ImageEdit;

public sealed partial class TextToolViewModel : ViewModelBase
{
    private const int MinimumTextFontSize = 1;
    private const int MaximumTextFontSize = 400;

    public Color TextFontColor
    {
        get;
        private set => Set(ref field, value);
    }

    public Color TextBackgroundColor
    {
        get;
        private set => Set(ref field, value);
    }

    public IReadOnlyList<Color> TextFontColorOptions { get; }

    public IReadOnlyList<Color> TextBackgroundColorOptions { get; }

    public int TextFontColorOpacity
    {
        get;
        private set => Set(ref field, value);
    }

    public int TextBackgroundColorOpacity
    {
        get;
        private set => Set(ref field, value);
    }

    public string TextFontFamily
    {
        get;
        private set => Set(ref field, value);
    }

    public int TextFontSize
    {
        get;
        private set => Set(ref field, value);
    }

    public IRelayCommand<Color> UpdateTextFontColorCommand { get; }
    public IRelayCommand<Color> UpdateTextBackgroundColorCommand { get; }
    public IRelayCommand<int> UpdateTextFontColorOpacityCommand { get; }
    public IRelayCommand<int> UpdateTextBackgroundColorOpacityCommand { get; }
    public IRelayCommand<string?> UpdateTextFontFamilyCommand { get; }
    public IRelayCommand<int> UpdateTextFontSizeCommand { get; }

    public TextToolViewModel()
    {
        UpdateTextFontColorCommand = new RelayCommand<Color>(UpdateTextFontColor);
        UpdateTextBackgroundColorCommand = new RelayCommand<Color>(UpdateTextBackgroundColor);
        UpdateTextFontColorOpacityCommand = new RelayCommand<int>(UpdateTextFontColorOpacity);
        UpdateTextBackgroundColorOpacityCommand = new RelayCommand<int>(UpdateTextBackgroundColorOpacity);
        UpdateTextFontFamilyCommand = new RelayCommand<string?>(UpdateTextFontFamily);
        UpdateTextFontSizeCommand = new RelayCommand<int>(UpdateTextFontSize);

        TextFontColorOptions = ImageEditColorPalette.Drawables;
        TextBackgroundColorOptions = ImageEditColorPalette.Drawables;
        TextFontColor = ImageEditColorPalette.Drawables[2]; // Black
        TextBackgroundColor = ImageEditColorPalette.Drawables[1]; // White
        TextFontColorOpacity = 100;
        TextBackgroundColorOpacity = 100;
        TextFontFamily = TextDrawable.DefaultFontFamily;
        TextFontSize = (int)TextDrawable.DefaultFontSize;
    }

    public void ApplyImageSizeDefaults(Size imageSize)
    {
        int smallestEdge = Math.Min(imageSize.Width, imageSize.Height);

        if (smallestEdge > 0)
        {
            TextFontSize = Math.Clamp(
                (int)Math.Round(smallestEdge / 40d),
                (int)TextDrawable.DefaultFontSize,
                MaximumTextFontSize);
        }
    }

    public TextStyle CreateStyle()
    {
        return new(TextFontColor, TextBackgroundColor, TextFontFamily, TextFontSize);
    }

    public TextDrawable? CreateDrawable(Vector2 startPoint, Vector2 endPoint)
    {
        return DrawableFactory.CreateTextBox(startPoint, endPoint, CreateStyle());
    }

    public void ApplyDrawable(TextDrawable text)
    {
        TextFontColor = text.Color;
        TextBackgroundColor = text.BackgroundColor;
        TextFontColorOpacity = ImageEditColorPalette.AlphaToOpacityPercentage(text.Color);
        TextBackgroundColorOpacity = ImageEditColorPalette.AlphaToOpacityPercentage(text.BackgroundColor);
        TextFontFamily = text.FontFamily;
        TextFontSize = Math.Clamp((int)Math.Round(text.FontSize), MinimumTextFontSize, MaximumTextFontSize);
    }

    public void UpdateTextFontColor(Color value)
    {
        TextFontColor = ImageEditColorPalette.ApplyOpacity(value, TextFontColorOpacity);
    }

    public void UpdateTextBackgroundColor(Color value)
    {
        TextBackgroundColor = ImageEditColorPalette.ApplyOpacity(value, TextBackgroundColorOpacity);
    }

    public void UpdateTextFontColorOpacity(int value)
    {
        TextFontColorOpacity = Math.Clamp(value, 0, 100);
        TextFontColor = ImageEditColorPalette.ApplyOpacity(TextFontColor, TextFontColorOpacity);
    }

    public void UpdateTextBackgroundColorOpacity(int value)
    {
        TextBackgroundColorOpacity = Math.Clamp(value, 0, 100);
        TextBackgroundColor = ImageEditColorPalette.ApplyOpacity(TextBackgroundColor, TextBackgroundColorOpacity);
    }

    public void UpdateTextFontFamily(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        TextFontFamily = value;
    }

    public void UpdateTextFontSize(int value)
    {
        TextFontSize = Math.Clamp(value, MinimumTextFontSize, MaximumTextFontSize);
    }
}
