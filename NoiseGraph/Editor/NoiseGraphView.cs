

namespace MCBurst.NoiseGraph
{
    using UnityEditor.Experimental.GraphView;
    using UnityEngine.UIElements;
    
    public class NoiseGraphView : GraphView
    {
        public NoiseGraphView()
        {
            //this.AddManipulator( new ContentZoomer() );

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

            this.AddManipulator(new ContentDragger());

            GridBackground grid = new GridBackground();

            grid.StretchToParentSize();

            Insert(0, grid);

            styleSheets.Add("Style");
        }
    }

}