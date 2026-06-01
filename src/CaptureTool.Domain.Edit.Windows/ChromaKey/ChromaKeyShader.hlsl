#define D2D_INPUT_COUNT 1
#define D2D_INPUT0_SIMPLE

#include "d2d1effecthelpers.hlsli"

float4 keyColor;
float tolerance;
float softness;

float3 RgbToHsv(float3 color)
{
    float maximum = max(color.r, max(color.g, color.b));
    float minimum = min(color.r, min(color.g, color.b));
    float delta = maximum - minimum;

    float hue = 0;
    if (delta > 0.00001f)
    {
        if (maximum == color.r)
        {
            hue = (color.g - color.b) / delta;
            hue = hue - 6.0f * floor(hue / 6.0f);
        }
        else if (maximum == color.g)
        {
            hue = ((color.b - color.r) / delta) + 2.0f;
        }
        else
        {
            hue = ((color.r - color.g) / delta) + 4.0f;
        }

        hue /= 6.0f;
    }

    float saturation = maximum <= 0.00001f ? 0 : delta / maximum;
    return float3(hue, saturation, maximum);
}

float CalculateAlpha(float distance)
{
    if (softness <= 0.00001f)
    {
        return distance <= tolerance ? 0 : 1;
    }

    return smoothstep(tolerance, tolerance + softness, distance);
}

D2D_PS_ENTRY(main)
{
    float4 source = D2DGetInput(0);
    float sourceAlpha = source.a;
    float3 sourceRgb = sourceAlpha > 0.00001f ? saturate(source.rgb / sourceAlpha) : source.rgb;

    float3 sourceHsv = RgbToHsv(sourceRgb);
    float3 keyHsv = RgbToHsv(keyColor.rgb);

    float hueDistance = abs(sourceHsv.x - keyHsv.x);
    hueDistance = min(hueDistance, 1.0f - hueDistance);

    float saturationDistance = abs(sourceHsv.y - keyHsv.y);
    float valueDistance = abs(sourceHsv.z - keyHsv.z);
    float distance = (hueDistance * 2.0f) + saturationDistance + valueDistance;

    float outputAlpha = sourceAlpha * CalculateAlpha(distance);
    return float4(sourceRgb * outputAlpha, outputAlpha);
}
