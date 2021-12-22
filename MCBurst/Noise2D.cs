
namespace MCBurst
{
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Jobs;
    using Unity.Mathematics;

    #if UNITY_EDITOR
    using UnityEngine;
    #endif

    public static class Noise2D
    {
        public enum ClassEnum
        {
            CellularA,
            CellularB,
            SimplexNoise,
            PerlinNoise,
            PerlinNoisePeriodicVariant
        }
        public interface INoiseJob
        {
            void Config(ref NativeArray<float> data, ref Config config);
            JobHandle ScheduleJob( JobHandle handle );
        }

        [System.Serializable]
        public struct Config
        {
            public ClassEnum type;
            [HideInInspector]
            public int2 size;
            [Range(0.01f, 3f)]
            public float scale;
            public float2 offset;
            public float2 period;

            public int length => size.x * size.y;

            public Config Add( float2 offset, float scale, int2 size ) => new Config
            {
                type = this.type,
                size = size,
                scale = this.scale * scale,
                offset = this.offset + offset,
                period = this.period
            };

            public static Config Default() => new Config
            {
                type = ClassEnum.PerlinNoise,
                scale = 1f,
                offset = float2.zero,
                period = new float2( 1,1 ),
            };

            public float2 GetPosition2D(int i) => (float2)Methods.Index1D2D(i, size) * scale + offset;

            public float2 GetPeriod2D( int i ) => period;
        }

        public static class Operations
        {
            [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
            public struct ToByte : IJob
            {
                public NativeArray<float> input;
                public NativeArray<byte> output;
                public void Execute() 
                { 
                    for (var i = 0; i < input.Length; ++i)
                    {
                        var value = (byte)( input[ i ] * 255 );
                        output[ i * 3 + 0 ] = value; 
                        output[ i * 3 + 1 ] = value; 
                        output[ i * 3 + 2 ] = value; 
                    }
                }

            }

            [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
            public struct ToFloat3 : IJob
            {
                public NativeArray<float> input;
                public NativeArray<float3> output;
                public void Execute() { for (var i = 0; i < input.Length; ++i) output[i] = new float3(input[i]); }
            }

            [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
            public struct Zero : IJob
            {
                public NativeArray<float> data;
                public void Execute() { for (var i = 0; i < data.Length; ++i) data[i] = 0; }
            }

            [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
            public struct Divide : IJob
            {
                public NativeArray<float> data;
                public float value;
                public void Execute() { for (var i = 0; i < data.Length; ++i) data[i] /= value; }
            }

            [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
            public struct Normalize : IJob
            {
                public NativeArray<float> data;
                public NativeArray<float> minmax;
                public void Execute() { for (var i = 0; i < data.Length; ++i) data[i] = ( data[i] - minmax[0]) / ( minmax[1] - minmax[0] ); }
            }

            [BurstCompile]
            public struct Scan : IJob
            {
                public NativeArray<float> result;
                public NativeArray<float> data; 
                public void Execute() 
                { 
                    for (var i = 0; i < data.Length; ++i) 
                    {
                        var value = data[ i ];
                        if( value < result[0]) result[0] = value;
                        if( value > result[1]) result[1] = value;
                        result[2] += value;
                    }  
                }
            }
        }

        public static class SingleThreaded
        {
            public static System.Type[] classes =
            {
                typeof( CellularA ) ,
                typeof( CellularB ) ,
                typeof( SimplexNoise ) ,
                typeof( PerlinNoise ) ,
                typeof( PerlinNoisePeriodicVariant ) ,
            };


            [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
            public struct CellularA : INoiseJob, IJob
            {
                public Config config;
                public NativeArray<float> data;

                public void Config(ref NativeArray<float> d, ref Config c) { data = d; config = c; }

                public void Execute() { for( var i = 0; i < data.Length; ++i ) Execute( i ); }
                public void Execute(int i) => data[i] += math.clamp(noise.cellular(config.GetPosition2D(i))[0], 0, 1);
                public JobHandle ScheduleJob(JobHandle handle) => this.Schedule(handle);
            }

            [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
            public struct CellularB : INoiseJob, IJob
            {
                public Config config;
                public NativeArray<float> data;

                public void Config(ref NativeArray<float> d, ref Config c) { data = d; config = c; }
                public void Execute() { for (var i = 0; i < data.Length; ++i) Execute(i); }
                public void Execute(int i) => data[i] += math.clamp((noise.cellular(config.GetPosition2D(i))[1] - 0.1f) / 1.2f, 0, 1);
                public JobHandle ScheduleJob(JobHandle handle) => this.Schedule(handle);
            }

            [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
            public struct SimplexNoise : INoiseJob, IJob
            {
                public Config config;
                public NativeArray<float> data;

                public void Config(ref NativeArray<float> d, ref Config c) { data = d; config = c; }
                public void Execute() { for (var i = 0; i < data.Length; ++i) Execute(i); }
                public void Execute(int i) => data[i] += (noise.snoise(config.GetPosition2D(i)) + 1) / 2f;
                public JobHandle ScheduleJob(JobHandle handle) => this.Schedule(handle);
            }

            [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
            public struct PerlinNoise : INoiseJob, IJob
            {
                public Config config;
                public NativeArray<float> data;

                public void Config(ref NativeArray<float> d, ref Config c) { data = d; config = c; }
                public void Execute() { for (var i = 0; i < data.Length; ++i) Execute(i); }
                public void Execute(int i) => data[i] += (noise.cnoise(config.GetPosition2D(i)) + 1) / 2f;
                public JobHandle ScheduleJob(JobHandle handle) => this.Schedule(handle);
            }

            [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
            public struct PerlinNoisePeriodicVariant : INoiseJob, IJob
            {
                public Config config;
                public NativeArray<float> data;

                public void Config(ref NativeArray<float> d, ref Config c) { data = d; config = c; }
                public void Execute() { for (var i = 0; i < data.Length; ++i) Execute(i); }
                public void Execute(int i) => data[i] += noise.pnoise(config.GetPosition2D(i), config.GetPeriod2D(i));
                public JobHandle ScheduleJob(JobHandle handle) => this.Schedule(handle);
            }
        }


        public static class Parallel
        {
            public static int innerloopBatchCount = 1024;

            public static System.Type[] classes =
            {
                typeof( CellularA ) ,
                typeof( CellularB ) ,
                typeof( SimplexNoise ) ,
                typeof( PerlinNoise ) ,
                typeof( PerlinNoisePeriodicVariant ) ,
            };


            [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
            public struct CellularA : INoiseJob, IJobParallelFor
            {
                public Config config;
                public NativeArray<float> data;

                public void Config(ref NativeArray<float> d, ref Config c) { data = d; config = c; }
                public void Execute(int i) => data[i] += math.clamp( noise.cellular(config.GetPosition2D(i))[0] , 0,1 );
                public JobHandle ScheduleJob( JobHandle handle ) => this.Schedule( config.length , innerloopBatchCount, handle );
            }

            [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
            public struct CellularB : INoiseJob, IJobParallelFor
            {
                public Config config;
                public NativeArray<float> data;
                
                public void Config(ref NativeArray<float> d, ref Config c) { data = d; config = c; }
                public void Execute(int i) => data[i] += math.clamp(( noise.cellular( config.GetPosition2D( i ) ) [ 1 ] - 0.1f ) / 1.2f , 0,1 );
                public JobHandle ScheduleJob(JobHandle handle) => this.Schedule(config.length, innerloopBatchCount, handle);
            }

            [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
            public struct SimplexNoise : INoiseJob, IJobParallelFor
            {
                public Config config;
                public NativeArray<float> data;

                public void Config(ref NativeArray<float> d, ref Config c) { data = d; config = c; }
                public void Execute(int i) => data[i] += ( noise.snoise( config.GetPosition2D( i ) ) + 1 ) / 2f;
                public JobHandle ScheduleJob(JobHandle handle) => this.Schedule(config.length, innerloopBatchCount, handle);
            }

            [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
            public struct PerlinNoise : INoiseJob, IJobParallelFor
            {
                public Config config;
                public NativeArray<float> data;

                public void Config(ref NativeArray<float> d, ref Config c) { data = d; config = c; }
                public void Execute(int i) => data[i] += ( noise.cnoise(config.GetPosition2D(i)) + 1 ) / 2f;
                public JobHandle ScheduleJob(JobHandle handle) => this.Schedule(config.length, innerloopBatchCount, handle);
            }

            [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
            public struct PerlinNoisePeriodicVariant : INoiseJob, IJobParallelFor
            {
                public Config config;
                public NativeArray<float> data;

                public void Config(ref NativeArray<float> d, ref Config c) { data = d; config = c; }
                public void Execute(int i) => data[i] += noise.pnoise(config.GetPosition2D(i), config.GetPeriod2D(i));
                public JobHandle ScheduleJob(JobHandle handle) => this.Schedule(config.length, innerloopBatchCount, handle);
            }

        }
    }
}