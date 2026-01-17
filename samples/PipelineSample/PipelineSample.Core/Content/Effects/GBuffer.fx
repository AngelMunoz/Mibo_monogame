 #if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_5_0
	#define PS_SHADERMODEL ps_5_0
#endif

matrix World;
matrix View;
matrix Projection;

float4 AlbedoColor = float4(1, 1, 1, 1);
float HasAlbedoMap = 0.0;
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
    float3 WorldPos : TEXCOORD3;
};

VertexShaderOutput MainVS(in VertexShaderInput input)
{
	VertexShaderOutput output;

	float4 worldPosition = mul(input.Position, World);
	float4 viewPosition = mul(worldPosition, View);
	output.Position = mul(viewPosition, Projection);
	output.TexCoord = input.TexCoord;
	
    // Transform normal to world space
    output.Normal = normalize(mul(input.Normal, (float3x3)World));
    
    // Pass world position
    output.WorldPos = worldPosition.xyz;

	return output;
}

struct PixelShaderOutput
{
	float4 Albedo : COLOR0;
	float4 Normal : COLOR1;
    float4 WorldPos : COLOR2;
};

PixelShaderOutput MainPS(VertexShaderOutput input)
{
	PixelShaderOutput output;

	float4 texColor = (HasAlbedoMap > 0.5) 
        ? tex2D(AlbedoSampler, input.TexCoord)
        : float4(1, 1, 1, 1);
        
	output.Albedo = texColor * AlbedoColor;
    
    // Store raw normal
    output.Normal = float4(normalize(input.Normal), 1.0);
    
    // Store world position
    output.WorldPos = float4(input.WorldPos, 1.0);

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
