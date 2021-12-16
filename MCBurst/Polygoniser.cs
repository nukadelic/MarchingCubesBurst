
namespace MCBurst
{
	using Unity.Burst;
	using Unity.Collections;
	using Unity.Jobs;
	using Unity.Mathematics;
	using UnityEngine;
	public static class PolygoniserM
	{
		public static void Allocate(ref Polygoniser self, Allocator alloc = (Allocator)3)
		{
			self.cell.Allocate(alloc);
            self.UVs = new NativeList<float2>(alloc);
			self.Vertices = new NativeList<float3>(alloc);
			self.Triangles = new NativeList<int>(alloc);

			//#if PROFILE_MARKERS
			//self.profilerMarker1 = new Unity.Profiling.ProfilerMarker("Marker 1");
			//self.profilerMarker2 = new Unity.Profiling.ProfilerMarker("Marker 2");
			//self.profilerMarker3 = new Unity.Profiling.ProfilerMarker("Marker 3");
			//#endif
		}

		static public void Write(Polygoniser self, ref Mesh m)
		{
			m.Clear();
			/// m.vertices = self.Vertices.CastArray<float3,Vector3>( );
			m.vertices =	self.Vertices		.AsArray().Reinterpret<Vector3>().ToArray();
			m.uv =			self.UVs			.AsArray().Reinterpret<Vector2>().ToArray();
			m.triangles =	self.Triangles		.ToArray();

			m.RecalculateBounds();
			m.RecalculateNormals();
			m.RecalculateTangents();

			//if ( filter.TryGetComponent( out MeshCollider col) ) col.sharedMesh = m;
		}
		static public void Dispose( this Polygoniser self )
		{
			self.cell.Dispose();
            self.UVs.Dispose();
			self.Vertices.Dispose();
			self.Triangles.Dispose();
		}

	}

	[BurstCompile( FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true )]
	public struct Polygoniser : IJob
	{
        [NativeDisableParallelForRestriction] public NativeList<float2>	UVs;
		[NativeDisableParallelForRestriction] public NativeList<float3>	Vertices;
		[NativeDisableParallelForRestriction] public NativeList<int>	Triangles;

        public Cell cell;

        [ReadOnly] public NativeArray<float4> pointsFlatten;

		public bool invertVerticies;
		public int3 gridSize;
		public float isolevel;

		public int length => gridSize.x * gridSize.y * gridSize.z;

  //      #if PROFILE_MARKERS 
		//public Unity.Profiling.ProfilerMarker profilerMarker1;
		//public Unity.Profiling.ProfilerMarker profilerMarker2;
		//public Unity.Profiling.ProfilerMarker profilerMarker3;
		//#endif

		public void Execute()
		{
			//var cell = new Cell().Allocate( Allocator.Temp );

			for (var i = 0; i < length; ++i)
            {
				// find current cell index in 3d space 

				//#if PROFILE_MARKERS
				//profilerMarker1.Begin();
				//#endif

				int3 index = Methods.Index1D3D(i, gridSize);

				if (index.x == gridSize.x - 1) continue; 
				if (index.y == gridSize.y - 1) continue; 
				if (index.z == gridSize.z - 1) continue; 

				for (var c = 0; c < 8; ++c)
				{
					var offset = new int3(
						Tables.CellOffsets[c * 3 + 0],
						Tables.CellOffsets[c * 3 + 1],
						Tables.CellOffsets[c * 3 + 2]
					);

					// get neighbouring cell index in 3d space 

					var cell_index = index + offset;

					var flat_index = Methods.Index3D1D(cell_index, gridSize);

					cell.points[ c ] = pointsFlatten[ flat_index ];
				}

				//#if PROFILE_MARKERS
				//profilerMarker1.End();
				//#endif
				
				//#if PROFILE_MARKERS
				//profilerMarker2.Begin();
				//#endif

				Solver.Polygonise(ref cell, isolevel);
				
				//#if PROFILE_MARKERS
				//profilerMarker2.End();
				//#endif
				
				//#if PROFILE_MARKERS
				//profilerMarker3.Begin();
				//#endif

				bool flip_uvs = false; // TODO: check if invertVerticies can be done by setting this initial value 

				for (var t = 0; t < cell.count; ++t)
				{
					var triangle = cell.triangles[t];

					for (var ti = 0; ti < 3; ++ti)
					{
						Vertices.Add(triangle[ti]);
					}

					int vertex_count = Vertices.Length;

					for (var ti = 0; ti < 3; ++ti)
					{
						int vi = !invertVerticies ? ti + 1 : 3 - ti;

						Triangles.Add(vertex_count - vi);
					}

					//int f = flip_uvs ? 1 : 0 ;
					//UVs.Add( TableData.UVOffsets.Data[ 0     ] );
					//UVs.Add( TableData.UVOffsets.Data[ 1 + f ] );
					//UVs.Add( TableData.UVOffsets.Data[ 2 + f ] );

					for (var ui = 0; ui < 3; ++ui)
					{
						int uj = ui + (ui == 0 ? 0 : ui + (flip_uvs ? 1 : 0));

						var uv = new float2(Tables.UVOffsets[ uj ], Tables.UVOffsets[ uj +	1 ]);

						UVs.Add(uv);
					}

					flip_uvs = !flip_uvs;
				}

				//#if PROFILE_MARKERS
				//profilerMarker3.End();
				//#endif
			}

			//cell.Dispose();

		}
	}
}