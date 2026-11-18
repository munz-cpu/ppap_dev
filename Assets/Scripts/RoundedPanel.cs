using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class RoundedPanel : MaskableGraphic
{
    private const int SegmentsPerCorner = 9;

    [SerializeField, Min(0f)] private float cornerRadius = 18f;
    [SerializeField, Min(0f)] private float borderWidth;
    [SerializeField] private Color borderColor = new Color(0.86f, 0.89f, 0.94f);
    [SerializeField, Min(0f)] private float shadowOffset;
    [SerializeField] private Color shadowColor = new Color(0.15f, 0.24f, 0.40f, 0.07f);

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect bounds = rectTransform.rect;
        float maxRadius = Mathf.Min(bounds.width, bounds.height) * 0.5f;
        if (maxRadius <= 0f)
            return;

        if (shadowOffset > 0f && shadowColor.a > 0f)
        {
            Rect shadow = bounds;
            shadow.y -= shadowOffset;
            AddRoundedRect(mesh, shadow, cornerRadius, shadowColor);
        }

        float border = Mathf.Min(borderWidth, maxRadius);
        if (border > 0f)
        {
            AddRoundedRect(mesh, bounds, cornerRadius, borderColor);
            bounds = Rect.MinMaxRect(bounds.xMin + border, bounds.yMin + border,
                bounds.xMax - border, bounds.yMax - border);
        }

        if (bounds.width > 0f && bounds.height > 0f)
            AddRoundedRect(mesh, bounds, Mathf.Max(0f, cornerRadius - border), color);
    }

    private static void AddRoundedRect(VertexHelper mesh, Rect bounds, float requestedRadius, Color tint)
    {
        float radius = Mathf.Min(requestedRadius, Mathf.Min(bounds.width, bounds.height) * 0.5f);
        int center = mesh.currentVertCount;
        mesh.AddVert(bounds.center, tint, Vector2.zero);
        for (int corner = 0; corner < 4; corner++)
        {
            bool right = corner == 0 || corner == 3;
            bool top = corner < 2;
            Vector2 arc = new Vector2(right ? bounds.xMax - radius : bounds.xMin + radius,
                top ? bounds.yMax - radius : bounds.yMin + radius);
            for (int i = 0; i <= SegmentsPerCorner; i++)
            {
                float angle = (corner + (float)i / SegmentsPerCorner) * Mathf.PI * 0.5f;
                Vector2 position = arc + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                mesh.AddVert(position, tint, Vector2.zero);
            }
        }

        int edgeCount = 4 * (SegmentsPerCorner + 1);
        for (int i = 0; i < edgeCount; i++)
            mesh.AddTriangle(center, center + 1 + (i + 1) % edgeCount, center + 1 + i);
    }
}
