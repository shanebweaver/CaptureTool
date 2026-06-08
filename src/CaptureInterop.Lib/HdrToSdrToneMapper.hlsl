Texture2D SourceTexture : register(t0);
SamplerState SourceSampler : register(s0);

cbuffer ToneMapperConstants : register(b0)
{
    float SdrWhiteNits;
    float ExposureScale;
    float ShoulderStrength;
    float _Padding0;
};

struct PixelInput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

float3 LinearToSrgb(float3 value)
{
    value = saturate(value);
    float3 low = value * 12.92f;
    float3 high = 1.055f * pow(value, 1.0f / 2.4f) - 0.055f;
    return lerp(high, low, value <= 0.0031308f);
}

float3 ApplyShoulder(float3 value)
{
    float shoulder = max(ShoulderStrength, 0.0001f);
    return value / (value + shoulder);
}

float4 PSMain(PixelInput input) : SV_TARGET
{
    float4 source = SourceTexture.Sample(SourceSampler, input.TexCoord);

    // The production input color/transfer assumption must be finalized by the capture format spike.
    // For now, treat incoming RGB as a linear-ish working value and keep the mapping conservative.
    float whiteScale = max(SdrWhiteNits, 1.0f) / 100.0f;
    float3 mapped = source.rgb * ExposureScale / whiteScale;
    mapped = ApplyShoulder(mapped);

    return float4(LinearToSrgb(mapped), 1.0f);
}
