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

int DirectionalLightCount;
float2 DirectionalLightDirections[8];
float4 DirectionalLightColors[8];
float2 DirectionalLightShadowOrigins[8];

float TileSize;
float TilesX;
int MaxLightsPerTile;
float LightIndexBufferWidth;
float LightIndexBufferHeight;

// Viewport dimensions
float2 ViewportSize;
float2 ViewportSizeInv;

// Camera matrices
matrix World;
matrix View;
matrix Projection;

// Shadow parameters
Texture2D Texture;
sampler2D SpriteSampler = sampler_state
{
	Texture = <Texture>;
};

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
float ProjectionSize; // World space size of the projection volume (half-extent)
float PointLightShadowIndices[16];
float DirectionalLightShadowIndices[8];

Texture2D LightIndexBuffer;
Texture2D NormalMap;

sampler2D NormalSampler = sampler_state
{
	Texture = <NormalMap>;
	AddressU = Clamp;
	AddressV = Clamp;
};

sampler2D TileSampler = sampler_state
{
	Texture = <LightIndexBuffer>;
	Filter = Point;
	AddressU = Clamp;
	AddressV = Clamp;
};

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
VSOutput SpriteVS(VSInput input)
{
	VSOutput output;
	// Standard transform: World * View * Projection
	float4 worldPos = mul(input.Position, World);
	float4 viewPos = mul(worldPos, View);
	output.Position = mul(viewPos, Projection);

	output.Color = input.Color;
	output.TexCoord = input.TexCoord;
	// Pass original world position (before transform) to pixel shader
	output.WorldPos = input.Position.xy;
	return output;
}

// Helper function to compute shadow factor for a light
float ComputeShadow(int lightIdx, int isPointLight, float2 worldPos)
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

	if (shadowIndex < 0) return 1.0;

	float u, dist;

	if (isPointLight)
	{
		// Point Light: Polar sampling
		float2 diff = worldPos - PointLightPositions[lightIdx];
		dist = length(diff) / PointLightRadii[lightIdx];
		float angle = atan2(diff.y, diff.x);
		u = (angle + 3.14159) / 6.28318;
	}
	else
	{
		// Directional Light: Orthographic sampling
        float2 origin = DirectionalLightShadowOrigins[lightIdx];
        float2 relPos = worldPos - origin;
        
		float2 perpDir = float2(DirectionalLightDirections[lightIdx].y, -DirectionalLightDirections[lightIdx].x);
		float proj = dot(relPos, perpDir);
		u = (proj / ProjectionSize + 1.0) * 0.5;
        
        // Depth relative to origin (matches shadowcaster.fx)
		dist = (dot(relPos, DirectionalLightDirections[lightIdx]) + ProjectionSize) / (ProjectionSize * 2.0);
	}

	float v = (float(shadowIndex) + 0.5) / ShadowAtlasSize.y;
    
    // Check if we are outside the shadow map bounds
    if (u < 0.0 || u > 1.0) return 1.0;
    
	float occluderDist = tex2D(ShadowAtlasSampler, float2(u, v)).r;

	if (dist > occluderDist + ShadowBias) return 0.0;
	return 1.0;
}

// Pixel Shader
float4 MainPS(VSOutput input) : COLOR
{
	float4 texColor = tex2D(SpriteSampler, input.TexCoord) * input.Color;
	if (texColor.a < 0.1) discard;

	// Sample Normal Map
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

	[unroll(16)]
	for (int i = 0; i < 16; i++)
	{
		if (i >= MaxLightsPerTile) break;

		int bufferIndex = startOffset + i;
		int lightIdx = -1;

		float u = (fmod(float(bufferIndex), LightIndexBufferWidth) + 0.5) / LightIndexBufferWidth;
		float v = (floor(float(bufferIndex) / LightIndexBufferWidth) + 0.5) / LightIndexBufferHeight;

		#if OPENGL
			lightIdx = (int)tex2Dlod(TileSampler, float4(u, v, 0, 0)).r;
		#else
			lightIdx = (int)tex2Dlod(TileSampler, float4(u, v, 0, 0)).r;
		#endif

		if (lightIdx < 0) break;
		if (lightIdx >= 16) continue;

		// Point Light Calculation
		float2 lightDir = PointLightPositions[lightIdx] - worldPos;
		float dist = length(lightDir);

		if (dist < PointLightRadii[lightIdx])
		{
			float atten = pow(max(0.0, 1.0 - (dist / PointLightRadii[lightIdx])), PointLightFalloffs[lightIdx]);
			float3 diffuse = PointLightColors[lightIdx].rgb * atten;

			float2 nLightDir = normalize(lightDir);
			float dotNL = max(0.0, dot(normal, float3(nLightDir, 0.5)));

			float shadow = ComputeShadow(lightIdx, 1, worldPos);
			finalLight += diffuse * dotNL * shadow;
		}
	}

	// Process directional lights
	[unroll(8)]
	for (int d = 0; d < 8; d++)
	{
		if (d >= DirectionalLightCount) break;

		float2 lightDir = -DirectionalLightDirections[d];
		float dotNL = max(0.0, dot(normal, float3(lightDir, 0.5)));

		float shadow = ComputeShadow(d, 0, worldPos);
		finalLight += DirectionalLightColors[d].rgb * dotNL * shadow;
	}

	// Clamp to prevent saturation
	finalLight = min(finalLight, float3(1.0, 1.0, 1.0));

	return float4(texColor.rgb * finalLight, texColor.a);
}

technique SpriteBatch
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL SpriteVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};
