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

float TileSize;
float TilesX;
int MaxLightsPerTile;
float LightIndexBufferWidth;

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

// OpenGL-compatible sampler for light index buffer
sampler2D TileSampler = sampler_state
{
	Texture = <LightIndexBuffer>;
    Filter = Point;
    AddressU = Clamp;
    AddressV = Clamp;
};

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

            finalLight += diffuse * dotNL;
        }
    }

    // Process directional lights (simpler - no attenuation, just direction)
    for (int d = 0; d < 8; d++)
    {
        if (d >= DirectionalLightCount) break;

        float2 lightDir = -DirectionalLightDirections[d]; // Negate for "coming from" direction
        float dotNL = max(0.0, dot(normal, float3(lightDir, 0.5)));
        finalLight += DirectionalLightColors[d].rgb * dotNL;
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
