using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace UnchartedLimbo.Utilities.Meshing
{
    /// <summary>
    /// Contains Parallel Jobs that facilitate the fast generation of meshes.
    /// </summary>
    public static class MeshJobs
    {
        [BurstCompile]
        public struct GridVerticesJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction]
            public NativeArray<float>  depth;
            public NativeArray<float3> vertices;
            public int                 gridResolution_X;
            public float2              xyMultiplier;
            public float               depthMultiplier;

            public void Execute(int index)
            {
                var ix = index % gridResolution_X;
                var iy = index / gridResolution_X;

                // In Barracuda 1.0.4 the output of MiDaS can be passed  directly to a texture as it is shaped correctly.
                // In later versions we have to swap X and Y axes, and also flip the X axis...
                // Therefore we need to change the depth sampling coordinates.
                #if _CHANNEL_SWAP
                    var nx     = (gridResolution_X-1) - iy;
                    var ny     = ix;
                    var nindex = nx + ny * gridResolution_X;
                    var z  = depth[nindex] * depthMultiplier;
                #else
                    var z = depth[index] * depthMultiplier;
                #endif
                    
                var x  = ix           * xyMultiplier.x;
                var y  = iy           * xyMultiplier.y;

                vertices[index] = new float3(x, y, z);
            }
        }

        [BurstCompile]
        public struct GridIndicesJob : IJobParallelFor
        {
            public NativeArray<int> indices;
            public int              width;

            public void Execute(int index)
            {
                var x = (index / 4) % (width - 1);
                var y = (index / 4) / (width - 1);

                var vertexIndex = x + y * width;
                var cornerIndex = index % 4;

                switch (cornerIndex)
                {
                    case 0:
                        indices[index] = vertexIndex;
                        break;
                    case 1:
                        indices[index] = vertexIndex + 1;
                        break;
                    case 2:
                        indices[index] = vertexIndex + 1 + width;
                        break;
                    case 3:
                        indices[index] = vertexIndex + width;
                        break;
                    default:
                        indices[index] = vertexIndex;
                        break;
                }
            }
        }

        [BurstCompile]
        public struct GridUVJob : IJobParallelFor
        {
            public NativeArray<float2> uv;
            public int                 width;

            public void Execute(int index)
            {
                var x = index % width;
                var y = index / width;
                uv[index] = new float2(x, y) / new float2(width, uv.Length / (float) width);
            }
        }
    }
}