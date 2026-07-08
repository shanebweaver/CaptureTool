using CaptureTool.Presentation.Features.Audio;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Specialized;
using System.ComponentModel;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Pages;

public sealed partial class AudioCapturePage : AudioCapturePageBase
{
    private const double WaveformDefaultSurfaceHeight = 140;
    private const double WaveformDefaultMaxBarHeight = 132;
    private readonly HashSet<AudioWaveformBarViewModel> _subscribedWaveformBars = [];
    private bool _isUpdatingWaveformBars;
    private bool _isWaveformSubscribed;

    public AudioCapturePage()
    {
        InitializeComponent();
        Loaded += AudioCapturePage_Loaded;
        Unloaded += AudioCapturePage_Unloaded;
    }

    private Symbol GetPauseResumeSymbol(bool isPaused)
    {
        return isPaused ? Symbol.Play : Symbol.Pause;
    }

    private void AudioCapturePage_Loaded(object sender, RoutedEventArgs e)
    {
        SubscribeWaveform();
        UpdateWaveformSizing();
    }

    private void AudioCapturePage_Unloaded(object sender, RoutedEventArgs e)
    {
        UnsubscribeWaveform();
    }

    private void SubscribeWaveform()
    {
        if (_isWaveformSubscribed)
        {
            return;
        }

        _isWaveformSubscribed = true;
        ViewModel.WaveformBars.CollectionChanged += WaveformBars_CollectionChanged;
        foreach (var bar in ViewModel.WaveformBars)
        {
            SubscribeWaveformBar(bar);
        }
    }

    private void UnsubscribeWaveform()
    {
        if (!_isWaveformSubscribed)
        {
            return;
        }

        _isWaveformSubscribed = false;
        ViewModel.WaveformBars.CollectionChanged -= WaveformBars_CollectionChanged;
        foreach (var bar in _subscribedWaveformBars)
        {
            bar.PropertyChanged -= WaveformBar_PropertyChanged;
        }

        _subscribedWaveformBars.Clear();
    }

    private void WaveformBars_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (AudioWaveformBarViewModel bar in e.OldItems)
            {
                UnsubscribeWaveformBar(bar);
            }
        }

        if (e.NewItems is not null)
        {
            foreach (AudioWaveformBarViewModel bar in e.NewItems)
            {
                SubscribeWaveformBar(bar);
            }
        }

        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            UnsubscribeWaveformBars();
            foreach (var bar in ViewModel.WaveformBars)
            {
                SubscribeWaveformBar(bar);
            }
        }

        UpdateWaveformSizing();
    }

    private void SubscribeWaveformBar(AudioWaveformBarViewModel bar)
    {
        if (_subscribedWaveformBars.Add(bar))
        {
            bar.PropertyChanged += WaveformBar_PropertyChanged;
        }
    }

    private void UnsubscribeWaveformBar(AudioWaveformBarViewModel bar)
    {
        if (_subscribedWaveformBars.Remove(bar))
        {
            bar.PropertyChanged -= WaveformBar_PropertyChanged;
        }
    }

    private void UnsubscribeWaveformBars()
    {
        foreach (var bar in _subscribedWaveformBars)
        {
            bar.PropertyChanged -= WaveformBar_PropertyChanged;
        }

        _subscribedWaveformBars.Clear();
    }

    private void WaveformBar_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isUpdatingWaveformBars || sender is not AudioWaveformBarViewModel bar)
        {
            return;
        }

        if (e.PropertyName is nameof(AudioWaveformBarViewModel.Level) or nameof(AudioWaveformBarViewModel.Height))
        {
            UpdateWaveformBarHeight(bar);
        }
    }

    private void WaveformSurface_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateWaveformSizing();
    }

    private void UpdateWaveformSizing()
    {
        _isUpdatingWaveformBars = true;
        try
        {
            foreach (var bar in ViewModel.WaveformBars)
            {
                SetWaveformBarHeight(bar);
            }
        }
        finally
        {
            _isUpdatingWaveformBars = false;
        }
    }

    private void UpdateWaveformBarHeight(AudioWaveformBarViewModel bar)
    {
        _isUpdatingWaveformBars = true;
        try
        {
            SetWaveformBarHeight(bar);
        }
        finally
        {
            _isUpdatingWaveformBars = false;
        }
    }

    private void SetWaveformBarHeight(AudioWaveformBarViewModel bar)
    {
        bar.Height = bar.Level * GetWaveformMaxBarHeight();
    }

    private double GetWaveformMaxBarHeight()
    {
        double surfaceHeight = WaveformSurface.ActualHeight;
        if (surfaceHeight <= 0)
        {
            return WaveformDefaultMaxBarHeight;
        }

        return surfaceHeight * (WaveformDefaultMaxBarHeight / WaveformDefaultSurfaceHeight);
    }
}
