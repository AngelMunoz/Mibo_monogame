 #if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_5_0
	#define PS_SHADERMODEL ps_5_0
#endif

// G-Buffer textures
texture AlbedoMap;
sampler AlbedoSampler = sampler_state { Texture = <AlbedoMap>; MagFilter = Point; MinFilter = Point; AddressU = Clamp; AddressV = Clamp; };

texture NormalMap;
sampler NormalSampler = sampler_state { Texture = <NormalMap>; MagFilter = Point; MinFilter = Point; AddressU = Clamp; AddressV = Clamp; };

texture WorldPosMap;
sampler WorldPosSampler = sampler_state { Texture = <WorldPosMap>; MagFilter = Point; MinFilter = Point; AddressU = Clamp; AddressV = Clamp; };

// Lighting
float3 AmbientColor;
// Use individual parameters instead of arrays to fix MonoGame array binding issue
float3 LightDirection0;
float3 LightColor0;

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
     float bias = 0.002;  // Lower shadow bias

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
    // 1. Sample G-Buffer
	float4 albedo = tex2D(AlbedoSampler, input.TexCoord);

    // Discard if no geometry (Albedo Alpha will be 0 from clear)
    if (albedo.a < 0.001) discard;

	float3 normal = normalize(tex2D(NormalSampler, input.TexCoord).rgb);
    float3 worldPos = tex2D(WorldPosSampler, input.TexCoord).rgb;

    // 2. Calculate shadow coordinates from world position (no normal offset to match Forward mode)
    float4 shadowPos = mul(float4(worldPos, 1.0), LightView);
    shadowPos = mul(shadowPos, LightProjection);
    float shadow = CalculateShadow(shadowPos);

    // DEBUG: Visualize shadow value - WHITE = lit (1.0), BLACK = shadowed (0.0)
    // return float4(shadow, shadow, shadow, 1.0);

    float3 diffuse = AmbientColor;

    // Primary directional light with shadow (using individual params instead of array)
    float ndotl = max(dot(normal, -normalize(LightDirection0)), 0.0);
    diffuse += ndotl * LightColor0 * shadow;

     // 3. Point Lights
    for(int j = 0; j < 32; j++)
    {
        if (j >= (int)PointLightCount) break;

        float3 lightDir = PointLightData[j].xyz - worldPos;
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
