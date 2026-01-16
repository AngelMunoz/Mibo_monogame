#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_1
	#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// G-Buffer textures
texture AlbedoMap;
sampler AlbedoSampler = sampler_state { Texture = <AlbedoMap>; MagFilter = Point; MinFilter = Point; AddressU = Clamp; AddressV = Clamp; };

texture NormalMap;
sampler NormalSampler = sampler_state { Texture = <NormalMap>; MagFilter = Point; MinFilter = Point; AddressU = Clamp; AddressV = Clamp; };

texture DepthMap;
sampler DepthSampler = sampler_state { Texture = <DepthMap>; MagFilter = Point; MinFilter = Point; AddressU = Clamp; AddressV = Clamp; };

// Lighting
float3 AmbientColor;
float3 LightDirections[3];
float3 LightColors[3];

// Point Lights
float4 PointLightData[32];   // xyz = position, w = range
float4 PointLightColors[32]; // rgb = color * intensity
float PointLightCount = 0;

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
matrix InvertViewProjection;

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

float CalculateShadow(float4 shadowCoord)
{
    float3 projCoords = shadowCoord.xyz / shadowCoord.w;
    float2 uv = float2(0.5 * projCoords.x + 0.5, -0.5 * projCoords.y + 0.5);
    float z = projCoords.z;

    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0 || z < 0.0 || z > 1.0)
        return 1.0;

    float shadow = 0.0;
    float2 texelSize = float2(1.0 / 2048.0, 1.0 / 2048.0);
    float bias = 0.002;

    for(int x = -1; x <= 1; ++x)
    {
        for(int y = -1; y <= 1; ++y)
        {
            float pcfDepth = tex2Dlod(ShadowSampler, float4(uv + float2(x, y) * texelSize, 0, 0)).r; 
            shadow += (z > pcfDepth + bias) ? 0.1 : 1.0;
        }
    }
    return shadow / 9.0;
}

float4 MainPS(VertexShaderOutput input) : COLOR0
{
    // 1. Sample G-Buffer
	float4 albedo = tex2D(AlbedoSampler, input.TexCoord);
    if (albedo.a <= 0.0) discard;

	float3 normal = normalize(tex2D(NormalSampler, input.TexCoord).rgb * 2.0 - 1.0);
    float depth = tex2D(DepthSampler, input.TexCoord).r;
    
    // Discard background pixels
    if (depth >= 0.999) discard;

    // 2. Reconstruct World Position from NDC
    float4 clipPos;
    clipPos.x = input.TexCoord.x * 2.0 - 1.0;
    clipPos.y = (1.0 - input.TexCoord.y) * 2.0 - 1.0;
    clipPos.z = depth;
    clipPos.w = 1.0;

    float4 worldPos = mul(clipPos, InvertViewProjection);
    worldPos /= worldPos.w;

    // 3. Directional Lighting
    // Calculate shadow coordinates for the reconstructed world position
    float4 shadowPos = mul(worldPos, LightView);
    shadowPos = mul(shadowPos, LightProjection);
    float shadow = CalculateShadow(shadowPos);

    float3 diffuse = AmbientColor;
    
    for(int i = 0; i < 3; i++)
    {
        float ndotl = max(dot(normal, -normalize(LightDirections[i])), 0.0);
        float atten = (i == 0) ? shadow : 1.0;
        diffuse += ndotl * LightColors[i] * atten;
    }

    // 4. Point Lights (Standard Loop)
    for(int j = 0; j < 32; j++)
    {
        if (j >= (int)PointLightCount) break;

        float3 lightDir = PointLightData[j].xyz - worldPos.xyz;
        float dist = length(lightDir);
        float range = PointLightData[j].w;
        
        if (dist < range)
        {
            lightDir /= dist;
            float ndotl = max(dot(normal, lightDir), 0.0);
            
            // Attenuation matching Forward mode: (1 - dist/range)^2
            float atten = pow(max(1.0 - (dist / range), 0.0), 2.0);
            
            diffuse += ndotl * PointLightColors[j].rgb * atten;
        }
    }

	return float4(albedo.rgb * diffuse, albedo.a);
}

technique DeferredLighting
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};
