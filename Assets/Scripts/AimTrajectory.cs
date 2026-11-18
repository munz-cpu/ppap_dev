using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class AimTrajectory : MaskableGraphic
{
    private const float DotSpacing = 0.30f;
    private const float DotRadius = 0.036f;
    private const int DotSides = 6;
    private const int MaxDots = 140;

    private Vector2[] path;
    private int pointCount;

    internal void SetPath(Vector2[] points, int count)
    {
        if (count < 2 && pointCount < 2)
            return;

        path = points;
        pointCount = count;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (pointCount < 2)
            return;

        float offset = 0.52f;
        int drawn = 0;
        for (int i = 1; i < pointCount && drawn < MaxDots; i++)
        {
            Vector2 start = path[i - 1];
            Vector2 delta = path[i] - start;
            float length = delta.magnitude;
            while (offset <= length && drawn < MaxDots)
            {
                Vector2 position = start + delta * (offset / length);
                AddDot(mesh, rectTransform.InverseTransformPoint(position));
                offset += DotSpacing;
                drawn++;
            }

            offset -= length;
        }
    }

    private void AddDot(VertexHelper mesh, Vector2 center)
    {
        int first = mesh.currentVertCount;
        mesh.AddVert(center, color, Vector2.zero);
        for (int side = 0; side < DotSides; side++)
        {
            float angle = side * (2f * Mathf.PI / DotSides);
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * DotRadius;
            mesh.AddVert(center + offset, color, Vector2.zero);
            mesh.AddTriangle(first, first + 1 + (side + 1) % DotSides, first + 1 + side);
        }
    }
}
