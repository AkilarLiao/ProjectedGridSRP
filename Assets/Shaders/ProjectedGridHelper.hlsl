/// <summary>
/// Author: AkilarLiao
/// Date: 2023/02/20
/// Desc:
/// </summary>
#ifndef PROJECTED_GRID_HELPER_INCLUDED
#define PROJECTED_GRID_HELPER_INCLUDED

//--------------------------------------------------------
// Find the world position on the projection plane by
// by interpolating the frustums projected corner points.
// The uv is the screen space in 0-1 range.
// Also adds a border around the mesh to prevent horizontal
// displacement from pulling the mesh from the screen.
//--------------------------------------------------------
uniform float4x4 _ProjectedGridInterpolationMatrix;
uniform float4x4 _ProjectedGridViewPortMatrix;
uniform float _ProjectedGridEdgeBorder;
uniform float _PlaneHeight;
//--------------------------------------------------------

//x,y is local position(x,y)
//z,w is local uv
float4 GetProjectedGridPos(float4 uv)
{
	//Interpolation must use a uv in range of 0-1
	//Should be in 0-1 but saturate just in case.
	uv.xy = saturate(uv.xy);

	//Interpolate between frustums world space projection points.
	float4 p = lerp(lerp(_ProjectedGridInterpolationMatrix[0], 
		_ProjectedGridInterpolationMatrix[1], uv.x),
		lerp(_ProjectedGridInterpolationMatrix[3], 
			_ProjectedGridInterpolationMatrix[2], uv.x), uv.y);
	p = p / p.w;

	//Find the world position of the screens center position.
	float4 c = lerp(lerp(_ProjectedGridInterpolationMatrix[0], 
		_ProjectedGridInterpolationMatrix[1], 0.5),
		lerp(_ProjectedGridInterpolationMatrix[3], 
			_ProjectedGridInterpolationMatrix[2], 0.5), 0.5);
	c = c / c.w;

	//Find the direction this position is relative to the meshes center.
	float3 worldDir = normalize(p.xyz - c.xyz);

	//if p and c are the same value the normalized
	//results in a nan on ATI cards. Clamp fixes this.
	worldDir = clamp(worldDir, -1, 1);

	//Apply edge border by pushing those verts in the border 
	//in the direction away from the center.
	float mask = saturate(uv.z + uv.w);
	p.xz += worldDir.xz * mask * _ProjectedGridEdgeBorder;

	return p;
}

#endif //PROJECTED_GRID_HELPER_INCLUDED