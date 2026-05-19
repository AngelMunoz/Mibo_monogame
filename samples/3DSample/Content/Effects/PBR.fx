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

// Albedo
float4 AlbedoColor = float4(1, 1, 1, 1);
float HasAlbedoMap = 0.0;
texture AlbedoMap;
sampler AlbedoSampler = sampler_state
{
	Texture = <AlbedoMap>;
	MagFilter = Linear; MinFilter = Linear; MipFilter = Linear;
	AddressU = Wrap; AddressV = Wrap;
};

// Normal map
float HasNormalMap = 0.0;
texture NormalMap;
sampler NormalSampler = sampler_state
{
	Texture = <NormalMap>;
	MagFilter = Linear; MinFilter = Linear; MipFilter = Linear;
	AddressU = Wrap; AddressV = Wrap;
};

// Metallic / Roughness
float Metallic = 0.0;
float Roughness = 1.0;

// Emissive
float4 EmissiveColor = float4(0, 0, 0, 1);
float EmissiveIntensity = 0.0;

// Simple lighting (single directional light)
float3 LightDirection = float3(0.5, -1.0, 0.3);
float3 LightColor = float3(1, 1, 1);
float LightIntensity = 1.0;
float3 AmbientColor = float3(0.1, 0.1, 0.15);

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float2 TexCoord : TEXCOORD0;
	float3 Normal : TEXCOORD1;
	float3 Tangent : TEXCOORD2;
	float3 Bitangent : TEXCOORD3;
	float3 WorldPos : TEXCOORD4;
};

VertexShaderOutput MainVS(
	in float4 Position : POSITION0,
	in float2 TexCoord : TEXCOORD0,
	in float3 Normal : NORMAL0,
	in float4 Tangent : TANGENT0)
{
	VertexShaderOutput output;
	float4 worldPosition = mul(Position, World);
	output.Position = mul(mul(worldPosition, View), Projection);
	output.TexCoord = TexCoord;
	output.Normal = normalize(mul(Normal, (float3x3)World));
	output.WorldPos = worldPosition.xyz;

	// Compute tangent space basis for normal mapping
	float3 N = output.Normal;
	float3 T = normalize(mul(Tangent.xyz, (float3x3)World));
	T = normalize(T - dot(T, N) * N);
	float3 B = cross(N, T) * Tangent.w;
	output.Tangent = T;
	output.Bitangent = B;

	return output;
}

// Simple Blinn-Phong with metallic/roughness influence
float4 MainPS(VertexShaderOutput input) : COLOR0
{
	// Albedo
	float4 albedo = (HasAlbedoMap > 0.5)
		? tex2D(AlbedoSampler, input.TexCoord) * AlbedoColor
		: AlbedoColor;

	// Normal
	float3 N = input.Normal;
	if (HasNormalMap > 0.5)
	{
		float3 normalTex = tex2D(NormalSampler, input.TexCoord).rgb * 2.0 - 1.0;
		float3x3 TBN = float3x3(input.Tangent, input.Bitangent, input.Normal);
		N = normalize(mul(normalTex, TBN));
	}

	// Lighting
	float3 L = normalize(-LightDirection);
	float3 V = normalize(-input.WorldPos); // Approximate view dir (camera at origin)
	float3 H = normalize(L + V);

	float NdotL = max(dot(N, L), 0.0);
	float NdotH = max(dot(N, H), 0.0);

	// Diffuse
	float3 diffuse = albedo.rgb * (AmbientColor + LightColor * LightIntensity * NdotL);

	// Specular (Blinn-Phong with roughness)
	float specPower = max(2.0, (1.0 - Roughness) * 128.0);
	float3 specular = LightColor * LightIntensity * pow(NdotH, specPower) * lerp(0.04, albedo.rgb, Metallic);

	// Combine
	float3 finalColor = diffuse + specular;

	// Emissive
	finalColor += EmissiveColor.rgb * EmissiveIntensity;

	return float4(finalColor, albedo.a);
}

technique PBR
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};
