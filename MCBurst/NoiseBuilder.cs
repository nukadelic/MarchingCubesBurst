namespace MCBurst
{
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Jobs;
    using Unity.Mathematics;

    public static class NoiseBuilder
    {
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

                var value = Perlin.Noise( noiseScale * ( normalized.xz + positionOffset ) ) * normalized.y ;

                value = math.remap( 0, 1, remap.x, remap.y, value );

                data[ i ] = new float4( position, value );
            }
        }


    }
}