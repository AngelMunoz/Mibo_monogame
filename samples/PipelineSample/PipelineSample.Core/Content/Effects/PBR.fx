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
float3 LightDirections[16];
float3 LightColors[16];
float DirectionalLightCount = 0;

 // Point Lights
float4 PointLightData[32];   // xyz = position, w = range
float4 PointLightColors[32]; // rgb = color * intensity
float PointLightCount = 0;

// Spot Lights
float4 SpotLightData[16];    // xyz = position, w = range
float4 SpotLightArgs[16];    // xyz = direction, w = cosOuter
float4 SpotLightColors[16];  // rgb = color * intensity, w = cosInner
float SpotLightCount = 0;

// Shadow Mapping
texture ShadowMap;
sampler ShadowSampler = sampler_state
{
    Texture = <ShadowMap>;
    AddressU = Clamp;
    AddressV = Clamp;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = None;
};

matrix LightView;
matrix LightProjection;

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
    float4 ShadowCoord : TEXCOORD3;
    float3 WorldPos : TEXCOORD4;
};

VertexShaderOutput MainVS(in VertexShaderInput input)
{
	VertexShaderOutput output;

	float4 worldPosition = mul(input.Position, World);
	float4 viewPosition = mul(worldPosition, View);
	output.Position = mul(viewPosition, Projection);
	output.TexCoord = input.TexCoord;
    float3 normal = normalize(mul(input.Normal, (float3x3)World));
    output.Normal = normal;
    output.WorldPos = worldPosition.xyz;

    // Shadow coordinates
    float4 shadowPos = mul(worldPosition, LightView);
    shadowPos = mul(shadowPos, LightProjection);
    output.ShadowCoord = shadowPos;

	return output;
}

 float CalculateShadow(float4 shadowCoord)
 {
     // Perspective divide
     float3 projCoords = shadowCoord.xyz / shadowCoord.w;
     
     // Transform to [0,1] range
     float2 uv = float2(0.5 * projCoords.x + 0.5, -0.5 * projCoords.y + 0.5);
     float z = projCoords.z;

     // Check if outside shadow map range [0, 1]
     if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0 || z < 0.0 || z > 1.0)
         return 1.0;

     // PCF 3x3
     float shadow = 0.0;
     float2 texelSize = float2(1.0 / 2048.0, 1.0 / 2048.0);
     float bias = 0.002;

     for(int x = -1; x <= 1; ++x)
     {
         for(int y = -1; y <= 1; ++y)
         {
             float pcfDepth = tex2Dlod(ShadowSampler, float4(uv + float2(x, y) * texelSize, 0.0, 0.0)).r; 
             shadow += (z > pcfDepth + bias) ? 0.1 : 1.0;
         }
     }
     return shadow / 9.0;
 }

float4 MainPS(VertexShaderOutput input) : COLOR0
{
	// Use texture if available, otherwise just use AlbedoColor
	float4 albedo = (HasAlbedoMap > 0.5)
		? tex2D(AlbedoSampler, input.TexCoord) * AlbedoColor
		: AlbedoColor;

    float3 normal = normalize(input.Normal);
    float shadow = CalculateShadow(input.ShadowCoord);

    float3 diffuse = AmbientColor;

    // Directional Lights
    for(int i = 0; i < 16; i++)
    {
        if (i >= (int)DirectionalLightCount) break;
        float ndotl = max(dot(normal, -normalize(LightDirections[i])), 0.0);
        float atten = (i == 0) ? shadow : 1.0; 
        diffuse += ndotl * LightColors[i] * atten;
    }

     // Point Lights
    for(int j = 0; j < 32; j++)
    {
        if (j >= (int)PointLightCount) break;

        float3 lightDir = PointLightData[j].xyz - input.WorldPos;
        float dist = length(lightDir);
        float range = PointLightData[j].w;
        
        if (dist < range)
        {
            lightDir /= dist;
            float ndotl = max(dot(normal, lightDir), 0.0);
            float atten = pow(max(1.0 - (dist / range), 0.0), 2.0);
            diffuse += ndotl * PointLightColors[j].rgb * atten;
        }
    }

    // Spot Lights
    for(int k = 0; k < 16; k++)
    {
        if (k >= (int)SpotLightCount) break;

        float3 lightPos = SpotLightData[k].xyz;
        float3 lightDir = SpotLightArgs[k].xyz;
        float range = SpotLightData[k].w;
        float cosOuter = SpotLightArgs[k].w;
        float cosInner = SpotLightColors[k].w;

        float3 L = lightPos - input.WorldPos;
        float dist = length(L);

        if (dist < range)
        {
            L /= dist;
            float theta = dot(L, -normalize(lightDir));

            if (theta > cosOuter)
            {
                float ndotl = max(dot(normal, L), 0.0);
                float distAtten = pow(max(1.0 - (dist / range), 0.0), 2.0);
                
                // Cone falloff
                float epsilon = cosInner - cosOuter;
                float coneAtten = clamp((theta - cosOuter) / epsilon, 0.0, 1.0);
                
                diffuse += ndotl * SpotLightColors[k].rgb * distAtten * coneAtten;
            }
        }
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
