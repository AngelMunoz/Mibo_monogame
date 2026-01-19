#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_5_0
	#define PS_SHADERMODEL ps_5_0
#endif

texture SceneTexture;
sampler SceneSampler = sampler_state { Texture = <SceneTexture>; AddressU = Clamp; AddressV = Clamp; MagFilter = Linear; MinFilter = Linear; };

texture BloomTexture;
sampler BloomSampler = sampler_state { Texture = <BloomTexture>; AddressU = Clamp; AddressV = Clamp; MagFilter = Linear; MinFilter = Linear; };

float ToneMapping = 0.0; // 0=None, 1=Reinhard, 2=ACES, 3=Filmic, 4=AgX
float Time = 0.0;

struct VertexShaderInput {
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

struct VertexShaderOutput {
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

VertexShaderOutput MainVS(in VertexShaderInput input) {
    VertexShaderOutput output;
    output.Position = input.Position;
    output.TexCoord = input.TexCoord;
    return output;
}

// ACES Tone Mapping
float3 ACESFilm(float3 x) {
    float a = 2.51;
    float b = 0.03;
    float c = 2.43;
    float d = 0.59;
    float e = 0.14;
    return saturate((x*(a*x+b))/(x*(c*x+d)+e));
}

float4 MainPS(VertexShaderOutput input) : COLOR0 {
    float4 scene = tex2D(SceneSampler, input.TexCoord);
    float4 bloom = tex2D(BloomSampler, input.TexCoord);
    
    // Additive Bloom
    float3 color = scene.rgb + bloom.rgb;
    
    // Slight Time-based pulsing effect to verify binding (Restored as requested)
    color *= (1.0 + sin(Time * 2.0) * 0.02);
    
    // Apply Tone Mapping
    if (ToneMapping == 1.0) color = color / (color + 1.0); // Reinhard
    else if (ToneMapping == 2.0) color = ACESFilm(color);
    
    return float4(color, scene.a);
}

technique PostProcess {
    pass P0 {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};
