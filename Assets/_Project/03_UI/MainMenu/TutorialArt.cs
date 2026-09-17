using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// Atlas artwork clipped to authored silhouettes by UI geometry; no runtime texture copies.
internal sealed class TutorialArt : VisualElement
{
    private static Texture2D atlas;
    private int index;
    private Vector2[] outline;
    private readonly List<int> triangles = new();
    public TutorialArt(int index)
    {
        pickingMode = PickingMode.Ignore;
        AddToClassList("jo-art");
        atlas ??= Resources.Load<Texture2D>("JoTutorial/atlas");
        SetIndex(index);
        generateVisualContent += Draw;
    }
    public void SetIndex(int value)
    {
        value = Mathf.Clamp(value, 0, 5);
        if (outline != null && index == value) return;
        index = value;
        if (index < 3)
        {
            outline = new Vector2[64];
            for (int i = 0; i < outline.Length; i++)
            {
                float a = i * Mathf.PI * 2 / outline.Length;
                outline[i] = new Vector2(256 + Mathf.Cos(a) * 207, 273 + Mathf.Sin(a) * 201);
            }
        }
        else if (index < 5)
            outline = new[] { new Vector2(99,80),new Vector2(390,80),new Vector2(427,101),new Vector2(441,147),new Vector2(453,326),new Vector2(440,377),new Vector2(396,395),new Vector2(106,395),new Vector2(73,378),new Vector2(59,337),new Vector2(69,167),new Vector2(82,109) };
        else
            outline = new[] { new Vector2(166,5),new Vector2(283,0),new Vector2(309,22),new Vector2(332,0),new Vector2(347,17),new Vector2(433,15),new Vector2(414,46),new Vector2(388,80),new Vector2(385,128),new Vector2(364,153),new Vector2(398,169),new Vector2(419,146),new Vector2(451,151),new Vector2(457,181),new Vector2(430,202),new Vector2(382,208),new Vector2(368,267),new Vector2(391,295),new Vector2(405,320),new Vector2(399,334),new Vector2(423,369),new Vector2(429,425),new Vector2(391,454),new Vector2(164,465),new Vector2(88,434),new Vector2(77,384),new Vector2(103,335),new Vector2(164,307),new Vector2(175,270),new Vector2(195,218),new Vector2(155,223),new Vector2(135,197),new Vector2(148,164),new Vector2(181,147),new Vector2(158,129),new Vector2(126,120),new Vector2(83,91),new Vector2(61,65),new Vector2(108,57),new Vector2(163,69),new Vector2(158,42) };
        Triangulate();
        MarkDirtyRepaint();
    }
    private void Triangulate()
    {
        triangles.Clear();
        var remaining = new List<int>();
        for (int i = 0; i < outline.Length; i++) remaining.Add(i);
        int guard = outline.Length * outline.Length;
        while (remaining.Count > 2 && guard-- > 0)
        {
            bool clipped = false;
            for (int i = 0; i < remaining.Count; i++)
            {
                int a = remaining[(i + remaining.Count - 1) % remaining.Count], b = remaining[i], c = remaining[(i + 1) % remaining.Count];
                if (Cross(outline[b] - outline[a], outline[c] - outline[b]) <= 0) continue;
                bool inside = false;
                foreach (int t in remaining)
                {
                    if (t == a || t == b || t == c) continue;
                    var p = outline[t];
                    if (Cross(outline[b]-outline[a],p-outline[a]) >= 0 && Cross(outline[c]-outline[b],p-outline[b]) >= 0 && Cross(outline[a]-outline[c],p-outline[c]) >= 0) { inside = true; break; }
                }
                if (inside) continue;
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                remaining.RemoveAt(i); clipped = true; break;
            }
            if (!clipped) break;
        }
    }
    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    private void Draw(MeshGenerationContext context)
    {
        if (atlas == null || contentRect.width <= 0 || contentRect.height <= 0) return;
        var mesh = context.Allocate(outline.Length, triangles.Count, atlas);
        for (int i = 0; i < outline.Length; i++)
        {
            var p = outline[i];
            mesh.SetNextVertex(new Vertex { position = new Vector3(p.x / 512 * contentRect.width, p.y / 512 * contentRect.height, Vertex.nearZ), tint = Color.white,
                uv = new Vector2((index % 3 * 512 + p.x) / 1536f, 1 - (index / 3 * 512 + p.y) / 1024f) });
        }
        foreach (int i in triangles) mesh.SetNextIndex((ushort)i);
    }
}
