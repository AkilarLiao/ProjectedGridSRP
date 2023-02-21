using UnityEngine;
using UnityEngine.Rendering.Universal;

using ProjectedGrid;
using UnityEngine.Experimental.Rendering.Universal;

[ExecuteAlways]
public class ProjectedGridsTest : MonoBehaviour, IProjectedGrids, IAddPassInterface
{
    private void OnEnable()
    {   
        m_ProjectedGrid.ReInitialize(this);

        string[] shaderTag = new string[] { "WireFrame" };
        m_drawWireFramePass = new RenderObjectsPass("WireFramePass",
            RenderPassEvent.AfterRenderingTransparents,
            shaderTag, RenderQueueType.Opaque, ~0, m_customCameraSettings);
        DynamicAddPassFeature.AppendAddPassInterfaces(this);
    }
    private void OnDisable()
    {
        DynamicAddPassFeature.RemoveAddPassInterfaces(this);

        m_ProjectedGrid.Release();
    }

    void IAddPassInterface.OnAddPass(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (m_showWireframe && (m_drawWireFramePass != null))
            renderer.EnqueuePass(m_drawWireFramePass);
    }

    MeshResolution IProjectedGrids.GetMeshResolution()
    {
        return m_meshResolution;
    }
    Material IProjectedGrids.GetMaterial()
    {
        return m_projectedGridsMaterial;
    }

    float IProjectionProcessor.GetPlaneHeight()
    {
        return m_planeHeight;
    }

    float IProjectionProcessor.GetOffestRange()
    {
        return m_offestRange;
    }

    [SerializeField]
    private bool m_showWireframe = false;

    [SerializeField]
    private MeshResolution m_meshResolution = MeshResolution.Medium;
    [SerializeField]
    [Range(-100.0f, 100.0f)]
    private float m_planeHeight = 0.0f;
    [SerializeField]
    [Range(0.0f, 50.0f)]
    private float m_offestRange = 20.0f;

    [SerializeField]
    private Material m_projectedGridsMaterial = null;
    ProjectedGrids m_ProjectedGrid = new ProjectedGrids();

    private RenderObjectsPass m_drawWireFramePass = null;
    private RenderObjects.CustomCameraSettings m_customCameraSettings =
        new RenderObjects.CustomCameraSettings();
}