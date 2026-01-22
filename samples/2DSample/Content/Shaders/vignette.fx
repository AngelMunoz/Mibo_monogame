#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_5_0
	#define PS_SHADERMODEL ps_5_0
#endif

Texture2D SpriteTexture;
sampler2D SpriteTextureSampler = sampler_state
{
    Texture = <SpriteTexture>;
};

float Radius = 1.0;
float Softness = 0.5;

struct VertexData
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

float4 PixelShaderFunction(VertexData input) : COLOR0
{
    float4 color = tex2D(SpriteTextureSampler, input.TexCoord);

    // Normalize coordinates to -1 to 1
    float2 dist = (input.TexCoord - 0.5f) * 2.0f;
    float len = length(dist);
    float vignette = smoothstep(Radius, Radius - Softness, len);

    return color * vignette * input.Color;
}

technique SpriteBatch
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL PixelShaderFunction();
    }
};
