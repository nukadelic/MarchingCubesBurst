
namespace MCBurst.NoiseGraph
{
    using UnityEngine.UIElements;
    using UnityEditor;
    using UnityEngine;

    public static class NoiseGraphEx
    {
        public static void Add( this VisualElementStyleSheetSet self, string path )
        {
            self.Add( ( StyleSheet ) Resources.Load( "NoiseGraph" + path ) );

            // self.Add( ( StyleSheet ) EditorGUIUtility.Load( "MCBurstNoiseGraph/NoiseGraph" + path + ".uss" ) );
        }
    }
}