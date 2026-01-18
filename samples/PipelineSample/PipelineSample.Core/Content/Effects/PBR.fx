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
float HasAlbedoMap = 0.0; 
texture AlbedoMap;
sampler AlbedoSampler = sampler_state
{
	Texture = <AlbedoMap>;
	MagFilter = Linear; MinFilter = Linear; MipFilter = Linear;
	AddressU = Wrap; AddressV = Wrap;
};

// Lighting Buffers (Unconstrained)
texture LightDataTexture;
sampler LightDataSampler = sampler_state { 
    Texture = <LightDataTexture>; 
    MinFilter = Point; MagFilter = Point; MipFilter = None; 
    AddressU = Clamp; AddressV = Clamp;
};
float LightCount = 0;

texture ShadowMatrixTexture;
sampler ShadowMatrixSampler = sampler_state { 
    Texture = <ShadowMatrixTexture>; 
    MinFilter = Point; MagFilter = Point; MipFilter = None; 
    AddressU = Clamp; AddressV = Clamp;
};
float ShadowMatrixCount = 0;

// Shadow Atlas
texture ShadowAtlas;
sampler ShadowAtlasSampler = sampler_state
{
    Texture = <ShadowAtlas>;
    MinFilter = Point; MagFilter = Point; MipFilter = None;
    AddressU = Clamp; AddressV = Clamp;
};
float ShadowAtlasTilesX = 4.0;
float ShadowAtlasSize = 4096.0;

float3 AmbientColor;
float ShadowBias = 0.0005;
float ShadowNormalBias = 0.001;

struct VertexShaderOutput {
	float4 Position : SV_POSITION;
	float2 TexCoord : TEXCOORD0;
	float3 Normal : TEXCOORD1;
    float3 WorldPos : TEXCOORD4;
};

float4 FetchData(sampler s, float2 pixelCoord, float width, float height) {
    float2 uv = (pixelCoord + 0.5) / float2(width, height);
    return tex2Dlod(s, float4(uv, 0, 0));
}

matrix FetchMatrix(int index) {
    float startRow = (float)index * 4.0;
    float4 r1 = FetchData(ShadowMatrixSampler, float2(0, startRow), 4.0, ShadowMatrixCount);
    float4 r2 = FetchData(ShadowMatrixSampler, float2(1, startRow), 4.0, ShadowMatrixCount);
    float4 r3 = FetchData(ShadowMatrixSampler, float2(2, startRow), 4.0, ShadowMatrixCount);
    float4 r4 = FetchData(ShadowMatrixSampler, float2(3, startRow), 4.0, ShadowMatrixCount);
    return matrix(r1, r2, r3, r4);
}

float CalculateShadow(int shadowIdx, float3 worldPos, float3 normal) {
    matrix lView = FetchMatrix(shadowIdx * 2);
    matrix lProj = FetchMatrix(shadowIdx * 2 + 1);

    float4 shadowCoord = mul(float4(worldPos + normal * ShadowNormalBias, 1.0), lView);
    shadowCoord = mul(shadowCoord, lProj);

    float3 projCoords = shadowCoord.xyz / shadowCoord.w;
    float2 uv = float2(0.5 * projCoords.x + 0.5, -0.5 * projCoords.y + 0.5);
    float z = projCoords.z;

    // Check bounds of light frustum
    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0 || z < 0.0 || z > 1.0) return 1.0;

    // Convert to Atlas UVs
    float col = fmod((float)shadowIdx, ShadowAtlasTilesX);
    float row = floor((float)shadowIdx / ShadowAtlasTilesX);
    
    float tileSize = 1.0 / ShadowAtlasTilesX;
    float2 atlasUV = (uv * tileSize) + float2(col * tileSize, row * tileSize);

    // PCF or simple sample
    float pcfDepth = tex2Dlod(ShadowAtlasSampler, float4(atlasUV, 0, 0)).r;
    
    return (z > pcfDepth + ShadowBias) ? 0.1 : 1.0;
}

float4 MainPS(VertexShaderOutput input) : COLOR0
{
	float4 albedo = (HasAlbedoMap > 0.5) ? tex2D(AlbedoSampler, input.TexCoord) * AlbedoColor : AlbedoColor;
    float3 normal = normalize(input.Normal);
    float3 diffuse = AmbientColor;

    for(int i = 0; i < (int)LightCount; i++) {
        float h = LightCount;
        float4 p0 = FetchData(LightDataSampler, float2(0, (float)i), 4.0, h);
        float type = p0.x;
        float intensity = p0.y;
        float range = p0.z;
        int shadowIdx = (int)p0.w;

        float4 p1 = FetchData(LightDataSampler, float2(1, (float)i), 4.0, h);
        float4 p2 = FetchData(LightDataSampler, float2(2, (float)i), 4.0, h);
        float4 p3 = FetchData(LightDataSampler, float2(3, (float)i), 4.0, h);

        float3 lightDir;
        float atten = 1.0;

        if (type == 0.0) { 
            lightDir = -normalize(p2.xyz);
        } else { 
            float3 L = p1.xyz - input.WorldPos;
            float dist = length(L);
            if (dist > range) continue;
            
            lightDir = L / dist;
            atten = pow(max(1.0 - (dist / range), 0.0), 2.0);

            if (type == 2.0) { 
                float cosOuter = p1.w;
                float cosInner = p2.w;
                float theta = dot(lightDir, -normalize(p2.xyz));
                if (theta <= cosOuter) continue;
                atten *= clamp((theta - cosOuter) / (cosInner - cosOuter), 0.0, 1.0);
            }
        }

        float ndotl = max(dot(normal, lightDir), 0.0);
        float shadow = 1.0;
        if (shadowIdx >= 0) shadow = CalculateShadow(shadowIdx, input.WorldPos, normal);
        
        diffuse += ndotl * p3.rgb * intensity * atten * shadow;
    }

	return float4(albedo.rgb * diffuse, albedo.a);
}

VertexShaderOutput MainVS(in float4 Position : POSITION0, in float2 TexCoord : TEXCOORD0, in float3 Normal : NORMAL0) {
    VertexShaderOutput output;
    float4 worldPosition = mul(Position, World);
    output.Position = mul(mul(worldPosition, View), Projection);
    output.TexCoord = TexCoord;
    output.Normal = normalize(mul(Normal, (float3x3)World));
    output.WorldPos = worldPosition.xyz;
    return output;
}

technique Forward { pass P0 { VertexShader = compile VS_SHADERMODEL MainVS(); PixelShader = compile PS_SHADERMODEL MainPS(); } };
