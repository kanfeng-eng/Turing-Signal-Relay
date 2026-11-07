using UnityEngine;
using UnityEngine.UI;

namespace TuringSignalRelay
{
    // Adapted from the user-owned Anti-fraud Ring.cs, using the same 36-segment circle.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ObserverRangeRing : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Vector2 center = rectTransform.rect.center;
            float outer = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) / 2;
            float inner = Mathf.Max(0, outer - 2);
            for (int i = 0; i < 36; i++)
            {
                float start = i * Mathf.PI * 2 / 36;
                float end = (i + 1) * Mathf.PI * 2 / 36;
                Vector2 a = new Vector2(Mathf.Cos(start), Mathf.Sin(start));
                Vector2 b = new Vector2(Mathf.Cos(end), Mathf.Sin(end));
                int index = mesh.currentVertCount;
                mesh.AddVert(center + a * inner, color, Vector2.zero);
                mesh.AddVert(center + a * outer, color, Vector2.zero);
                mesh.AddVert(center + b * outer, color, Vector2.zero);
                mesh.AddVert(center + b * inner, color, Vector2.zero);
                mesh.AddTriangle(index, index + 1, index + 2);
                mesh.AddTriangle(index, index + 2, index + 3);
            }
        }
    }
}
