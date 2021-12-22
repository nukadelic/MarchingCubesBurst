using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.Mathematics;
using MCBurst;
using Unity.Collections;
using Unity.Jobs;

public class NoiseScene : MonoBehaviour
{
    [Header("Info")]
    public string info = "";
    float info_t = 0;

    [Header("Global")]

    public int2 size = new int2( 512, 512 );
    public int2 offset = int2.zero;
    [Range(0.01f,3f)]
    public float scale = 1f;

    [Header("Stack")]
    public bool multiThreaded = false;
    public Noise2D.Config[] stack = { Noise2D.Config.Default() };

    Texture2D texture;

    NativeArray<float> values;
    NativeArray<byte> valuesBytes;

    JobHandle handle;
    bool scheduled = false;

    void Start()
    {
        texture = new Texture2D( size.x, size.y, TextureFormat.RGB24, false, false );
        texture.filterMode = FilterMode.Point;

        values = new NativeArray<float>( size.x * size.y, Allocator.Persistent );
        valuesBytes = new NativeArray<byte>( size.x * size.y * 3 , Allocator.Persistent );
    }

    void Update()
    {
        if( scheduled )
        {
            if( handle.IsCompleted )
            {
                handle.Complete();

                scheduled = false;

                texture.SetPixelData(valuesBytes, 0);

                texture.Apply();

                var t = Time.timeSinceLevelLoad - info_t;

                info = ( t * 1000 ).ToString("N1") + " ms";
            }
            else
            {
                return;
            }
        }

        if( stack.Length < 1 ) return;

        if( texture.width != size.x || texture.height != size.y )
        {
            texture.Resize( size.x, size.y );

            values.Dispose();
            values = new NativeArray<float>(size.x * size.y, Allocator.Persistent);

            valuesBytes.Dispose();
            valuesBytes = new NativeArray<byte>(size.x * size.y * 3 , Allocator.Persistent);
        }

        handle = default;

        handle = new Noise2D.Operations.Zero { data = values }.Schedule( handle );

        //Noise2D.Operations.Scan[] scans = new Noise2D.Operations.Scan[ stack.Length ];

        for (var i = 0; i < stack.Length; ++i )
        {
            var config = stack[ i ].Add( offset, scale, size );

            var NoiseType = multiThreaded ? 
                Noise2D.Parallel.classes[ (int) config.type ] : 
                Noise2D.SingleThreaded.classes[ (int) config.type ] ;

            var jobConfig = ( Noise2D.INoiseJob ) System.Activator.CreateInstance( NoiseType );

            jobConfig.Config( ref values, ref config );

            handle = jobConfig.ScheduleJob( handle );

            //{
            //    var scan = new Noise2D.Operations.Scan { data = values, 
            //        result = new NativeArray<float>( 
            //            new float[] { float.MaxValue, float.MinValue, 0 }, 
            //            Allocator.TempJob ) };
            //    scans[ i ] = scan;
            //    handle = scan.Schedule( handle );
            //}
        }

        //for (var i = 0; i < scans.Length; ++i)
        //{
        //    Debug.Log(i + ". min:" + scans[i].result[0].ToString("N3") +
        //        " max:" + scans[i].result[1].ToString("N3") +
        //        " sum:" + scans[i].result[2].ToString("N3"));
        //}


        //if ( stack.Length > 1 ) 
        //    handle = new Noise2D.Operations.Divide { data = values, value = stack.Length }.Schedule( handle );
        
        if( stack.Length > 1 )
        {
            var scan = new Noise2D.Operations.Scan { data = values,
                result = new NativeArray<float>(
                    new float[] { float.MaxValue, float.MinValue, 0 }, 
                    Allocator.TempJob)
            };

            handle = scan.Schedule( handle );

            handle = new Noise2D.Operations.Normalize 
                { data = values, minmax = scan.result }
                .Schedule( handle );
        }

        handle = new Noise2D.Operations.ToByte { input = values , output = valuesBytes }.Schedule( handle );

        scheduled = true;

        info_t = Time.timeSinceLevelLoad;

        //handle.Complete();

        //var noise = new NoiseBuilder.FlatPerlinNoise
        //{
        //    size = size,
        //    offset = offset,
        //    scale = scale,
        //    octave = octave,
        //    values = values
        //};

        //noise.Schedule( size.x * size.y, 1024 ).Complete();

        //for (var x = 0; x < size.x; ++x)
        //{
        //    for (var y = 0; y < size.y; ++y)
        //    {
        //        /// < cnoise output is ranged between -1 and +1 , normalize to [0,1] >
        //        var v = ( noise.cnoise(new float2(x, y) * scale * 0.01f) + 1 ) / 2;
        //        //greyscale[x + y * size.x] = new float3((value + 1) / 2);
        //        texture.SetPixel( x, y, new Color( v,v,v ) );
        //    }
        //}
    }
    private void OnGUI()
    {
        GUI.DrawTexture( new Rect( Vector2.zero, (Vector2) (float2) size ) , texture );
    }
}
