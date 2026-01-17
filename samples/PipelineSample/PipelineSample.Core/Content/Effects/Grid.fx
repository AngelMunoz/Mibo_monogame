 // -----------------------------------------------------------------------------
// Grid.fx - Path: samples/3DSample/Content/Effects/Grid.fx
// -----------------------------------------------------------------------------

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

float3 PlayerPosition;
float MaxDistance;

struct VertexShaderInput
{
    float4 Position : SV_Position;
    float4 Color : COLOR0;
};

struct VertexShaderOutput
{
    float4 Position : SV_Position;
    float4 Color : COLOR0;
};

VertexShaderOutput MainVS(in VertexShaderInput input)
{
    VertexShaderOutput output;

    float4 worldPosition = mul(input.Position, World);
    float4 viewPosition = mul(worldPosition, View);
    output.Position = mul(viewPosition, Projection);

    float dist = distance(worldPosition.xyz, PlayerPosition);
    float alpha = 1.0 - saturate(dist / MaxDistance);
    alpha = pow(alpha, 1); // Slightly sharper than 1.5, still softer than quadratic

    output.Color = input.Color * alpha;

    return output;
}

float4 MainPS(VertexShaderOutput input) : COLOR0
{
    return input.Color;
}

technique Fade
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}
