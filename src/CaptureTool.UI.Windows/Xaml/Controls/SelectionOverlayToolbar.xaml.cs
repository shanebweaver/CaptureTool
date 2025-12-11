using CaptureTool.Core.Implementations.FeatureManagement;
using CaptureTool.ViewModels;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Windows.Input;

namespace CaptureTool.UI.Windows.Xaml.Controls;

public sealed partial class SelectionOverlayToolbar : UserControlBase
{
    public static readonly DependencyProperty SupportedCaptureTypesProperty = DependencyProperty.Register(
        nameof(SupportedCaptureTypes),
        typeof(IEnumerable<CaptureTypeViewModel>),
        typeof(SelectionOverlayToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty SelectedCaptureTypeIndexProperty = DependencyProperty.Register(
        nameof(SelectedCaptureTypeIndex),
        typeof(int),
        typeof(SelectionOverlayToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty SupportedCaptureModesProperty = DependencyProperty.Register(
        nameof(SupportedCaptureModes),
        typeof(IEnumerable<CaptureModeViewModel>),
        typeof(SelectionOverlayToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty SelectedCaptureModeIndexProperty = DependencyProperty.Register(
        nameof(SelectedCaptureModeIndex),
        typeof(int),
        typeof(SelectionOverlayToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty CloseCommandProperty = DependencyProperty.Register(
        nameof(CloseCommand),
        typeof(ICommand),
        typeof(SelectionOverlayToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public event EventHandler<int>? CaptureTypeSelectionChanged;
    public event EventHandler<int>? CaptureModeSelectionChanged;

    public IEnumerable<CaptureTypeViewModel> SupportedCaptureTypes
    {
        get => Get<IEnumerable<CaptureTypeViewModel>>(SupportedCaptureTypesProperty);
        set => Set(SupportedCaptureTypesProperty, value);
    }

    public int SelectedCaptureTypeIndex
    {
        get => Get<int>(SelectedCaptureTypeIndexProperty);
        set
        {
            Set(SelectedCaptureTypeIndexProperty, value);
            CaptureTypeSelectionChanged?.Invoke(this, value);
        }
    }

    public IEnumerable<CaptureModeViewModel> SupportedCaptureModes
    {
        get => Get<IEnumerable<CaptureModeViewModel>>(SupportedCaptureModesProperty);
        set => Set(SupportedCaptureModesProperty, value);
    }

    public int SelectedCaptureModeIndex
    {
        get => Get<int>(SelectedCaptureModeIndexProperty);
        set
        {
            Set(SelectedCaptureModeIndexProperty, value);
            CaptureModeSelectionChanged?.Invoke(this, value);
        }
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

    private void CaptureModeSegmentedControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is Segmented segmentedControl)
        {
            SelectedCaptureModeIndex = segmentedControl.SelectedIndex;
        }
    }

    private void CaptureTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox)
        {
            SelectedCaptureTypeIndex = comboBox.SelectedIndex;
        }
    }
}
