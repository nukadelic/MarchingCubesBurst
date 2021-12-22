
namespace MCBurst
{
    using Unity.Burst;
	using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
	using Unity.Mathematics;
	using UnityEngine;
    using UnityEngine.Rendering;

	[System.Serializable]
	public struct PolygoniserParams
	{
		[Range(0, 1)]
		public float isolevel;
		public bool invertVerticies;
		public int3 gridSize;

		public int length => gridSize.x * gridSize.y * gridSize.z;
	}

	public struct Triangle
	{
		public bool valid;
		public float3x3 verticies;
		public bool uvFlip;
	}

	public class PolygonParallelLists
    {
		public NativeList<Triangle> triangles;
		public NativeArray<VertexBuffer> polygons;
		public NativeArray<ushort> indicies;
		public NativeArray<int> counter;
		public CellParallel cell;

		public PolygonParallelLists Allocate( PolygoniserParams parameters )
		{
			var _allocator = Allocator.Persistent;

			// maximum of 5 triangles per grid cell ( triangle is float3x3 which holds 3 verticies ) 

			triangles = new NativeList<Triangle>( parameters.length * 5 , _allocator);

			polygons = new NativeArray<VertexBuffer>( parameters.length * 5 * 3 , _allocator );

			indicies = new NativeArray<ushort>( polygons.Length, _allocator );

			counter = new NativeArray<int>( 1 , _allocator );

			cell.Allocate( _allocator );

			return this;
		}

		public void Clear()
        {
			counter[ 0 ] = 0;
			triangles.Clear();
		}

		public void Dispose()
        {
			cell.Dispose();
			triangles.Dispose();
			polygons.Dispose();
			indicies.Dispose();
			counter.Dispose();
		}
    }

	public static class PolygoniserParallelM
	{
        public static JobHandle Schedule
		(
			PolygoniserParams parameters,
			ref NativeArray<float4> input,
			PolygonParallelLists lists,
			JobHandle dependency = default
		)
		{
			//if( parameters.length > ushort.MaxValue - 1 ) throw new System.Exception("Grid is too large");

			lists.Clear();

			var J1 = new PolygoniserParallel
			{
				parameters = parameters,
				input = input,
				output = lists.triangles.AsParallelWriter(),
				counter = lists.counter,
				cell = lists.cell,

#if PROFILE_MARKERS // -----------------------------------------------
				profilerMarker1 = new Unity.Profiling.ProfilerMarker("Marker 1"),
				profilerMarker2 = new Unity.Profiling.ProfilerMarker("Marker 2"),
				profilerMarker3 = new Unity.Profiling.ProfilerMarker("Marker 3"),
#endif // -----------------------------------------------

			};

			//var handle = J1.ScheduleBatch( parameters.length, 256, dependency );
            var handle = J1.Schedule( parameters.length, 256, dependency );

            var J2 = new PolygoniserArray
			{
				parameters	= parameters,
				input		= lists.triangles,
				outV		= lists.polygons,
				outI		= lists.indicies
			};

			handle = J2.Schedule( handle );

			return handle;
		}
		static readonly MeshUpdateFlags Flags = MeshUpdateFlags.DontValidateIndices |
			MeshUpdateFlags.DontNotifyMeshUsers |
            MeshUpdateFlags.DontRecalculateBounds |
            MeshUpdateFlags.DontResetBoneBounds;

		public static void WriteBuffer( PolygonParallelLists lists, ref Mesh mesh, int count )
        {
			if( mesh == null )
            {
				mesh = new Mesh();

				mesh.MarkDynamic();

				mesh.indexFormat = IndexFormat.UInt32;
			}

			mesh.Clear();

			mesh.SetVertexBufferParams( count, new[] 
			{ 
				new VertexAttributeDescriptor( VertexAttribute.Position, VertexAttributeFormat.Float32, 3 ) ,
				new VertexAttributeDescriptor( VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2 )
			});

			mesh.SetVertexBufferData( lists.polygons, 0, 0, count, 0, Flags );

			mesh.SetIndexBufferParams( count, IndexFormat.UInt16 );

			mesh.SetIndexBufferData( lists.indicies, 0, 0, count, Flags );

			mesh.SetSubMesh( 0, new SubMeshDescriptor( 0, count, MeshTopology.Triangles ) , Flags );

			mesh.RecalculateBounds();
			mesh.RecalculateNormals();
			mesh.RecalculateTangents();
        }

		public static void Write( PolygonParallelLists lists, ref Mesh mesh, int count )
        {
			mesh.Clear();

			Vector3[] verts = new Vector3[ count ];
			Vector2[] uvs = new Vector2[ count ];
			int[] triangles = new int[ count ];

			for( var i = 0 ; i < count ; ++ i )
            {
				uvs[ i ] = lists.polygons[ i ].uv;
				verts[ i ] = lists.polygons[ i ].vertex;
				triangles[ i ] = lists.indicies[ i ];

			}

			mesh.vertices = verts;
			mesh.uv = uvs;
			mesh.triangles = triangles;
			//mesh.triangles = lists.indicies.Slice( 0, count / 3 ).SliceConvert<int>().ToArray();
			
			mesh.RecalculateBounds();
			mesh.RecalculateNormals();
			mesh.RecalculateTangents();
		}
	}

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
	public unsafe struct PolygoniserParallel : IJobParallelFor // IJobParallelForBatch
	{
		[ReadOnly] public NativeArray<float4> input;

		public NativeList<Triangle>.ParallelWriter output;

		public PolygoniserParams parameters;

		public NativeArray<int> counter;

		public CellParallel cell;

		[NativeSetThreadIndex] public int thread_index;

#if PROFILE_MARKERS // -----------------------------------------------
		public Unity.Profiling.ProfilerMarker profilerMarker1;
		public Unity.Profiling.ProfilerMarker profilerMarker2;
		public Unity.Profiling.ProfilerMarker profilerMarker3;
#endif // -----------------------------------------------

		public void Execute(int startIndex, int count)
		{
			 int thread_i = thread_index - 1;
            
			// Debug.Log($"Thread Index {thread_i} , start {startIndex} , count {count}");

            for ( var i = startIndex; i < count; ++i )
            {
				ExecuteThreadIndex( i , thread_i );
			}
		}

		public void Execute(int i)
        {
			int thread_i = thread_index - 1;

			ExecuteThreadIndex( i , thread_i );
		}

		public void ExecuteThreadIndex(int i , int thread_i )
        {
			// find current cell index in 3d space 

			int3 index = Methods.Index1D3D(i, parameters.gridSize);

			// check if index is out of bounds ( input data is gridSize.xyz - 1 ) 

			if (index.x == parameters.gridSize.x - 1) return;
			if (index.y == parameters.gridSize.y - 1) return;
			if (index.z == parameters.gridSize.z - 1) return;

			// populate cell with 8 points from input data ( flatten 3d grid )

#if PROFILE_MARKERS // -----------------------------------------------
			profilerMarker1.Begin();
#endif // -----------------------------------------------

			SolverParallel.FillCellPoints(ref this, index, thread_i);


#if PROFILE_MARKERS // -----------------------------------------------
			profilerMarker1.End();
			profilerMarker2.Begin();
#endif // -----------------------------------------------

			// compute cell verticies ( cell.triangles & cell.count ) where each triangle is 3 points ( float3x3 )

			SolverParallel.Polygonise(ref cell, thread_i, parameters.isolevel);

#if PROFILE_MARKERS // -----------------------------------------------
			profilerMarker2.End();
			profilerMarker3.Begin();
#endif // -----------------------------------------------

			// populate output data 

			SolverParallel.WriteOutput(ref this, thread_i);

#if PROFILE_MARKERS // -----------------------------------------------
			profilerMarker3.End();
#endif // -----------------------------------------------
		}
	}

	// to be used, to get the data layout match exactly what it needs to be.
	[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
	public struct VertexBuffer
	{
		public float3 vertex;
		public float2 uv;
	}


	[BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
	public struct PolygoniserArray : IJob
	{
		public PolygoniserParams						parameters;

		[ReadOnly] public NativeList<Triangle>			input;
		public NativeArray<VertexBuffer>				outV;
		public NativeArray<ushort>						outI;

		public void Execute()
		{
			var input_length = input.Length;

			for ( var i = 0; i < input_length ; ++i )
            {
				var item = input[ i ];	
				
				if ( ! item.valid ) break;

				for (var t = 0; t < 3; ++t)
				{
					int uj = t + ( t == 0 ? 0 : t + ( item.uvFlip ? 2 : 0) ); // flip ? 0,4,6 : 0,2,4 

					var uv = new float2( Tables.UVOffsets[uj], Tables.UVOffsets[uj + 1] );

					int ti = ! parameters.invertVerticies ? 2 - t : t ; // invert ? 2,1,0 : 0,1,2

					outV[ i * 3 + t ] = new VertexBuffer { uv = uv , vertex = item.verticies[ ti ] };

					outI[ i * 3 + t ] = ( ushort ) ( i * 3 + t );

					//ti += 3 * (int) math.floor( i );
					//output[ i ++ ] = new VertexTriangleUV { vertex = item.verticies[ t ],triangle = ti,uv = uv };
				}
			}
		}
	}
}