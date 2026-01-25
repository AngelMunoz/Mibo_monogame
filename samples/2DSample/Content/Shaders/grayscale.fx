#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_5_0
	#define PS_SHADERMODEL ps_5_0
#endif

// Lighting Uniforms
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

// Viewport dimensions
float2 ViewportSize;
float2 ViewportSizeInv;

// Camera matrices
matrix World;
matrix View;
matrix Projection;

Texture2D LightIndexBuffer;
Texture2D SpriteTexture;
Texture2D NormalMap;

sampler2D SpriteSampler = sampler_state { Texture = <SpriteTexture>; };
sampler2D NormalSampler = sampler_state { Texture = <NormalMap>; AddressU = Clamp; AddressV = Clamp; };
sampler2D TileSampler = sampler_state { Texture = <LightIndexBuffer>; Filter = Point; AddressU = Clamp; AddressV = Clamp; };

// Vertex Shader Input
struct VSInput
{
	float4 Position : POSITION0;
	float4 Color : COLOR0;
	float2 TexCoord : TEXCOORD0;
};

// Vertex Shader Output
struct VSOutput
{
	float4 Position : SV_POSITION;
	float4 Color : COLOR0;
	float2 TexCoord : TEXCOORD0;
	float2 WorldPos : TEXCOORD1;
};

// Vertex Shader - transforms to clip space and passes world position
VSOutput MainVS(VSInput input)
{
	VSOutput output;
	// Standard transform: World * View * Projection
	float4 worldPos = mul(input.Position, World);
	float4 viewPos = mul(worldPos, View);
	output.Position = mul(viewPos, Projection);
	output.Color = input.Color;
	output.TexCoord = input.TexCoord;
	// Pass original world position to pixel shader
	output.WorldPos = input.Position.xy;
	return output;
}

// --- Pixel Shader ---

float4 MainPS(VSOutput input) : COLOR
{
	float4 texColor = tex2D(SpriteSampler, input.TexCoord) * input.Color;
    if (texColor.a < 0.1) discard;

    // 1. Apply Grayscale (The core purpose of this shader)
    float gray = dot(texColor.rgb, float3(0.299, 0.587, 0.114));
    texColor.rgb = float3(gray, gray, gray);

    // 2. Apply Lighting (Contract Fulfillment)
    float3 normal = float3(0, 0, 1);
    float4 nData = tex2D(NormalSampler, input.TexCoord);
    if (any(nData.rgb)) {
        normal = normalize(nData.rgb * 2.0 - 1.0);
    }

    float3 finalLight = AmbientColor.rgb;
    float2 worldPos = input.WorldPos;

    // World-space tiled lookup
    float2 tileCoord = floor(worldPos / TileSize);
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
            float fetchCoord = (float(bufferIndex) + 0.5) / LightIndexBufferWidth;
            lightIdx = (int)tex2Dlod(TileSampler, float4(fetchCoord, 0.5, 0, 0)).r;
        #else
            lightIdx = (int)LightIndexBuffer.Load(int3(bufferIndex, 0, 0)).r;
        #endif

        if (lightIdx < 0) break;
        if (lightIdx >= 16) continue;

        float2 lightDir = PointLightPositions[lightIdx] - worldPos;
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

        float2 lightDir = -DirectionalLightDirections[d];
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
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};
