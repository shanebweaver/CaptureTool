using CaptureTool.Application.Interfaces.FeatureManagement;
using CaptureTool.Domain.Capture.Interfaces;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Windows.Input;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class SelectionOverlayToolbar : UserControlBase
{
    public static readonly DependencyProperty SupportedCaptureTypesProperty = DependencyProperty.Register(
        nameof(SupportedCaptureTypes),
        typeof(object),
        typeof(SelectionOverlayToolbar),
        new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedCaptureTypeIndexProperty = DependencyProperty.Register(
        nameof(SelectedCaptureTypeIndex),
        typeof(int),
        typeof(SelectionOverlayToolbar),
        new PropertyMetadata(0));

    public static readonly DependencyProperty SupportedCaptureModesProperty = DependencyProperty.Register(
        nameof(SupportedCaptureModes),
        typeof(object),
        typeof(SelectionOverlayToolbar),
        new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedCaptureModeIndexProperty = DependencyProperty.Register(
        nameof(SelectedCaptureModeIndex),
        typeof(int),
        typeof(SelectionOverlayToolbar),
        new PropertyMetadata(0));

    public static readonly DependencyProperty CloseCommandProperty = DependencyProperty.Register(
        nameof(CloseCommand),
        typeof(ICommand),
        typeof(SelectionOverlayToolbar),
        new PropertyMetadata(null));

    public event EventHandler<int>? CaptureTypeSelectionChanged;
    public event EventHandler<int>? CaptureModeSelectionChanged;

    public object SupportedCaptureTypes
    {
        get => Get<object>(SupportedCaptureTypesProperty);
        set => Set(SupportedCaptureTypesProperty, value);
    }

    public int SelectedCaptureTypeIndex
    {
        get => Get<int>(SelectedCaptureTypeIndexProperty);
        set => Set(SelectedCaptureTypeIndexProperty, value);
    }

    public object SupportedCaptureModes
    {
        get => Get<object>(SupportedCaptureModesProperty);
        set => Set(SupportedCaptureModesProperty, value);
    }

    public int SelectedCaptureModeIndex
    {
        get => Get<int>(SelectedCaptureModeIndexProperty);
        set => Set(SelectedCaptureModeIndexProperty, value);
    }

    public ICommand CloseCommand
    {
        get => Get<ICommand>(CloseCommandProperty);
        set => Set(CloseCommandProperty, value);
    }

    public SelectionOverlayToolbar()
    {
        InitializeComponent();

        if (AppServiceLocator.FeatureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture))
        {
            CaptureModeSegmentedControl.Visibility = Visibility.Visible;
        }
    }

    // Function converters for {x:Bind} - maps enum to glyph resource key
    public static string GetCaptureModeGlyph(CaptureMode mode)
    {
        return mode switch
        {
            CaptureMode.Image => Microsoft.UI.Xaml.Application.Current.Resources["Glyph_CaptureMode_Image"] as string ?? "",
            CaptureMode.Video => Microsoft.UI.Xaml.Application.Current.Resources["Glyph_CaptureMode_Video"] as string ?? "",
            _ => ""
        };
    }

    public static string GetCaptureTypeGlyph(CaptureType type)
    {
        return type switch
        {
            CaptureType.Rectangle => Microsoft.UI.Xaml.Application.Current.Resources["Glyph_CaptureType_Rectangle"] as string ?? "",
            CaptureType.Window => Microsoft.UI.Xaml.Application.Current.Resources["Glyph_CaptureType_Window"] as string ?? "",
            CaptureType.FullScreen => Microsoft.UI.Xaml.Application.Current.Resources["Glyph_CaptureType_FullScreen"] as string ?? "",
            CaptureType.AllScreens => Microsoft.UI.Xaml.Application.Current.Resources["Glyph_CaptureType_AllScreens"] as string ?? "",
            _ => ""
        };
    }

    private void CaptureModeSegmentedControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is Segmented segmentedControl)
        {
            SelectedCaptureModeIndex = segmentedControl.SelectedIndex;
            CaptureModeSelectionChanged?.Invoke(this, segmentedControl.SelectedIndex);
        }
    }

    private void CaptureTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox)
        {
            SelectedCaptureTypeIndex = comboBox.SelectedIndex;
            CaptureTypeSelectionChanged?.Invoke(this, comboBox.SelectedIndex);
        }
    }
}
