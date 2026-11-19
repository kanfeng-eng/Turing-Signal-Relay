using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TuringSignalRelay
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ObserverGraph : MaskableGraphic
    {
        public IList<ObserverFrame> Frames;
        public int Last;
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (Frames == null || Frames.Count < 2)
                return;
            Rect area = rectTransform.rect;
            float maximum = 1;
            foreach (var frame in Frames)
                maximum = Mathf.Max(maximum, Mathf.Max(frame.scoreHalfPoints[0], frame.scoreHalfPoints[1]));
            int stride = Mathf.Max(1, Last / 150);
            for (int team = 0; team < 2; team++)
            for (int i = stride; i <= Last; i += stride)
            {
                Vector2 a = new Vector2(area.x + (i - stride) * area.width / (Frames.Count - 1), area.y + Frames[i - stride].scoreHalfPoints[team] / maximum * area.height);
                Vector2 b = new Vector2(area.x + i * area.width / (Frames.Count - 1), area.y + Frames[i].scoreHalfPoints[team] / maximum * area.height);
                Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * 1.2f;
                int start = helper.currentVertCount;
                Color color = team == 0 ? new Color(.88f, .4f, .35f) : new Color(.36f, .7f, .91f);
                helper.AddVert(a - normal, color, Vector2.zero);
                helper.AddVert(a + normal, color, Vector2.zero);
                helper.AddVert(b + normal, color, Vector2.zero);
                helper.AddVert(b - normal, color, Vector2.zero);
                helper.AddTriangle(start, start + 1, start + 2);
                helper.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}
