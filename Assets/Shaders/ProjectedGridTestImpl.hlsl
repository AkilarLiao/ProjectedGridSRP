/// <summary>
/// Author: AkilarLiao
/// Date: 2022/02/20
/// Desc:
/// </summary>
#ifndef PROJECTED_GRID_TEST_IMPL_INCLUDED
#define PROJECTED_GRID_TEST_IMPL_INCLUDED

#include "ProjectedGridHelper.hlsl"
#include "Noise.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct VertexInput
{
	float4 positionOS	: POSITION;
	float2 uvOS : TEXCOORD0;
};

struct VertexOutput
{
	float4 positionCS	: SV_POSITION;	
	float3 worldPosition : TEXCOORD0;
};

struct GeometryOutput
{
	float4 positionCS	: SV_POSITION;
	float3 distance		: TEXCOORD0;
};

CBUFFER_START(UnityPerMaterial)
half3 _BaseColor;
half3 _WireColor;
float _PerlinNoiseSpeed;
float _PerlinRnageRatio;
CBUFFER_END

VertexOutput VertexProgram(VertexInput input)
{
	VertexOutput output = (VertexOutput)0;
	float3 projectedPosition = GetProjectedGridPos(float4(input.positionOS.xy,
		input.uvOS)).xyz;

	float2 offestPosition = projectedPosition.xz + _Time.y * _PerlinNoiseSpeed;
	projectedPosition.y += snoise(offestPosition) * _PerlinRnageRatio;

	output.worldPosition = projectedPosition;
	output.positionCS = TransformWorldToHClip(projectedPosition);
	return output;
}

[maxvertexcount(3)]
void GeometryProgram(triangle VertexOutput input[3], inout TriangleStream<GeometryOutput> triStream)
{
	float2 screenScale = float2(_ScreenParams.x / 2.0, _ScreenParams.y / 2.0);

	//frag position
	float2 p0 = screenScale * input[0].positionCS.xy / input[0].positionCS.w;
	float2 p1 = screenScale * input[1].positionCS.xy / input[1].positionCS.w;
	float2 p2 = screenScale * input[2].positionCS.xy / input[2].positionCS.w;

	//barycentric position
	float2 v0 = p2 - p1;
	float2 v1 = p2 - p0;
	float2 v2 = p1 - p0;

	//triangles area
	float area = abs(v1.x * v2.y - v1.y * v2.x);

	GeometryOutput output;
	output.positionCS = input[0].positionCS;
	output.distance = float3(area / length(v0), 0, 0);
	triStream.Append(output);

	output.positionCS = input[1].positionCS;
	output.distance = float3(0, area / length(v1), 0);
	triStream.Append(output);

	output.positionCS = input[2].positionCS;
	output.distance = float3(0, 0, area / length(v2));
	triStream.Append(output);
}

half4 GeometryFragmentProgram(GeometryOutput input) : SV_Target
{
	//distance of frag from triangles center
	float minDistance = min(input.distance.x, min(input.distance.y, input.distance.z));
	//fade based on dist from center
	float wireRatio = exp2(-4.0 * minDistance * minDistance);
	return half4(_WireColor, wireRatio);
}

real3 CalculateNormal(in float3 worldPosition)
{
	float offestValue = 1.0;

	float3 offestA = float3(
		worldPosition.x + offestValue,
		snoise(worldPosition.xz + float2(offestValue, offestValue)) * _PerlinRnageRatio,
		worldPosition.z + offestValue);

	float3 offestB = float3(
		worldPosition.x - offestValue,
		snoise(worldPosition.xz + float2(-offestValue, -offestValue)) * _PerlinRnageRatio,
		worldPosition.z - offestValue);

	real3 normal = normalize(cross(offestA - worldPosition,
		offestB - worldPosition));

	//_ProjectionParams
	//x: is 1.0 (or ¡V1.0 if currently rendering with a flipped projection matrix),
	//y: is the camera¡¦s near plane,
	//z: is the camera¡¦s far plane,
	//w: is 1 / FarPlane.
	
	//the normal dissolve in the far distance
	float theLength = length(_WorldSpaceCameraPos - worldPosition);
	float ratio = min(theLength, _ProjectionParams.z) / _ProjectionParams.z;
	normal = lerp(normal, real3(0.0, 1.0, 0.0), saturate(ratio * 20.0));

	return normal;
}

half3 CalculateDiffuse(in real3 normal, in real3 lightDirection, in half3 lightColor,
	in half3 diffuseColor)
{
	half dotValue = dot(normal, lightDirection);
	//Half Lambert
	half NdotL = dotValue * 0.5 + 0.5;	
	half3 lightDiffuse = lightColor * max(0.65, NdotL);

	half3 vertexSH = half3(0.5, 0.5, 0.5);
	return diffuseColor * (vertexSH + lightDiffuse);
}

half4 FragmentProgram(VertexOutput input) : SV_Target
{
	real3 normal = CalculateNormal(input.worldPosition);
	Light mainLight = GetMainLight();
	
	half3 lightResult = CalculateDiffuse(normal, mainLight.direction,
		mainLight.color, _BaseColor);

	return half4(lightResult, 1.0);
}
#endif //PROJECTED_GRID_TEST_IMPL_INCLUDED