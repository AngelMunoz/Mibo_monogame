#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_1
	#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

matrix World;
matrix View;
matrix Projection;

float4 TopColor;
float4 BottomColor;
float StarIntensity; // 0.0 (Day) to 1.0 (Night)
float2 Resolution;

struct VertexShaderInput
{
	float4 Position : POSITION0;
	float2 TexCoord : TEXCOORD0;
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float2 TexCoord : TEXCOORD0;
    float4 ScreenPos : TEXCOORD1;
};

VertexShaderOutput MainVS(in VertexShaderInput input)
{
	VertexShaderOutput output;
	output.Position = mul(input.Position, mul(View, Projection));
	output.TexCoord = input.TexCoord;
    output.ScreenPos = output.Position;
	return output;
}

float rand(float2 co)
{
    return frac(sin(dot(co.xy ,float2(12.9898,78.233))) * 43758.5453);
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
    // Gradient
    float4 color = lerp(BottomColor, TopColor, input.TexCoord.y);
    
    // Stars
    if (StarIntensity > 0.01)
    {
        // Use screen coordinates for stable stars that don't stretch with camera zoom
        // Or world coordinates if we want them to move with camera.
        // Let's use UVs for now, assuming the quad covers the view.
        
        float2 noiseUV = input.TexCoord * Resolution / 2.0; // Scale for density
        float r = rand(floor(noiseUV)); // Pixelated stars
        
        if (r > 0.995) // Threshold for stars
        {
            float brightness = (r - 0.995) / (1.0 - 0.995);
            // Twinkle
            brightness *= (0.5 + 0.5 * sin(input.TexCoord.x * 100.0 + input.TexCoord.y * 50.0));
            
            color += float4(1, 1, 1, 1) * brightness * StarIntensity;
        }
    }

	return color;
}

technique Sky
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};
