#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_5_0
	#define PS_SHADERMODEL ps_5_0
#endif

// ============================================================================
// Uniform Parameters
// ============================================================================

// Point Light (Polar Projection)
float2 LightPosition;    // World space position
float LightRadius;       // Maximum range for depth calculation

// Directional Light (Orthographic Projection)
float2 LightDirection;   // Normalized direction vector

// Atlas dimensions
float AtlasWidth;        // Width of shadow atlas (angular resolution for point lights)
float AtlasHeight;       // Height (number of light rows)

// Sentinel value: Radius < 0 indicates directional light, point light otherwise
// Set by CPU: LightRadius < 0 = Directional, LightRadius >= 0 = Point

// ============================================================================
// Input / Output Structures
// ============================================================================

struct VSInput
{
	float2 Position : POSITION;
};

struct VSOutput
{
	float4 Position : SV_POSITION;
	float Depth : TEXCOORD0;
};

// ============================================================================
// Vertex Shader
// ============================================================================

VSOutput MainVS(VSInput input)
{
	VSOutput output;

	if (LightRadius < 0.0)
	{
		// ============================================================
		// Orthographic Projection (Directional Light)
		// ============================================================

		// Calculate perpendicular direction for X-axis projection
		float2 perpDir = float2(LightDirection.y, -LightDirection.x);

		// Project position onto perpendicular direction
		float proj = dot(input.Position, perpDir);

		// Map projection to clip space X ([-1, 1])
		float x = (proj / AtlasWidth + 1.0) * 0.5;
		output.Position.x = x * 2.0 - 1.0;

		// Depth along light direction (0 to 1)
		// We use AtlasWidth as a reference scale for depth
		float depth = dot(input.Position, LightDirection) / AtlasWidth;
		output.Position.z = saturate(depth);

		output.Depth = output.Position.z;
	}
	else
	{
		// ============================================================
		// Polar Projection (Point Light)
		// ============================================================

		// Calculate vector from light to pixel
		float2 diff = input.Position - LightPosition;

		// Angle in radians (-PI to PI)
		float angle = atan2(diff.y, diff.x);

		// Map angle to clip space X ([-1, 1])
		float x = angle / 3.14159;  // -PI to PI -> -1 to 1
		output.Position.x = x;

		// Depth as normalized distance (0 to 1)
		float depth = length(diff) / max(LightRadius, 0.001);
		output.Position.z = saturate(depth);

		output.Depth = output.Position.z;
	}

	// Fixed Y position (each light gets its own row via viewport scissor)
	output.Position.y = 0.0;
	output.Position.w = 1.0;

	return output;
}

// ============================================================================
// Pixel Shader (Pass depth through)
// ============================================================================

float4 MainPS(VSOutput input) : COLOR
{
	// Output depth as white (higher values = further from light)
	return float4(input.Depth, input.Depth, input.Depth, 1.0);
}

// ============================================================================
// Technique
// ============================================================================

technique ShadowCaster
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
}
