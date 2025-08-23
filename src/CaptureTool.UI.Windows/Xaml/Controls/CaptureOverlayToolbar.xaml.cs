using CaptureTool.Capture;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CaptureTool.UI.Windows.Xaml.Controls;

public sealed partial class CaptureOverlayToolbar : UserControlBase
{
    public static readonly DependencyProperty SupportedCaptureTypesProperty = DependencyProperty.Register(
        nameof(SupportedCaptureTypes),
        typeof(IEnumerable<CaptureType>),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty SelectedCaptureTypeIndexProperty = DependencyProperty.Register(
        nameof(SelectedCaptureTypeIndex),
        typeof(int),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty SupportedCaptureModesProperty = DependencyProperty.Register(
        nameof(SupportedCaptureModes),
        typeof(IEnumerable<CaptureMode>),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty SelectedCaptureModeIndexProperty = DependencyProperty.Register(
        nameof(SelectedCaptureModeIndex),
        typeof(int),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty IsDesktopAudioEnabledProperty = DependencyProperty.Register(
        nameof(IsDesktopAudioEnabled),
        typeof(bool),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty ActiveCaptureModeProperty = DependencyProperty.Register(
        nameof(ActiveCaptureMode),
        typeof(CaptureMode),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public IEnumerable<CaptureType> SupportedCaptureTypes
    {
        get => Get<IEnumerable<CaptureType>>(SupportedCaptureTypesProperty);
        set => Set(SupportedCaptureTypesProperty, value);
    }

    public int SelectedCaptureTypeIndex
    {
        get => Get<int>(SelectedCaptureTypeIndexProperty);
        set => Set(SelectedCaptureTypeIndexProperty, value);
    }

    public IEnumerable<CaptureMode> SupportedCaptureModes
    {
        get => Get<IEnumerable<CaptureMode>>(SupportedCaptureModesProperty);
        set => Set(SupportedCaptureModesProperty, value);
    }

    public int SelectedCaptureModeIndex
    {
        get => Get<int>(SelectedCaptureModeIndexProperty);
        set => Set(SelectedCaptureModeIndexProperty, value);
    }

    public bool IsDesktopAudioEnabled
    {
        get => Get<bool>(IsDesktopAudioEnabledProperty);
        set => Set(IsDesktopAudioEnabledProperty, value);
    }

    public CaptureMode ActiveCaptureMode
    {
        get => Get<CaptureMode>(ActiveCaptureModeProperty);
        set
        {
            Set(ActiveCaptureModeProperty, value);
            RaisePropertyChanged(nameof(IsActiveCaptureModeImage));
            RaisePropertyChanged(nameof(IsActiveCaptureModeVideo));
        }
    }

    public bool IsActiveCaptureModeImage => ActiveCaptureMode == CaptureMode.Image;
    public bool IsActiveCaptureModeVideo => ActiveCaptureMode == CaptureMode.Video;

    public event EventHandler? CloseRequested;

    public CaptureOverlayToolbar()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private bool IsCaptureTypeSupported(CaptureType captureType)
    {
        if (SupportedCaptureTypes != null)
        {
            return SupportedCaptureTypes.Contains(captureType);
        }

        return false;
    }

    private bool IsCaptureModeSupported(CaptureMode captureMode)
    {
        if (SupportedCaptureModes != null)
        {
            return SupportedCaptureModes.Contains(captureMode);    
        }

        return false;
    }

    private void LocalAudioToggle_Click(object sender, RoutedEventArgs e)
    {
        IsDesktopAudioEnabled = !IsDesktopAudioEnabled;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        ActiveCaptureMode = CaptureMode.Image;
    }
}
