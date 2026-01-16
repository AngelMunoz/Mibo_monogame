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
float HasAlbedoMap = 0.0; // 0 = no texture, 1 = has texture
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

// Lighting
float3 AmbientColor;
float3 LightDirections[3];
float3 LightColors[3];

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
    output.Normal = mul(input.Normal, (float3x3)World);

	return output;
}

float4 MainPS(VertexShaderOutput input) : COLOR0
{
	// Use texture if available, otherwise just use AlbedoColor
	float4 albedo = (HasAlbedoMap > 0.5)
		? tex2D(AlbedoSampler, input.TexCoord) * AlbedoColor
		: AlbedoColor;

    float3 normal = normalize(input.Normal);

    float3 diffuse = AmbientColor;

    for(int i = 0; i < 3; i++)
    {
        float ndotl = max(dot(normal, -normalize(LightDirections[i])), 0.0);
        diffuse += ndotl * LightColors[i];
    }

	return float4(albedo.rgb * diffuse, albedo.a);
}

technique Forward
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};
