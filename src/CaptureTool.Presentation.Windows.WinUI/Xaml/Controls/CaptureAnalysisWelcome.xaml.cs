using CaptureTool.Application.Abstractions.Analysis;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class CaptureAnalysisWelcome : UserControl
{
    public CaptureAnalysisWelcome() : this(new(false, false, false)) { }
    public CaptureAnalysisWelcome(AnalysisFeatureBenefits benefits)
    {
        InitializeComponent();
        TextBenefit.Visibility = benefits.Text ? Visibility.Visible : Visibility.Collapsed;
        SummaryBenefit.Visibility = benefits.Summary ? Visibility.Visible : Visibility.Collapsed;
        NamesBenefit.Visibility = benefits.Names ? Visibility.Visible : Visibility.Collapsed;
    }
}
