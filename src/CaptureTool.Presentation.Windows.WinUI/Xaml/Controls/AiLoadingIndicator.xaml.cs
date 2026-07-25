using Microsoft.UI.Xaml;
using Windows.UI.ViewManagement;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class AiLoadingIndicator : UserControlBase
{
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive),
        typeof(bool),
        typeof(AiLoadingIndicator),
        new PropertyMetadata(false, OnIsActiveChanged));

    private readonly UISettings _uiSettings = new();
    private bool _isMotionRunning;

    public AiLoadingIndicator()
    {
        InitializeComponent();
        Loaded += AiLoadingIndicator_Loaded;
        Unloaded += AiLoadingIndicator_Unloaded;
    }

    public bool IsActive
    {
        get => Get<bool>(IsActiveProperty);
        set => Set(IsActiveProperty, value);
    }

    private static void OnIsActiveChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is AiLoadingIndicator indicator)
        {
            indicator.UpdateAnimationState();
        }
    }

    private void AiLoadingIndicator_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateAnimationState();
    }

    private void AiLoadingIndicator_Unloaded(object sender, RoutedEventArgs e)
    {
        StopMotion();
    }

    private void UpdateAnimationState()
    {
        bool shouldRun = IsLoaded && IsActive && _uiSettings.AnimationsEnabled;
        if (shouldRun == _isMotionRunning)
        {
            return;
        }

        if (shouldRun)
        {
            MagicMotionStoryboard.Begin();
            _isMotionRunning = true;
        }
        else
        {
            StopMotion();
        }
    }

    private void StopMotion()
    {
        if (!_isMotionRunning)
        {
            return;
        }

        MagicMotionStoryboard.Stop();
        _isMotionRunning = false;
    }
}
