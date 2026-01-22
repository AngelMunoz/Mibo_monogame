#if OPENGL
	#define SV_POSITION POSITION
	#define PS_SHADERMODEL ps_3_0
#else
	#define PS_SHADERMODEL ps_5_0
#endif

Texture2D SpriteTexture;
sampler2D SpriteTextureSampler = sampler_state
{
    Texture = <SpriteTexture>;
};

struct VertexData
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

float4 PixelShaderFunction(VertexData input) : COLOR0
{
    float4 color = tex2D(SpriteTextureSampler, input.TexCoord);
    float gray = dot(color.rgb, float3(0.299, 0.587, 0.114));
    return float4(gray, gray, gray, color.a) * input.Color;
}

technique SpriteBatch
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL PixelShaderFunction();
    }
};
