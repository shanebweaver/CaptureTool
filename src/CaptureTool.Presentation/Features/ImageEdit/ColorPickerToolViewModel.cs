using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Presentation.Notifications;
using CaptureTool.Presentation.Shared.Commands;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;
using System.Drawing;
using System.Globalization;

namespace CaptureTool.Presentation.Features.ImageEdit;

public sealed partial class ColorPickerToolViewModel : ViewModelBase
{
    private const string ColorCopiedMessageResourceKey = "ImageEdit_ColorCopiedNotification";

    private readonly IClipboardService _clipboardService;
    private readonly ILocalizationService _localizationService;
    private readonly IAppNotificationService _notificationService;

    public IRelayCommand<int> UpdateSelectedColorTypeIndexCommand { get; }
    public IRelayCommand<Color> UpdatePickedColorCommand { get; }
    public IAsyncRelayCommand CopyPickedColorCommand { get; }

    public ColorPickerColorType SelectedColorType
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                SelectedColorTypeIndex = (int)value;
                UpdatePickedColorValue();
            }
        }
    }

    public int SelectedColorTypeIndex
    {
        get;
        private set => Set(ref field, value);
    }

    public Color PickedColor
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                UpdatePickedColorValue();
            }
        }
    }

    public string PickedColorValue
    {
        get;
        private set => Set(ref field, value);
    } = string.Empty;

    public ColorPickerToolViewModel(
        IClipboardService clipboardService,
        ILocalizationService localizationService,
        IAppNotificationService notificationService,
        ITelemetryService? telemetryService = null)
    {
        _clipboardService = clipboardService;
        _localizationService = localizationService;
        _notificationService = notificationService;

        UpdateSelectedColorTypeIndexCommand = new RelayCommand<int>(UpdateSelectedColorTypeIndex);
        UpdatePickedColorCommand = new RelayCommand<Color>(UpdatePickedColor);
        CopyPickedColorCommand = TelemetryCommandFactory.Async("image_edit.copy_picked_color", CopyPickedColorAsync, telemetryService, "image_edit");

        SelectedColorType = ColorPickerColorType.Hex;
        SelectedColorTypeIndex = (int)SelectedColorType;
        PickedColor = Color.Empty;
    }

    public void Reset()
    {
        SelectedColorType = ColorPickerColorType.Hex;
        PickedColor = Color.Empty;
        PickedColorValue = string.Empty;
    }

    public void UpdateSelectedColorTypeIndex(int value)
    {
        if (!Enum.IsDefined(typeof(ColorPickerColorType), value))
        {
            return;
        }

        SelectedColorType = (ColorPickerColorType)value;
    }

    public void UpdatePickedColor(Color color)
    {
        PickedColor = color;
    }

    public async Task CopyPickedColorAsync()
    {
        if (string.IsNullOrWhiteSpace(PickedColorValue))
        {
            return;
        }

        await _clipboardService.CopyTextAsync(PickedColorValue);
        _notificationService.ShowInfo(GetLocalizedString(ColorCopiedMessageResourceKey));
    }

    private void UpdatePickedColorValue()
    {
        PickedColorValue = PickedColor.IsEmpty
            ? string.Empty
            : FormatColor(PickedColor, SelectedColorType);
    }

    private string GetLocalizedString(string resourceKey)
    {
        string value = _localizationService.GetString(resourceKey);
        return string.IsNullOrWhiteSpace(value)
            ? resourceKey
            : value;
    }

    public static string FormatColor(Color color, ColorPickerColorType colorType)
    {
        return colorType switch
        {
            ColorPickerColorType.Rgb => string.Create(
                CultureInfo.InvariantCulture,
                $"rgb({color.R}, {color.G}, {color.B})"),
            ColorPickerColorType.Hsl => FormatHsl(color),
            _ => string.Create(
                CultureInfo.InvariantCulture,
                $"#{color.R:X2}{color.G:X2}{color.B:X2}")
        };
    }

    private static string FormatHsl(Color color)
    {
        double r = color.R / 255d;
        double g = color.G / 255d;
        double b = color.B / 255d;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double lightness = (max + min) / 2d;
        double hue = 0;
        double saturation = 0;

        if (Math.Abs(max - min) > double.Epsilon)
        {
            double delta = max - min;
            saturation = lightness > 0.5d
                ? delta / (2d - max - min)
                : delta / (max + min);

            if (Math.Abs(max - r) < double.Epsilon)
            {
                hue = (g - b) / delta + (g < b ? 6d : 0d);
            }
            else if (Math.Abs(max - g) < double.Epsilon)
            {
                hue = (b - r) / delta + 2d;
            }
            else
            {
                hue = (r - g) / delta + 4d;
            }

            hue *= 60d;
        }

        int roundedHue = (int)Math.Round(hue, MidpointRounding.AwayFromZero);
        int roundedSaturation = (int)Math.Round(saturation * 100d, MidpointRounding.AwayFromZero);
        int roundedLightness = (int)Math.Round(lightness * 100d, MidpointRounding.AwayFromZero);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"hsl({roundedHue}, {roundedSaturation}%, {roundedLightness}%)");
    }
}
