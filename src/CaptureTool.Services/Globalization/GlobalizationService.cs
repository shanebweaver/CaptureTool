using System.Globalization;

namespace CaptureTool.Services.Globalization;

public sealed partial class GlobalizationService : IGlobalizationService
{
    public bool IsRightToLeft => CultureInfo.CurrentCulture.TextInfo.IsRightToLeft;
}
