namespace MCBurst
{
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Jobs;
    using Unity.Mathematics;

    public static class NoiseBuilder
    {




        [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
        public struct FlatPerlinNoise : IJobParallelFor
        {
            public int2 size;
            public int2 offset;
            public int octave;
            public float scale;

            [WriteOnly] public NativeArray<float> values;

            public void Execute( int i )
            {
                int2 index = Methods.Index1D2D( i , size );

                var value = Perlin.Fbm( ( offset + (float2) index ) * scale , octave );

                //noise.cellular(  ) 

                //var value = Perlin.Noise( ( offset + (float2) index ) * scale ) + 0.5f;

                values[ i ] = value;
            }
        }


        [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
        public struct FillGridHeightJob : IJobParallelFor
        {
            public float2 positionOffset;
            public int3 gridSize;
            public float positionScale;
            public float noiseScale;
            public float2 remap;

            [WriteOnly] public NativeArray<float4> data;

            public int length => gridSize.x * gridSize.y * gridSize.z;

            public void Execute( int i )
            {
                var index = Methods.Index1D3D( i , gridSize );

                var normalized = index / ( float3 ) gridSize;

                var position = positionScale * normalized;

                var loc = noiseScale * ( normalized.xz + positionOffset );

                //var value = UnityEngine.Mathf.PerlinNoise( loc.x, loc.y ) * normalized.y;

                var value = ( Perlin.Noise( loc ) + 1 ) / 2f * normalized.y;

                value = math.remap( 0, 1, remap.x, remap.y, value );

                //UnityEngine.Debug.Log( value );

                data[ i ] = new float4( position, math.clamp( value , 0, 1 ) );
            }
        }


    }
}