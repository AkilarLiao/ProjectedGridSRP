/// <summary>
/// Author: AkilarLiao
/// Date: 2022/02/20
/// Desc:
/// </summary>
Shader "CustomURP/ProjectedGridTest"
{
    Properties
    {   
        _BaseColor("Base Color", Color) = (1, 0, 0, 1)
        _WireColor("Wire Color", Color) = (0, 0, 0, 1)
        _PerlinNoiseSpeed("PerlinNoiseSpeed", Range(0.0, 10.0)) = 0.5
        _PerlinRnageRatio("PerlinRnageRatio", Range(0.1, 100.0)) = 20.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags{"LightMode" = "UniversalForward"}
            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0
            #pragma vertex VertexProgram
            #pragma fragment FragmentProgram
            #include "ProjectedGridTestImpl.hlsl"            
            ENDHLSL
        }
        Pass
        {
            Name "ForwardLit"
            Tags{"LightMode" = "WireFrame"}
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 4.0
            #pragma vertex VertexProgram
            #pragma geometry GeometryProgram
            #pragma fragment GeometryFragmentProgram
            #include "ProjectedGridTestImpl.hlsl"
            ENDHLSL
        }
    }
}