
namespace MCBurst
{
    using Unity.Collections;
    using Unity.Mathematics;
    using System.Threading;
    using Unity.Collections.LowLevel.Unsafe;

    public static unsafe class SolverParallel
    {
        // Multi threaded solver version , comments are only relevant to parallel execution , see Solver.cs for more info 

        public static void Polygonise(ref CellParallel cell, int thread_index, float isolevel)
        {
            cell.SetCount( thread_index , 0 );

            int configurationIndex = 0;

            for (var i = 0; i < 8; ++i)
            {
                 //UnityEngine.Debug.Log($"Cell[{thread_index},{i}] = " + cell.GetPoint( thread_index, i ) );

                if (cell.GetPoint(thread_index, i).w < isolevel)
                    configurationIndex |= 1 << i;

            }

            var edge = Tables.edge[configurationIndex];

            if (edge == 0) return;

            for (var i = 0; i < 12; ++i)
            {
                if ((edge & (1 << i)) == 0) cell.SetVert(thread_index, i, float3.zero);

                else
                {
                    var _i1 = Tables.edgeToVertex[i * 2 + 0];
                    var _i2 = Tables.edgeToVertex[i * 2 + 1];

                    var _p1 = cell.GetPoint(thread_index, _i1);
                    var _p2 = cell.GetPoint(thread_index, _i2);

                    var v = Solver.VertexInterp(isolevel, _p1, _p2);

                    cell.SetVert(thread_index, i, v);
                }
            }

            int c = 0;

            // UnityEngine.Debug.Log( "First Config Index[" + configurationIndex + "] : " + Tables.triangle[ 16 * configurationIndex + c] );

            while (Tables.triangle[16 * configurationIndex + c] != -1)
            {
                var _count = cell.GetCount(thread_index);

                var _triangle = cell.GetTriangle(thread_index, _count);

                for (var i = 0; i < 3; ++i)
                {
                    int ii = 16 * configurationIndex + c + i;

                    var _ti = Tables.triangle[ii];

                    float3 _vertex = cell.GetVert(thread_index, _ti);

                    Solver.SetFloat3x3Row(ref _triangle, i, _vertex );
                }

                cell.SetTriangle(thread_index, _count, _triangle);

                cell.SetCount(thread_index, _count + 1);

                c += 3;
            }
        }

        public static void FillCellPoints( ref PolygoniserParallel job, int3 index, int thread_index)
        {
            for (var c = 0; c < 8; ++c)
            {
                var offset = new int3
                (
                    Tables.CellOffsets[c * 3 + 0], 
                    Tables.CellOffsets[c * 3 + 1], 
                    Tables.CellOffsets[c * 3 + 2]
                );

                var flat_index = Methods.Index3D1D( index + offset, job.parameters.gridSize );

                 //UnityEngine.Debug.Log("Set cell " + thread_index + " , " + c + " = " + job.input[ flat_index ] );

                job.cell.SetPoint(thread_index, c, job.input[ flat_index ] );

                // UnityEngine.Debug.Log("Get cell = " + job.cell.GetPoint( thread_index, c ) );
            }
        }

        public static void WriteOutput( ref PolygoniserParallel job, int thread_index )
        {
            bool flip_uvs = true;

            var _count = job.cell.GetCount( thread_index );
            
            Interlocked.Add( ref ( ( int* ) job.counter.GetUnsafePtr() ) [ 0 ] , 3 * _count );

            for (var t = 0; t < _count; ++t)
            {
                var _tirangle = job.cell.GetTriangle( thread_index, t );

                var data = new Triangle { verticies = _tirangle , uvFlip = flip_uvs = !flip_uvs , valid = true };

                job.output.AddNoResize( data );    
            }
        }
    }

    public struct CellParallel
    {
        [NativeDisableParallelForRestriction] public NativeArray<float4> points;
        [NativeDisableParallelForRestriction] public NativeArray<float3> vertlist;
        [NativeDisableParallelForRestriction] public NativeArray<float3x3> triangles;
        [NativeDisableParallelForRestriction] public NativeArray<int> count;

        // Each thread can only access its local memory region shifted by the value of thread_index 

        public int GetCount(int thread_index) => count[thread_index];
        public void SetCount(int thread_index, int value) => count[thread_index] = value;
        public float4 GetPoint(int thread_index, int index) => points[thread_index * Cell.points_count + index];
        public void SetPoint(int thread_index, int index, float4 value) => points[thread_index * Cell.points_count + index] = value;
        public float3 GetVert(int thread_index, int index) => vertlist[thread_index * Cell.vertlist_count + index];
        public void SetVert(int thread_index, int index, float3 value) => vertlist[thread_index * Cell.vertlist_count + index] = value;
        public float3x3 GetTriangle(int thread_index, int index) => triangles[thread_index * Cell.triangles_count + index];
        public void SetTriangle(int thread_index, int index, float3x3 value) => triangles[thread_index * Cell.triangles_count + index] = value;

        public void Allocate(Allocator alloc = (Allocator)4)
        {
            var c = System.Environment.ProcessorCount;

            // allocate memory (x) the processor count 

            count = new NativeArray<int>( c , alloc);
            points = new NativeArray<float4>(Cell.points_count * c, alloc);
            vertlist = new NativeArray<float3>(Cell.vertlist_count * c, alloc);
            triangles = new NativeArray<float3x3>(Cell.triangles_count * c, alloc);
        }
        public void Dispose()
        {
            count.Dispose();
            points.Dispose();
            vertlist.Dispose();
            triangles.Dispose();
        }
    }
}