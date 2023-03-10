using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using static UnchartedLimbo.Utilities.Meshing.MeshJobs;


namespace UnchartedLimbo.NN.Depth
{
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public class DepthMesher : MonoBehaviour
    {
        public enum DisplacementMethod
        {
            Mesh,
            Shader
        }

        public DisplacementMethod method;

        // --------------------------------------------------------------------

        [Header("Meshing")]
        [Range(0, 1)]
        public float depthMultiplier = 0.01f;

        [Range(0, 1)]
        public float imageScale = 0.1f;

        // --------------------------------------------------------------------

        [Header("Depth Visualization")]
        public Material mat;

        public Texture colorTexture;
        public bool    useColorTexture;

        [Range(0, 10000)]
        public float minDepth = 0;

        [Range(0, 10000)]
        public float maxDepth = 1000;

        [Range(0, 2)]
        public float LogNormalizationFactor = 1;

        // --------------------------------------------------------------------

        // Helper values
        private int                _width, _height;
        private DisplacementMethod _previousMethod;
        private float              _previousRatio;

        // Object references
        private MeshFilter _mf;
        private Mesh       _mesh;
        private Texture2D  _t2d;

        // Native Arrays
        private NativeArray<float3> _vertices;
        private NativeArray<int>    _indices;
        private NativeArray<float2> _uv;
        private NativeArray<float>  _depths;
        
        // --------------------------------------------------------------------

        // Shader Properties
        private static readonly int Min             = Shader.PropertyToID("_Min");
        private static readonly int Max             = Shader.PropertyToID("_Max");
        private static readonly int MainTex         = Shader.PropertyToID("_MainTex");
        private static readonly int Displace        = Shader.PropertyToID("_Displace");
        private static readonly int DepthMultiplier = Shader.PropertyToID("_DepthMultiplier");
        private static readonly int LogNorm         = Shader.PropertyToID("_LogNorm");
        private static readonly int ColorIsDepth    = Shader.PropertyToID("_ColorIsDepth");
        private static readonly int DepthTex        = Shader.PropertyToID("_DepthTex");

        // --------------------------------------------------------------------
        // MONOBEHAVIOUR
        // --------------------------------------------------------------------

        private void Start()
        {
            _mf            = GetComponent<MeshFilter>();
            _mesh          = new Mesh {indexFormat = IndexFormat.UInt32};
            _mf.sharedMesh = _mesh;
        }

        private void Update()
        {
            mat.SetFloat(Min,             minDepth);
            mat.SetFloat(Max,             maxDepth);
            mat.SetFloat(DepthMultiplier, depthMultiplier);
            mat.SetFloat(LogNorm,         LogNormalizationFactor);
            mat.SetInt(ColorIsDepth, colorTexture == null || !useColorTexture ? 1 : 0);
            mat.SetInt(Displace,     method == DisplacementMethod.Mesh ? 0 : 1);
        
            // In Barracuda 1.0.4 the output of MiDaS can be passed  directly to a texture as it is shaped correctly.
            // In later versions we have to swap X and Y axes, and also flip the X axis...
            // We need to inform the shader of this change.
            #if _CHANNEL_SWAP
                mat.SetInt("_SwapChannels",1);
            #else
                mat.SetInt("_SwapChannels",0);
            #endif

        }

        private void OnDestroy()
        {
            DeallocateArrays();

            Destroy(_mesh);
            _mesh = null;

            Destroy(_t2d);
            _t2d = null;
        }


        // --------------------------------------------------------------------
        // MEMORY MANAGEMENT
        // --------------------------------------------------------------------

        private void AllocateArrays(int width, int height)
        {
            DeallocateArrays();

            _vertices = new NativeArray<float3>(width    * height, Allocator.Persistent);
            _indices  = new NativeArray<int>((width - 1) * (height - 1) * 4, Allocator.Persistent);
            _uv       = new NativeArray<float2>(_vertices.Length, Allocator.Persistent);
        }

        private void DeallocateArrays()
        {
            if (_vertices.IsCreated) _vertices.Dispose();
            if (_indices.IsCreated) _indices.Dispose();
            if (_uv.IsCreated) _uv.Dispose();
        }


        // --------------------------------------------------------------------
        // PUBLIC INTERFACES
        // --------------------------------------------------------------------

        /// <summary>
        /// Height / Width ratio of the source footage. Used to scale the mesh properly
        /// </summary>
        public float Ratio { get; set; }

        /// <summary>
        /// Executes when a new texture is received
        /// </summary>
        public void OnDepthReceived(RenderTexture rt)
        {
            if (rt.width * rt.height == 0) return;

            mat.SetTexture(MainTex,  colorTexture == null || !useColorTexture ? rt : colorTexture);
            mat.SetTexture(DepthTex, rt);

            // Vertex Displacement on the CPU - Mesh gets updated at every frame
            if (method == DisplacementMethod.Mesh)
            {
                _mesh.MarkDynamic();

                var jobs = new NativeList<JobHandle>(Allocator.Temp);

                if (_width != rt.width || _height != rt.height)
                {
                    _width  = rt.width;
                    _height = rt.height;

                    AllocateArrays(rt.width, rt.height);

                    jobs.Add(UpdateIndexBuffer());
                    jobs.Add(UpdateUVBuffer());
                }
              
                jobs.Add(UpdateVertexBuffer(ReadTextureAsync(rt)));
               
                JobHandle.CompleteAll(jobs);
             
                UpdateMesh(true);
            }
            // Vertex Displacement on the GPU - Mesh only needs to be created once
            else
            {
                // Generate mesh only if necessary
                if (_width                           != rt.width || _height != rt.height || _previousMethod != method ||
                    Math.Abs(_previousRatio - Ratio) > 0.001F)
                {
                    _width  = rt.width;
                    _height = rt.height;

                    var tempDepth = new NativeArray<float>(rt.width * rt.height, Allocator.TempJob);
                    var jobs      = new NativeList<JobHandle>(Allocator.Temp);

                    // Flat plane
                    AllocateArrays(rt.width, rt.height);

                    jobs.Add(UpdateVertexBuffer(tempDepth));
                    jobs.Add(UpdateIndexBuffer());
                    jobs.Add(UpdateUVBuffer());
                    JobHandle.CompleteAll(jobs);

                    UpdateMesh(true);

                    tempDepth.Dispose();
                }
            }

            _previousMethod = method;
            _previousRatio  = Ratio;
        }

        /// <summary>
        /// Updates the color texture
        /// </summary>
        public void OnColorReceived(RenderTexture rt)
        {
            colorTexture = rt;
        }
        
        /// <summary>
        /// Updates the Min and Max assumed depth that is represented by the Depth texture
        /// </summary>
        /// <param name="depthExtents"></param>
        public void OnDepthExtentsReceived(Vector2 depthExtents)
        {
            minDepth = depthExtents.x;
            maxDepth = depthExtents.y;
        }
        
        // --------------------------------------------------------------------
        // MESH UPDATES
        // --------------------------------------------------------------------

        /// <summary>
        /// Update mesh vertices using depth data (JobHandle needs to be completed by the caller)
        /// </summary>
        private JobHandle UpdateVertexBuffer(NativeArray<float> depthData)
        {
            return new GridVerticesJob
            {
                    vertices         = _vertices,
                    depth            = depthData,
                    gridResolution_X = _width,
                    xyMultiplier     = new float2(imageScale, imageScale * Ratio),
                    depthMultiplier  = depthMultiplier
            }.Schedule(_width * _height, 128);
        }

        /// <summary>
        /// Update mesh indices (JobHandle needs to be completed by the caller)
        /// </summary>
        private JobHandle UpdateIndexBuffer()
        {
            return new GridIndicesJob
            {
                    indices = _indices,
                    width   = _width
            }.Schedule(_indices.Length, 64);
        }

        /// <summary>
        /// Update mesh UVs  (JobHandle needs to be completed by the caller)
        /// </summary>
        private JobHandle UpdateUVBuffer()
        {
            return new GridUVJob
            {
                    uv    = _uv,
                    width = _width
            }.Schedule(_uv.Length, 64);
        }

        /// <summary>
        /// Update Mesh
        /// </summary>
        private void UpdateMesh(bool topologyChanged = false)
        {
            if (topologyChanged)
            {
                _mesh.Clear();
                _mesh.SetVertices(_vertices);
                _mesh.SetIndices(_indices, MeshTopology.Quads, 0);
                _mesh.SetUVs(0, _uv);
            }
            else
            {
                _mesh.SetVertices(_vertices);
            }

            //_mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
        }


        // --------------------------------------------------------------------
        // TEXTURE IO
        // --------------------------------------------------------------------
        /// <summary>
        ///  Read a RenderTexture back to CPU
        /// </summary>
        private NativeArray<float> ReadTextureAsync(RenderTexture rt, bool safe = false)
        {
            // Create or resize texture2D
            if (_t2d == null)
            {
                _t2d = new Texture2D(rt.width, rt.height, rt.graphicsFormat, TextureCreationFlags.None);
            }
            else if (_t2d.width != rt.width || _t2d.height != rt.height)
            {
                _t2d.Reinitialize(rt.width, rt.height);
            }
            
            // Asynchronously read the data from the GPU
            // No check whether the data has been fully received
            // Quite possible data mixups
            var req = AsyncGPUReadback.Request(rt, 0, asyncAction =>
            {
                if (_t2d == null) return;
                _t2d.SetPixelData(asyncAction.GetData<byte>(), 0);
                _t2d.Apply();
             
            });

            if (safe)
                req.WaitForCompletion();
            
            return _t2d.GetRawTextureData<float>();
        }
    }
}
