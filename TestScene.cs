
using Unity.Mathematics;
using Unity.Jobs;
using UnityEngine;
using MCBurst;
using Unity.Collections;

[RequireComponent(typeof(MeshFilter))]
public class TestScene : MonoBehaviour
{
                        public bool         parallelMC = false;
                        public bool         writeBuffer = true;
                        public int3         gridSize = new int3( 50, 10 , 30 );

    [Range(0.1f,10)]    public float        positionScale = 1;
                        public Vector2      noiseOffset = Vector2.zero;
    [Range(0,1)]        public float        isoLevel = 0.5f;
    [Range(0.1f,8)]     public float        noiseScale = 1f;
                        public bool         inverted = false;
                        public Vector2      heightRemap = new Vector2( 0, 1 );


    [Header("Debug preview values in inspector")]
    public float    execTime = 0;
    public int      execFrames = 0;
    public string   execInfo = "";

    int frames = 0;
    float timer = 0;
    bool scheduled = false;
    JobHandle handle;
    bool activeParallelMC;

    NoiseBuilder.FillGridHeightJob job_noise;
    Polygoniser job_polygon;

    MeshFilter filter;

    PolygonParallelLists lists;

    NativeArray<float4> noiseData;

    private void Start()
    {
        filter = GetComponent<MeshFilter>();
    }

    private void LateUpdate()
    {
        if ( scheduled ) return;

        job_noise = new NoiseBuilder.FillGridHeightJob
        {
            gridSize = gridSize,
            positionOffset = noiseOffset,
            positionScale = positionScale,
            noiseScale = noiseScale,
            remap = heightRemap
        };

        if(! noiseData.IsCreated ) noiseData = new NativeArray<float4>( job_noise.length, Allocator.Persistent );
        else if( noiseData.Length != job_noise.length )
        {
            noiseData.Dispose();
            noiseData = new NativeArray<float4>(job_noise.length, Allocator.Persistent);
        }

        job_noise.data = noiseData;

        var noiseHandle = job_noise.Schedule(job_noise.length, 256 );

        activeParallelMC = parallelMC;

        if ( parallelMC )
        {
            var parameters = new PolygoniserParams { gridSize = gridSize, invertVerticies = inverted, isolevel = isoLevel };
            
            if( lists != null && lists.indicies.Length != parameters.length )
            {
                lists.Dispose();
                lists = null;
            }
            if( lists == null)
            {
                lists = new PolygonParallelLists().Allocate(parameters);
            }

            handle = PolygoniserParallelM.Schedule( parameters, ref job_noise.data , lists, noiseHandle);
        }
        else
        {
            job_polygon = new Polygoniser
            {
                invertVerticies = inverted,
                gridSize = gridSize,
                pointsFlatten = job_noise.data,
                isolevel = isoLevel,
            };

            PolygoniserM.Allocate( ref job_polygon , Allocator.Persistent );

            handle = job_polygon.Schedule( noiseHandle );
        }

        scheduled = true;

        frames = 0;

        timer = Time.timeSinceLevelLoad;

    }

    void Update()
    {
        if( ! scheduled ) return;

        frames ++ ;

        if( handle.IsCompleted )
        {
            // Tracing data ownership requires dependencies to complete before the control
            // thread can use them again. It is not enough to check JobHandle.IsCompleted.
            // You must call the method JobHandle.Complete to regain ownership of the
            // NativeContainer types to the control thread

            handle.Complete();

            execTime = Time.timeSinceLevelLoad - timer;
            execFrames = frames;

            var mesh = filter.mesh;

            if( activeParallelMC )
            {
                var count = lists.counter[ 0 ];

                var write_t = Time.timeSinceLevelLoad;

                if ( writeBuffer )
                {
                    PolygoniserParallelM.WriteBuffer( lists, ref mesh, count );
                }
                else
                {
                    PolygoniserParallelM.Write(lists, ref mesh, count);
                }

                write_t = Time.timeSinceLevelLoad - write_t;
                var wt = ( 1000 * write_t ).ToString("N1") + " ms";

                execInfo = "Vert: " + mesh.vertexCount + ", Count: " + count + ", FPS: " + (1 / execTime).ToString("N1") + ", Write:" + wt;
            }
            else
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

                PolygoniserM.Write( job_polygon, ref mesh );

                execInfo = "Vert: " + mesh.vertexCount + ", Size: " + job_polygon.length + ", FPS: " + ( 1 / execTime ).ToString("N1");

                job_polygon.Dispose();
            }


            scheduled = false;
        }
    }
}
