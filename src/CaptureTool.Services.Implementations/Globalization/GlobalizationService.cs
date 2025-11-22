using CaptureTool.Services.Interfaces.Globalization;
using System.Globalization;

namespace CaptureTool.Services.Implementations.Globalization;

public sealed partial class GlobalizationService : IGlobalizationService
{
    public bool IsRightToLeft => CultureInfo.CurrentCulture.TextInfo.IsRightToLeft;
}
