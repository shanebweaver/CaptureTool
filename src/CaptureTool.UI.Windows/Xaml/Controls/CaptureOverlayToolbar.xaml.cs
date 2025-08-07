using CaptureTool.Capture;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;

namespace CaptureTool.UI.Windows.Xaml.Controls;

public sealed partial class CaptureOverlayToolbar : UserControlBase
{
    public static readonly DependencyProperty SupportedCaptureTypesProperty = DependencyProperty.Register(
        nameof(SupportedCaptureTypes),
        typeof(ICollection<CaptureType>),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty SelectedCaptureTypeIndexProperty = DependencyProperty.Register(
        nameof(SelectedCaptureTypeIndex),
        typeof(int),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty SupportedCaptureModesProperty = DependencyProperty.Register(
        nameof(SupportedCaptureModes),
        typeof(ICollection<CaptureMode>),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty SelectedCaptureModeIndexProperty = DependencyProperty.Register(
        nameof(SelectedCaptureModeIndex),
        typeof(int),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty IsVideoCaptureEnabledProperty = DependencyProperty.Register(
        nameof(IsVideoCaptureEnabled),
        typeof(bool),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public ICollection<CaptureType> SupportedCaptureTypes
    {
        get => Get<ICollection<CaptureType>>(SupportedCaptureTypesProperty);
        set => Set(SupportedCaptureTypesProperty, value);
    }

    public int SelectedCaptureTypeIndex
    {
        get => Get<int>(SelectedCaptureTypeIndexProperty);
        set => Set(SelectedCaptureTypeIndexProperty, value);
    }

    public ICollection<CaptureMode> SupportedCaptureModes
    {
        get => Get<ICollection<CaptureMode>>(SupportedCaptureModesProperty);
        set => Set(SupportedCaptureModesProperty, value);
    }

    public int SelectedCaptureModeIndex
    {
        get => Get<int>(SelectedCaptureModeIndexProperty);
        set => Set(SelectedCaptureModeIndexProperty, value);
    }

    public bool IsVideoCaptureEnabled
    {
        get => Get<bool>(IsVideoCaptureEnabledProperty);
        set => Set(IsVideoCaptureEnabledProperty, value);
    }

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
}
