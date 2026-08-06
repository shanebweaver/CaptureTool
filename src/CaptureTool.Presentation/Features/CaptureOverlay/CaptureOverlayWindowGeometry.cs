using System.Drawing;

namespace CaptureTool.Presentation.Features.CaptureOverlay;

internal readonly record struct CaptureOverlayWindowLayout(
    Rectangle ToolbarBounds,
    Rectangle ShadowBounds)
{
    public Point ShadowCasterOffset => new(
        ToolbarBounds.X - ShadowBounds.X,
        ToolbarBounds.Y - ShadowBounds.Y);
}

internal static class CaptureOverlayWindowGeometry
{
    public static bool TryCalculate(
        Rectangle monitorBounds,
        double logicalContentWidth,
        double logicalContentHeight,
        double rasterizationScale,
        int topInsetPixels,
        double shadowPaddingDips,
        out CaptureOverlayWindowLayout layout)
    {
        layout = default;

        if (monitorBounds.Width <= 0 ||
            monitorBounds.Height <= 0 ||
            !IsPositiveFinite(logicalContentWidth) ||
            !IsPositiveFinite(logicalContentHeight) ||
            !IsPositiveFinite(rasterizationScale) ||
            topInsetPixels <= 0 ||
            !IsPositiveFinite(shadowPaddingDips))
        {
            return false;
        }

        if (!TryRoundUpToPhysicalPixels(logicalContentWidth, rasterizationScale, out int contentWidth) ||
            !TryRoundUpToPhysicalPixels(logicalContentHeight, rasterizationScale, out int contentHeight) ||
            !TryRoundUpToPhysicalPixels(shadowPaddingDips, rasterizationScale, out int shadowPadding))
        {
            return false;
        }

        long toolbarX = monitorBounds.X +
            (long)Math.Floor((monitorBounds.Width - (double)contentWidth) / 2d);
        long toolbarY = (long)monitorBounds.Y + topInsetPixels;
        long toolbarRight = toolbarX + contentWidth;
        long toolbarBottom = toolbarY + contentHeight;

        long shadowX = toolbarX - shadowPadding;
        long shadowY = toolbarY - shadowPadding;
        long shadowRight = toolbarRight + shadowPadding;
        long shadowBottom = toolbarBottom + shadowPadding;

        if (!IsValidCoordinate(toolbarX) ||
            !IsValidCoordinate(toolbarY) ||
            !IsValidCoordinate(toolbarRight) ||
            !IsValidCoordinate(toolbarBottom) ||
            !IsValidCoordinate(shadowX) ||
            !IsValidCoordinate(shadowY) ||
            !IsValidCoordinate(shadowRight) ||
            !IsValidCoordinate(shadowBottom))
        {
            return false;
        }

        long shadowWidth = shadowRight - shadowX;
        long shadowHeight = shadowBottom - shadowY;
        if (shadowWidth > int.MaxValue || shadowHeight > int.MaxValue)
        {
            return false;
        }

        Rectangle toolbarBounds = new(
            (int)toolbarX,
            (int)toolbarY,
            contentWidth,
            contentHeight);
        Rectangle shadowBounds = new(
            (int)shadowX,
            (int)shadowY,
            (int)shadowWidth,
            (int)shadowHeight);

        layout = new CaptureOverlayWindowLayout(toolbarBounds, shadowBounds);
        return true;
    }

    private static bool TryRoundUpToPhysicalPixels(
        double logicalValue,
        double rasterizationScale,
        out int physicalValue)
    {
        double scaledValue = logicalValue * rasterizationScale;
        if (!IsPositiveFinite(scaledValue) || scaledValue > int.MaxValue)
        {
            physicalValue = 0;
            return false;
        }

        physicalValue = (int)Math.Ceiling(scaledValue);
        return physicalValue > 0;
    }

    private static bool IsPositiveFinite(double value)
        => value > 0 && double.IsFinite(value);

    private static bool IsValidCoordinate(long value)
        => value >= int.MinValue && value <= int.MaxValue;
}
