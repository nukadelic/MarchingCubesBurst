
namespace MCBurst
{

    using Unity.Collections;
    using Unity.Mathematics;

    /*
    Based on : 
    Polygonising a scalar field - Also known as: "3D Contouring", "Marching Cubes", "Surface Reconstruction" - Written by Paul Bourke - May 1994 
    http://paulbourke.net/geometry/polygonise/
     */
    public struct Cell
    {
        public const int points_count = 8;
        public const int vertlist_count = 12;
        public const int triangles_count = 5;

        public NativeArray<float4> points;
        public NativeArray<float3> vertlist;
        public NativeArray<float3x3> triangles;
        public int count; // result triangle count 

        public Cell Allocate( Allocator alloc = ( Allocator ) 2 )
        {
            count = 0;
            points = new NativeArray<float4>(points_count, alloc);
            vertlist = new NativeArray<float3>(vertlist_count, alloc);
            triangles = new NativeArray<float3x3>(triangles_count, alloc);
            return this;
        }

        public void Dispose()
        {
            points.Dispose();
            vertlist.Dispose();
            triangles.Dispose();
        }
    }

    public static class Solver
    {
        //  Given a grid cell and an isolevel, calculate the triangular
        //  facets required to represent the isosurface through the cell.
        //  Return the number of triangular facets, the array "triangles"
        //  will be loaded up with the vertices at most 5 triangular facets.
        //  0 will be returned if the grid cell is either totally above
        //  of totally below the isolevel.

        public static void Polygonise(ref Cell cell, float isolevel)
        {
            cell.count = 0;

            int configurationIndex = 0;

            //  Determine the index into the edge table which
            //  tells us which vertices are inside of the surface

            for (var i = 0; i < 8; ++i)
                if (cell.points[i].w < isolevel)
                    configurationIndex |= 1 << i;

            var edge = Tables.edge[configurationIndex];

            if (edge == 0) return; //  Cube is entirely in/out of the surface

            //  Find the vertices where the surface intersects the cube

            for (var i = 0; i < 12; ++i)
            {
                if ((edge & (1 << i)) == 0) cell.vertlist[i] = float3.zero;

                else
                {
                    var p1 = cell.points[ Tables.edgeToVertex[ i * 2 + 0 ] ];
                    var p2 = cell.points[ Tables.edgeToVertex[ i * 2 + 1 ] ];

                    cell.vertlist[i] = VertexInterp(isolevel, p1, p2); // p1.xyz; // 
                }
            }

            // The counter 'c' acts as the index position to read from the triangles table 

            int c = 0;

            // Create the triangles by seeking forward until we reach a stop 

            while ( Tables.triangle[ 16 * configurationIndex + c ] != -1 )
            {
                var t = cell.triangles[ cell.count ];

                for (var i = 0; i < 3; ++i) 
                {
                    // Since the table is in 1D flat array format , shift towards the right index 

                    int ii = 16 * configurationIndex + c + i;

                    // Get the already interpolated verticies from the lookup table 

                    float3 v = cell.vertlist[ Tables.triangle[ ii ] ];

                    // write triangle row 

                    SetFloat3x3Row( ref t, i, v );

                    //switch ( i ) // /!\ float3x3 only has get :: public ref float3 this[int index] { get; }
                    //{
                    //    case 0 : t.c0 = v ; break;
                    //    case 1 : t.c1 = v ; break;
                    //    case 2 : t.c2 = v ; break;
                    //}
                }

                cell.triangles[ cell.count ] = t;

                cell.count ++ ; 

                // Each triangle containes 3 verticies , and above we loop 'i' 3 times , hence advance the lookup index by 3 
                
                c += 3;
            }
        }

        public static void SetFloat3x3Row( ref float3x3 item , int row , float3 value )
        {
            // [!] float3x3 only has get :: public ref float3 this[ int index ] { get; }

            // the switch replaces the following line : 
            //
            //      item [ row ] = value ; 
            //

            switch ( row )
            {
                case 0: item.c0 = value; break;
                case 1: item.c1 = value; break;
                case 2: item.c2 = value; break;
            }
        }

        public static float3 VertexInterp(float isolevel, float4 p1, float4 p2)
        {
            // Smooth vertex interpolation between two points (xyz) the value on each point (w) 
            // computed by using the relevant iso level

            if ( math.abs( isolevel - p1.w ) < 1e-5f)  return p1.xyz;
            if ( math.abs( isolevel - p2.w ) < 1e-5f)  return p2.xyz;
            if ( math.abs( p1.w - p2.w     ) < 1e-5f)  return p1.xyz;

            float t = (isolevel - p1.w) / (p2.w - p1.w);

            return math.lerp( p1.xyz , p2.xyz , t );
        }

    }
}
