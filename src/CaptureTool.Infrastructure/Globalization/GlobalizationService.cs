using CaptureTool.Application.Abstractions.Globalization;
using System.Globalization;

namespace CaptureTool.Infrastructure.Globalization;

public sealed partial class GlobalizationService : IGlobalizationService
{
    public bool IsRightToLeft => CultureInfo.CurrentCulture.TextInfo.IsRightToLeft;
}
