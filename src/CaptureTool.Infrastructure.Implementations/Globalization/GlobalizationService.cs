using CaptureTool.Infrastructure.Interfaces.Globalization;
using System.Globalization;

namespace CaptureTool.Infrastructure.Implementations.Globalization;

public sealed partial class GlobalizationService : IGlobalizationService
{
    public bool IsRightToLeft => CultureInfo.CurrentCulture.TextInfo.IsRightToLeft;
}
