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
float Metallic = 0.0;
float Roughness = 1.0;

float4 EmissiveColor = float4(0, 0, 0, 1);
float EmissiveIntensity = 0.0;

float3 LightDirection = float3(0.5, -1.0, 0.3);
float3 LightColor = float3(1, 1, 1);
float LightIntensity = 1.0;
float3 AmbientColor = float3(0.1, 0.1, 0.15);

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float2 TexCoord : TEXCOORD0;
	float3 Normal : TEXCOORD1;
	float3 WorldPos : TEXCOORD4;
};

VertexShaderOutput MainVS(in float4 Position : POSITION0, in float2 TexCoord : TEXCOORD0, in float3 Normal : NORMAL0)
{
	VertexShaderOutput output;
	float4 worldPosition = mul(Position, World);
	output.Position = mul(mul(worldPosition, View), Projection);
	output.TexCoord = TexCoord;
	output.Normal = normalize(mul(Normal, (float3x3)World));
	output.WorldPos = worldPosition.xyz;
	return output;
}

float4 MainPS(VertexShaderOutput input) : COLOR0
{
	float3 N = normalize(input.Normal);
	float3 L = normalize(-LightDirection);
	float3 V = normalize(-input.WorldPos);
	float3 H = normalize(L + V);

	float NdotL = max(dot(N, L), 0.0);
	float NdotH = max(dot(N, H), 0.0);

	float3 diffuse = AlbedoColor.rgb * (AmbientColor + LightColor * LightIntensity * NdotL);

	float specPower = max(2.0, (1.0 - Roughness) * 128.0);
	float3 specular = LightColor * LightIntensity * pow(NdotH, specPower) * lerp(0.04, AlbedoColor.rgb, Metallic);

	float3 finalColor = diffuse + specular;
	finalColor += EmissiveColor.rgb * EmissiveIntensity;

	return float4(finalColor, AlbedoColor.a);
}

technique PBR
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};
