using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectedGrid
{
    public enum MeshResolution
    {
        Low,
        Medium,
        High,
        Ultra,
        Extreme
    };

    public interface IProjectedGrids : IProjectionProcessor
    {   
        MeshResolution GetMeshResolution();
        Material GetMaterial();
    };

    public class ProjectedGrids// : IProjectionProcessor
    {
        public bool ReInitialize(IProjectedGrids theInterface)
        {
            Release();
            m_interface = theInterface;
            m_ProjectionProcessor.TheProjectionProcessor = m_interface;

            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;

            return true;
        }
        public bool Release()
        {
            DestroyProjectGridMehsInfo(m_SceneViewGridMehsInfo);
            DestroyProjectGridMehsInfo(m_GameViewGridMehsInfo);

            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            m_ProjectionProcessor.TheProjectionProcessor = null;
            return true;
        }

        private void DestroyProjectGridMehsInfo(ProjectGridMehsInfo gridMeshInfo)
        {
            var meshLists = m_SceneViewGridMehsInfo.m_MeshLists;
            var listElement = meshLists.GetEnumerator();
            while (listElement.MoveNext())
            {
                var meshList = listElement.Current.Value;
                var meshElement = meshList.GetEnumerator();
                while (meshElement.MoveNext())
                    SafeDestroy(meshElement.Current);
                meshElement.Dispose();
                meshList.Clear();
            }
            listElement.Dispose();
            meshLists.Clear();
            gridMeshInfo.m_CameraResolution = new int2(-1, -1);
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context,
            Camera camera)
        {   
            if (m_interface == null)
                return;
            
            var targetMaterial = m_interface.GetMaterial();
            if (targetMaterial == null)
                return;

            var meshList = GetTargetMeshList(camera);
            if (meshList == null)
                return;
            
            m_ProjectionProcessor.UpdateProjection(camera, ref m_projectorVP, ref m_interpolation);
            Shader.SetGlobalMatrix("_ProjectedGridInterpolationMatrix", m_interpolation);
            Shader.SetGlobalMatrix("_ProjectedGridViewPortMatrix", m_projectorVP);

            Vector3 pos = camera.transform.position;
            float len = camera.farClipPlane * 2.0f;
            //pos.y = m_planeHeight;
            pos.y = m_interface.GetPlaneHeight();

            var element = meshList.GetEnumerator();
            while (element.MoveNext())
            {
                var mesh = element.Current;
                mesh.bounds = new Bounds(pos, new Vector3(len, 1.0f, len));                
                Graphics.DrawMesh(mesh, Matrix4x4.identity, targetMaterial, 0, camera);
            }
            element.Dispose();
        }

        private ProjectGridMehsInfo GetTargetGridMeshInfo(Camera camera)
        {
            switch (camera.cameraType)
            {
                case CameraType.Game:
                    return m_GameViewGridMehsInfo;
                case CameraType.SceneView:
                    return m_SceneViewGridMehsInfo;
                default:
                    return null;
            }
        }

        private List<Mesh> GetTargetMeshList(Camera camera)
        {
            if (m_interface == null)
                return null;

            var targetGridMehsInfo = GetTargetGridMeshInfo(camera);
            if (targetGridMehsInfo == null)
                return null;

            var cameraResolution = targetGridMehsInfo.m_CameraResolution;

            var cameraPixelWidth = Mathf.Min(camera.pixelWidth, c_MaxScreenWidth);
            var cameraPixelHeight = Mathf.Min(camera.pixelHeight, c_MaxScreenHeight);

            var meshResolution = m_interface.GetMeshResolution();

            var meshLists = targetGridMehsInfo.m_MeshLists;
            List<Mesh> meshList;
            if ((cameraResolution.x == cameraPixelWidth) &&
                (cameraResolution.y == cameraPixelHeight))
            {
                if (meshLists.TryGetValue(meshResolution, out meshList))
                    return meshList;
            }

            targetGridMehsInfo.m_CameraResolution =
                new int2(cameraPixelWidth, cameraPixelHeight);

            ref int2 targetCameraResolution = ref targetGridMehsInfo.m_CameraResolution;
            
            if (meshLists.TryGetValue(meshResolution, out meshList))
            {
                var element = meshList.GetEnumerator();
                while (element.MoveNext())
                    SafeDestroy(element.Current);
                element.Dispose();
                meshList.Clear();
            }
            else
            {
                meshList = new List<Mesh>();
                targetGridMehsInfo.m_MeshLists[meshResolution] = meshList;
            }

            int resolution = GetResolutionValue(meshResolution);
            int groups = ChooseGroupSize(resolution, m_gridGroups,
                targetCameraResolution.x, targetCameraResolution.y);


            CreateScreenGridMeshList(resolution, groups, targetCameraResolution.x,
                targetCameraResolution.y, meshList);

            return meshList;
        }

        private bool CreateScreenGridMeshList(int resolution, int groupSize,
            int width, int height, List<Mesh> meshList)
        {
            float w = 0.0f, h = 0.0f;
            int numVertsX = 0, numVertsY = 0, numX = 0, numY = 0;

            //Group size is sqrt of number of verts in mesh at resolution 1.
            //Work out how many meshes can fit in the screen at this size.
            if (groupSize != -1)
            {
                //Change size to be divisible by groups
                while (width % groupSize != 0) width++;
                while (height % groupSize != 0) height++;

                numVertsX = groupSize / resolution;
                numVertsY = groupSize / resolution;

                numX = width / groupSize;
                numY = height / groupSize;

                w = groupSize / width;
                h = groupSize / height;
            }
            else
            {
                numVertsX = width / resolution;
                numVertsY = height / resolution;
                numX = 1;
                numY = 1;
                w = 1.0f;
                h = 1.0f;
            }

            meshList.Clear();
            for (int x = 0; x < numX; x++)
            {
                for (int y = 0; y < numY; y++)
                {
                    float ux = x * w;
                    float uy = y * h;

                    Mesh gridMesh = CreateScreenGridMesh(numVertsX, numVertsY, ux, uy, w, h);
                    meshList.Add(gridMesh);
                }
            }
            return true;
        }

        public Mesh CreateScreenGridMesh(int numVertsX, int numVertsY, float ux, float uy, float w, float h)
        {
            Vector3[] vertices = new Vector3[numVertsX * numVertsY];
            Vector2[] texcoords = new Vector2[numVertsX * numVertsY];
            int[] indices = new int[numVertsX * numVertsY * 6];

            //Percentage of verts that will be in the border.
            //Only a small number is needed.
            float border = 0.1f;

            for (int x = 0; x < numVertsX; x++)
            {
                for (int y = 0; y < numVertsY; y++)
                {
                    Vector2 uv = new Vector2((float)x /  (float)(numVertsX - 1), (float)y / (float)(numVertsY - 1));

                    uv.x *= w;
                    uv.x += ux;

                    uv.y *= h;
                    uv.y += uy;

                    //Add border. Values outside of 0-1 are verts that will be in the border.
                    uv.x = uv.x * (1.0f + border * 2.0f) - border;
                    uv.y = uv.y * (1.0f + border * 2.0f) - border;

                    //The screen uv is used for the interpolation to calculate the
                    //world position from the interpolation matrix so must be in a 0-1 range.
                    Vector2 screenUV = uv;
                    screenUV.x = Mathf.Clamp01(screenUV.x);
                    screenUV.y = Mathf.Clamp01(screenUV.y);

                    //For the edge verts calculate the direction in screen space 
                    //and normalize. Only the directions length is needed but store the
                    //x and y direction because edge colors are output sometimes for debugging.
                    Vector2 edgeDirection = uv;

                    if (edgeDirection.x < 0.0f)
                        edgeDirection.x = Mathf.Abs(edgeDirection.x) / border;
                    else if (edgeDirection.x > 1.0f)
                        edgeDirection.x = Mathf.Max(0.0f, edgeDirection.x - 1.0f) / border;
                    else
                        edgeDirection.x = 0.0f;

                    if (edgeDirection.y < 0.0f)
                        edgeDirection.y = Mathf.Abs(edgeDirection.y) / border;
                    else if (edgeDirection.y > 1.0f)
                        edgeDirection.y = Mathf.Max(0.0f, edgeDirection.y - 1.0f) / border;
                    else
                        edgeDirection.y = 0.0f;

                    edgeDirection.x = Mathf.Pow(edgeDirection.x, 2);
                    edgeDirection.y = Mathf.Pow(edgeDirection.y, 2);

                    texcoords[x + y * numVertsX] = edgeDirection;
                    vertices[x + y * numVertsX] = new Vector3(screenUV.x, screenUV.y, 0.0f);
                }
            }

            int num = 0;
            for (int x = 0; x < numVertsX - 1; x++)
            {
                for (int y = 0; y < numVertsY - 1; y++)
                {
                    indices[num++] = x + y * numVertsX;
                    indices[num++] = x + (y + 1) * numVertsX;
                    indices[num++] = (x + 1) + y * numVertsX;

                    indices[num++] = x + (y + 1) * numVertsX;
                    indices[num++] = (x + 1) + (y + 1) * numVertsX;
                    indices[num++] = (x + 1) + y * numVertsX;
                }
            }

            Mesh mesh = new Mesh();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.uv = texcoords;
            mesh.triangles = indices;
            mesh.name = "Projected Grid Mesh";
            mesh.hideFlags = HideFlags.HideAndDontSave;
            
            return mesh;
        }

        private int GetResolutionValue(MeshResolution meshResolution)
        {
            switch (meshResolution)
            {
                case MeshResolution.Extreme:
                    return 1;

                case MeshResolution.Ultra:
                    return 2;

                case MeshResolution.High:
                    return 4;

                case MeshResolution.Medium:
                    return 8;

                case MeshResolution.Low:
                    return 16;

                default:
                    return 16;
            }
        }

        /// <summary>
        /// Chooses the number of verts that can be in each mesh given the mesh resolution. 
        /// </summary>
        private int ChooseGroupSize(int resolution, GridGroups groups, int width, int height)
        {
            int numVertsX = 0, numVertsY = 0;
            int groupSize = GroupToNumber(groups);

            if (groupSize == -1)
            {
                //If group size -1 try and create just a single mesh.
                numVertsX = width / resolution;
                numVertsY = height / resolution;
            }
            else
            {
                //Else work out how many verts will be in the group.
                numVertsX = groupSize / resolution;
                numVertsY = groupSize / resolution;
            }

            //If the number of verts is greater than Unitys max then will have to use a larger number of verts.
            while (numVertsX * numVertsY > 65000)
            {
                //This should never happen as the Extreme size should not be over max verts
                if (groups == GridGroups.Extreme)
                    break;// throw new InvalidOperationException("Can not increase group size");

                int nextSize = (int)groups + 1;

                //Ocean.LogWarning("Mesh resolution to high for group size. Trying next group size of " +
                //((GRID_GROUPS)nextSize));

                groups = (GridGroups)nextSize;

                groupSize = GroupToNumber(groups);

                numVertsX = groupSize / resolution;
                numVertsY = groupSize / resolution;
            }

            return groupSize;
        }

        /// <summary>
        /// Converts the group enum to a number.
        /// The group number is the sqrt of the number of verts in each mesh at resolution of 1.
        /// It will require less meshes to fill the screen the bigger they are.
        /// </summary>
        int GroupToNumber(GridGroups groups)
        {
            switch (groups)
            {

                case GridGroups.Extreme:
                    return 128;

                case GridGroups.High:
                    return 196;

                case GridGroups.Medium:
                    return 256;

                case GridGroups.Low:
                    return 512;

                case GridGroups.Single:
                    //special case. Will try and create just 1 mesh.
                    return -1;

                default:
                    return 128;
            }
        }

        public void SafeDestroy<T>(T obj, bool ignoreDataLoss = false) where T : Object
        {
            if (obj == null)
                return;
            if (Application.isEditor)
                Object.DestroyImmediate(obj, ignoreDataLoss);
            else
                Object.Destroy(obj);
            obj = null;
        }

        private const int c_MaxScreenWidth = 2048;
        private const int c_MaxScreenHeight = 2048;

        public class ProjectGridMehsInfo
        {
            public int2 m_CameraResolution = new int2(-1, -1);
            public Dictionary<MeshResolution, List<Mesh>> m_MeshLists =
                new Dictionary<MeshResolution, List<Mesh>>();
        };
        
        public enum GridGroups
        {
            Single,
            Low,
            Medium,
            High,
            Extreme
        };

        private GridGroups m_gridGroups = GridGroups.Single;

        /// <summary>
        /// Holds the meshes created for each resolution. 
        /// Allows the mesh to be saved when the resolution changes
        /// so it does not need to be created if changed back.
        /// </summary>
        private ProjectGridMehsInfo m_SceneViewGridMehsInfo = new ProjectGridMehsInfo();
        private ProjectGridMehsInfo m_GameViewGridMehsInfo = new ProjectGridMehsInfo();
        
        private ProjectionProcessor m_ProjectionProcessor = new ProjectionProcessor();

        private Matrix4x4 m_projectorVP = Matrix4x4.identity;
        private Matrix4x4 m_interpolation = Matrix4x4.identity;
        
        private IProjectedGrids m_interface = null;
    }
}