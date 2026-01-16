#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_1
	#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

matrix World;
matrix View;
matrix Projection;

float4 AlbedoColor = float4(1, 1, 1, 1);
texture AlbedoMap;
sampler AlbedoSampler = sampler_state
{
	Texture = <AlbedoMap>;
	MagFilter = Linear;
	MinFilter = Linear;
	MipFilter = Linear;
	AddressU = Wrap;
	AddressV = Wrap;
};

struct VertexShaderInput
{
	float4 Position : POSITION0;
	float2 TexCoord : TEXCOORD0;
	float3 Normal : NORMAL0;
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float2 TexCoord : TEXCOORD0;
	float3 Normal : TEXCOORD1;
};

VertexShaderOutput MainVS(in VertexShaderInput input)
{
	VertexShaderOutput output;

	float4 worldPosition = mul(input.Position, World);
	float4 viewPosition = mul(worldPosition, View);
	output.Position = mul(viewPosition, Projection);
	output.TexCoord = input.TexCoord;
	
    // Transform normal to world space
    output.Normal = mul(input.Normal, (float3x3)World);

	return output;
}

struct PixelShaderOutput
{
	float4 Albedo : COLOR0;
	float4 Normal : COLOR1;
};

PixelShaderOutput MainPS(VertexShaderOutput input)
{
	PixelShaderOutput output;

	float4 texColor = tex2D(AlbedoSampler, input.TexCoord);
	output.Albedo = texColor * AlbedoColor;
    
    // Store normal in [0, 1] range
    float3 normal = normalize(input.Normal);
    output.Normal = float4(normal * 0.5 + 0.5, 1.0);

	return output;
}

technique GBuffer
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};
