#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
    #define VPOS_SEMANTIC VPOS
#else
	#define VS_SHADERMODEL vs_5_0
	#define PS_SHADERMODEL ps_5_0
    #define VPOS_SEMANTIC SV_Position
#endif

// Established Contract Uniforms (Phase 3)
float4 AmbientColor;
int PointLightCount;
float2 PointLightPositions[16];
float4 PointLightColors[16];
float PointLightRadii[16];
float PointLightFalloffs[16];

// Directional lights (no position/radius, just direction)
int DirectionalLightCount;
float2 DirectionalLightDirections[8];
float4 DirectionalLightColors[8];

Texture2D LightIndexBuffer;
Texture2D SpriteTexture;
Texture2D NormalMap;

sampler2D SpriteSampler = sampler_state
{
	Texture = <SpriteTexture>;
};

sampler2D NormalSampler = sampler_state
{
	Texture = <NormalMap>;
    AddressU = Clamp;
    AddressV = Clamp;
};

// Shadow parameters
Texture2D ShadowAtlas;
sampler2D ShadowAtlasSampler = sampler_state
{
	Texture = <ShadowAtlas>;
    Filter = Point;
    AddressU = Clamp;
    AddressV = Clamp;
};

float2 ShadowAtlasSize;
float ShadowBias;
float PointLightShadowIndices[16];
float DirectionalLightShadowIndices[8];

float TileSize;
float TilesX;
int MaxLightsPerTile;
float LightIndexBufferWidth;

// OpenGL-compatible sampler for light index buffer
sampler2D TileSampler = sampler_state
{
	Texture = <LightIndexBuffer>;
    Filter = Point;
    AddressU = Clamp;
    AddressV = Clamp;
};

// Helper function to compute shadow factor for a light
float ComputeShadow(int lightIdx, int isPointLight, float2 pixelPos)
{
    int shadowIndex = -1;

    if (isPointLight)
    {
        if (lightIdx >= 0 && lightIdx < 16)
            shadowIndex = (int)PointLightShadowIndices[lightIdx];
    }
    else
    {
        if (lightIdx >= 0 && lightIdx < 8)
            shadowIndex = (int)DirectionalLightShadowIndices[lightIdx];
    }

    if (shadowIndex < 0) return 1.0; // No shadow for this light

    float u, dist;

    if (isPointLight)
    {
        // Point Light: Polar sampling
        float2 diff = pixelPos - PointLightPositions[lightIdx];
        dist = length(diff) / PointLightRadii[lightIdx]; // Normalize distance
        float angle = atan2(diff.y, diff.x);
        u = (angle + 3.14159) / 6.28318;  // 0..1
    }
    else
    {
        // Directional Light: Orthographic sampling
        // DirectionalLightDirections are now in screen space, pixelPos is in screen space
        float2 perpDir = float2(DirectionalLightDirections[lightIdx].y, -DirectionalLightDirections[lightIdx].x);
        float proj = dot(pixelPos, perpDir);
        u = (proj / ShadowAtlasSize.x + 1.0) * 0.5;
        dist = dot(pixelPos, DirectionalLightDirections[lightIdx]) / ShadowAtlasSize.x;
    }

    float v = (float(shadowIndex) + 0.5) / ShadowAtlasSize.y;
    float occluderDist = tex2D(ShadowAtlasSampler, float2(u, v)).r;

    if (dist > occluderDist + ShadowBias) return 0.0;
    return 1.0;
}

// --- Pixel Shader ---

float4 MainPS(float4 pos : VPOS_SEMANTIC, float4 color : COLOR0, float2 texCoord : TEXCOORD0) : COLOR
{
	float4 texColor = tex2D(SpriteSampler, texCoord) * color;
    if (texColor.a < 0.1) discard;

    // 1. Sample Normal Map
    float3 normal = float3(0, 0, 1);
    float4 nData = tex2D(NormalSampler, texCoord);

    if (any(nData.rgb)) {
        normal = normalize(nData.rgb * 2.0 - 1.0);
    }

    float3 finalLight = AmbientColor.rgb;
    float2 pixelPos = pos.xy;

    // 2. Tiled Lookup (Universal for GL/DX)
    float2 tileCoord = floor(pixelPos / TileSize);
    int tileIdx = (int)(tileCoord.y * TilesX + tileCoord.x);
    int startOffset = tileIdx * MaxLightsPerTile;

    // Bounds check: ensure we don't read beyond the buffer
    int maxOffset = (int)LightIndexBufferWidth;

    [unroll(16)]
    for (int i = 0; i < 16; i++)
    {
        if (i >= MaxLightsPerTile) break;

        int bufferIndex = startOffset + i;
        if (bufferIndex >= maxOffset) break; // Out of bounds

        int lightIdx = -1;
        #if OPENGL
            // Normalized lookup for OpenGL (MojoShader)
            float fetchCoord = (float(bufferIndex) + 0.5) / LightIndexBufferWidth;
            lightIdx = (int)tex2Dlod(TileSampler, float4(fetchCoord, 0.5, 0, 0)).r;
        #else
            // High-performance Load for SM 4.0+ (DirectX)
            lightIdx = (int)LightIndexBuffer.Load(int3(bufferIndex, 0, 0)).r;
        #endif

        if (lightIdx < 0) break;
        if (lightIdx >= 16) continue;

        // 3. Lighting Calculation
        float2 lightDir = PointLightPositions[lightIdx] - pixelPos;
        float dist = length(lightDir);

        if (dist < PointLightRadii[lightIdx])
        {
            float atten = pow(max(0.0, 1.0 - (dist / PointLightRadii[lightIdx])), PointLightFalloffs[lightIdx]);
            float3 diffuse = PointLightColors[lightIdx].rgb * atten;

            float2 nLightDir = normalize(lightDir);
            float dotNL = max(0.0, dot(normal, float3(nLightDir, 0.5)));

            float shadow = ComputeShadow(lightIdx, 1, pixelPos);
            finalLight += diffuse * dotNL * shadow;
        }
    }

    // Process directional lights (simpler - no attenuation, just direction)
    for (int d = 0; d < 8; d++)
    {
        if (d >= DirectionalLightCount) break;

        float2 lightDir = -DirectionalLightDirections[d]; // Negate for "coming from" direction
        float dotNL = max(0.0, dot(normal, float3(lightDir, 0.5)));

        float shadow = ComputeShadow(d, 0, pixelPos);
        finalLight += DirectionalLightColors[d].rgb * dotNL * shadow;
    }

    // Clamp to prevent white saturation
    finalLight = min(finalLight, float3(1.0, 1.0, 1.0));

	return float4(texColor.rgb * finalLight, texColor.a);
}

technique SpriteBatch
{
	pass P0
	{
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};
