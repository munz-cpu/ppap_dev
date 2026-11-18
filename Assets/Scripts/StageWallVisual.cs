using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer), typeof(BoxCollider2D))]
public sealed class StageWallVisual : MaskableGraphic
{
    private const float ShatterDuration = 0.26f;
    private const int ShardCount = 4;

    private BoxCollider2D wall;
    private bool breakable;
    private float shatterTime = -1f;

    internal bool CanBreak => breakable && shatterTime < 0f;

    internal void ResetWall(BoxCollider2D collider, bool canBreak)
    {
        wall = collider;
        wall.enabled = true;
        breakable = canBreak;
        shatterTime = -1f;
        SetVerticesDirty();
    }

    internal void BreakOnImpact()
    {
        if (!CanBreak)
            return;

        wall.enabled = false;
        shatterTime = 0f;
        SetVerticesDirty();
    }

    private void Update()
    {
        if (shatterTime < 0f)
            return;

        shatterTime += Time.deltaTime;
        if (shatterTime >= ShatterDuration)
            gameObject.SetActive(false);
        else
            SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect area = rectTransform.rect;
        bool horizontal = area.width >= area.height;
        Vector2 along = horizontal ? Vector2.right : Vector2.up;
        Vector2 across = horizontal ? Vector2.up : Vector2.right;
        float length = horizontal ? area.width : area.height;
        float thickness = horizontal ? area.height : area.width;

        if (shatterTime >= 0f)
        {
            float time = Mathf.Clamp01(shatterTime / ShatterDuration);
            float travel = 1f - (1f - time) * (1f - time);
            Color tint = color;
            tint.a *= 1f - time;
            Vector2 size = (along * (length / ShardCount - 0.025f) + across * thickness) * 0.5f;
            for (int i = 0; i < ShardCount; i++)
            {
                float index = i - (ShardCount - 1) * 0.5f;
                Vector2 center = area.center + along * (index * (length / ShardCount + 0.18f * travel));
                center += across * ((i % 2 == 0 ? 0.26f : -0.26f) * travel);
                AddRectangle(mesh, center, size, tint);
            }
            return;
        }

        AddRectangle(mesh, area.center, area.size * 0.5f, color);
        if (breakable)
        {
            DrawCrack(mesh, area.center - along * (length * 0.17f), along, across, thickness);
            DrawCrack(mesh, area.center + along * (length * 0.17f), -along, across, thickness);
        }
    }

    private static void DrawCrack(VertexHelper mesh, Vector2 center, Vector2 along, Vector2 across, float thickness)
    {
        Vector2 a = center - across * (thickness * 0.42f) - along * 0.09f;
        Vector2 b = center - across * (thickness * 0.12f) + along * 0.045f;
        Vector2 c = center + across * (thickness * 0.14f) - along * 0.025f;
        Vector2 d = center + across * (thickness * 0.42f) + along * 0.09f;
        AddLine(mesh, a, b);
        AddLine(mesh, b, c);
        AddLine(mesh, c, d);
    }

    private static void AddLine(VertexHelper mesh, Vector2 a, Vector2 b)
    {
        Vector2 edge = Vector2.Perpendicular((b - a).normalized) * 0.016f;
        AddQuad(mesh, a + edge, b + edge, b - edge, a - edge, Color.white);
    }

    private static void AddRectangle(VertexHelper mesh, Vector2 center, Vector2 halfSize, Color tint)
    {
        Vector2 corner = new Vector2(-halfSize.x, halfSize.y);
        AddQuad(mesh, center - halfSize, center + corner, center + halfSize, center - corner, tint);
    }

    private static void AddQuad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(a, tint, Vector2.zero);
        mesh.AddVert(b, tint, Vector2.zero);
        mesh.AddVert(c, tint, Vector2.zero);
        mesh.AddVert(d, tint, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start + 2, start + 3, start);
    }
}
