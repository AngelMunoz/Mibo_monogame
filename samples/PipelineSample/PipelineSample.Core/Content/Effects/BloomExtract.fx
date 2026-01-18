#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_5_0
	#define PS_SHADERMODEL ps_5_0
#endif

texture SceneTexture;
sampler SceneSampler = sampler_state
{
    Texture = <SceneTexture>;
    AddressU = Clamp;
    AddressV = Clamp;
    MagFilter = Linear;
    MinFilter = Linear;
    MipFilter = Linear;
};

float Threshold = 0.8;
float Intensity = 1.0;
float2 TexelSize;

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

VertexShaderOutput MainVS(in VertexShaderInput input)
{
    VertexShaderOutput output;
    output.Position = input.Position;
    output.TexCoord = input.TexCoord;
    return output;
}

// Helper: Extract brightness
float3 ThresholdColor(float3 color)
{
    float brightness = dot(color, float3(0.2126, 0.7152, 0.0722));
    if (brightness > Threshold)
        return color;
    return float3(0, 0, 0);
}

float4 MainPS(VertexShaderOutput input) : COLOR0
{
    float3 colorSum = float3(0,0,0);
    
    // 4x4 Sample Grid with Pre-Thresholding
    // We sample a wider area to simulate a larger glow
    for(float x = -1.5; x <= 1.5; x += 1.0)
    {
        for(float y = -1.5; y <= 1.5; y += 1.0)
        {
            float2 offset = float2(x, y) * TexelSize * 3.0; // Spread factor 3.0
            float3 c = tex2D(SceneSampler, input.TexCoord + offset).rgb;
            colorSum += ThresholdColor(c);
        }
    }
    
    float3 avgBloom = (colorSum / 16.0) * Intensity;
    return float4(avgBloom, 1.0);
}

technique BloomExtract
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};
