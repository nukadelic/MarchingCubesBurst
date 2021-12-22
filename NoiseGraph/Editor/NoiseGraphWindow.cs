
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace MCBurst.NoiseGraph
{
    public class NoiseGraphWindow : EditorWindow
    {
        [MenuItem("Window/MCBurst/Noise Graph")]
        public static void Open()
        {
            GetWindow<NoiseGraphWindow>("Noise Graph");
        }

        private void OnEnable()
        {
            NoiseGraphView graphview = new NoiseGraphView();

            graphview.StretchToParentSize();

            rootVisualElement.Add( graphview );

            rootVisualElement.styleSheets.Add( "Variables" );
        }

    }
}

