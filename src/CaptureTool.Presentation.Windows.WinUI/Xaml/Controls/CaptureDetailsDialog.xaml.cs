using CaptureTool.Presentation.Features.CaptureDetails;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

/// <summary>One reusable, read-only details surface for all capture media kinds.</summary>
public sealed partial class CaptureDetailsDialog : ContentDialog, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public CaptureDetailsViewModel ViewModel { get; }
    private XamlRoot? _root;
    public double ContentWidth => Math.Max(160, (XamlRoot?.Size.Width ?? 760) - 96);

    public CaptureDetailsDialog(CaptureDetailsViewModel viewModel, string path)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Opened += async (_, _) =>
        {
            _root = XamlRoot;
            _root.Changed += RootChanged;
            RootChanged(null, null!);
            await ViewModel.OpenAsync(path);
        };
        Closed += (_, _) =>
        {
            if (_root != null) _root.Changed -= RootChanged;
            _root = null;
            ViewModel.Dispose();
        };
    }

    private void RootChanged(XamlRoot? sender, XamlRootChangedEventArgs args) =>
        PropertyChanged?.Invoke(this, new(nameof(ContentWidth)));
    private async void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string text }) await ViewModel.CopyCommand.ExecuteAsync(text);
    }
}
