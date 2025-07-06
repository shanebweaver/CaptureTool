// ChromaKeyEffect.hlsl
Texture2D input : register(t0);
sampler inputSampler : register(s0);

float3 keyColor;       // The chroma key color to remove (e.g., green)
float threshold;       // How similar the color has to be to be removed

float4 main(float2 uv : TEXCOORD) : SV_Target {
    float4 color = input.Sample(inputSampler, uv);

    // Calculate distance from key color
    float dist = distance(color.rgb, keyColor);
    
    // If it's close to key color, set alpha to 0 (transparent)
    if (dist < threshold) {
        color.a = 0.0;
    }

    return color;
}
