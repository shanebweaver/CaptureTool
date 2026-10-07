using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using CaptureTool.Application.Abstractions.Analysis;
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
