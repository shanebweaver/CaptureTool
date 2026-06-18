using CaptureTool.Application.Abstractions.Features.ImageEdit.ChromaKey;
using CaptureTool.Application.Abstractions.Features.Store;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.Edit;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;
using System.Drawing;

namespace CaptureTool.Presentation.Features.ImageEdit;

public sealed partial class ChromaKeyToolViewModel : ViewModelBase
{
    private readonly IStoreService _storeService;
    private readonly IChromaKeyService _chromaKeyService;
    private readonly IChromaKeyFeatureAvailability _featureAvailability;
    private ChromaKeySettings? _pendingInteractionSettings;

    public event EventHandler? SettingsChanged;
    public event EventHandler<(ChromaKeySettings OldSettings, ChromaKeySettings NewSettings)>? InteractionCommitted;

    public IRelayCommand<Color> UpdateChromaKeyColorCommand { get; }
    public IRelayCommand<int> UpdateDesaturationCommand { get; }
    public IRelayCommand<int> UpdateToleranceCommand { get; }
    public IRelayCommand<int> UpdateSelectedColorOptionIndexCommand { get; }

    public bool IsFeatureEnabled => _featureAvailability.IsChromaKeyEnabled;

    public IReadOnlyList<ChromaKeyColorOption> ChromaKeyColorOptions
    {
        get;
        private set => Set(ref field, value);
    } = [];

    public int SelectedChromaKeyColorOption
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsChromaKeyAddOnOwned
    {
        get;
        private set => Set(ref field, value);
    }

    public int ChromaKeyTolerance
    {
        get;
        private set => Set(ref field, value);
    }

    public int ChromaKeyDesaturation
    {
        get;
        private set => Set(ref field, value);
    }

    public Color ChromaKeyColor
    {
        get;
        private set => Set(ref field, value);
    }

    public ChromaKeyToolViewModel(
        IStoreService storeService,
        IChromaKeyService chromaKeyService,
        IChromaKeyFeatureAvailability featureAvailability)
    {
        _storeService = storeService;
        _chromaKeyService = chromaKeyService;
        _featureAvailability = featureAvailability;

        UpdateChromaKeyColorCommand = new RelayCommand<Color>(UpdateChromaKeyColor, (c) => IsFeatureEnabled);
        UpdateDesaturationCommand = new RelayCommand<int>(UpdateDesaturation);
        UpdateToleranceCommand = new RelayCommand<int>(UpdateTolerance);
        UpdateSelectedColorOptionIndexCommand = new RelayCommand<int>(UpdateSelectedColorOptionIndex);

        Reset();
    }

    public async Task LoadAsync(ImageFile imageFile, CancellationToken cancellationToken)
    {
        ChromaKeyColorOptions = [];

        if (!IsFeatureEnabled)
        {
            return;
        }

        IsChromaKeyAddOnOwned = await _storeService.IsAddonPurchasedAsync(
            CaptureToolStoreProducts.AddOns.ChromaKeyBackgroundRemoval,
            cancellationToken);

        if (!IsChromaKeyAddOnOwned)
        {
            return;
        }

        var colorOptions = new List<ChromaKeyColorOption>
        {
            // Empty option disables the effect.
            ChromaKeyColorOption.Empty
        };

        var topColors = await _chromaKeyService.GetTopColorsAsync(imageFile, 15, 16);
        colorOptions.AddRange(topColors.Select(color => new ChromaKeyColorOption(color)));
        ChromaKeyColorOptions = colorOptions;
    }

    public void Reset()
    {
        SelectedChromaKeyColorOption = 0;
        ChromaKeyTolerance = 30;
        ChromaKeyDesaturation = 0;
        ChromaKeyColor = Color.Empty;
        ChromaKeyColorOptions = [];
        IsChromaKeyAddOnOwned = false;
        _pendingInteractionSettings = null;
    }

    public void UpdateDesaturation(int value)
    {
        ChromaKeyDesaturation = Math.Clamp(value, 0, 100);
        OnSettingsChanged();
    }

    public void UpdateTolerance(int value)
    {
        ChromaKeyTolerance = Math.Clamp(value, 0, 100);
        OnSettingsChanged();
    }

    public void UpdateSelectedColorOptionIndex(int value)
    {
        SelectedChromaKeyColorOption = value;
        UpdateChromaKeyColor(value);
    }

    public void ApplySettings(ChromaKeySettings settings)
    {
        SelectedChromaKeyColorOption = settings.SelectedColorOptionIndex;
        ChromaKeyColor = settings.Color;
        ChromaKeyTolerance = settings.Tolerance;
        ChromaKeyDesaturation = settings.Desaturation;
    }

    public ChromaKeySettings CaptureSettings()
    {
        return new(
            SelectedChromaKeyColorOption,
            ChromaKeyColor,
            ChromaKeyTolerance,
            ChromaKeyDesaturation);
    }

    private void UpdateChromaKeyColor(int colorIndex)
    {
        if (colorIndex < 0 || colorIndex >= ChromaKeyColorOptions.Count)
        {
            return;
        }

        UpdateChromaKeyColor(ChromaKeyColorOptions[colorIndex].Color);
    }

    public void UpdateChromaKeyColor(Color color)
    {
        if (!IsFeatureEnabled)
        {
            return;
        }

        ChromaKeyColor = color;
        OnSettingsChanged();
    }

    public void BeginInteraction()
    {
        _pendingInteractionSettings ??= CaptureSettings();
    }

    public void CompleteInteraction()
    {
        if (_pendingInteractionSettings is not { } oldSettings)
        {
            return;
        }

        _pendingInteractionSettings = null;
        ChromaKeySettings newSettings = CaptureSettings();

        if (!oldSettings.Equals(newSettings))
        {
            InteractionCommitted?.Invoke(this, (oldSettings, newSettings));
        }
    }

    private void OnSettingsChanged()
    {
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }
}
