using UnityEngine;
using UnityEngine.UIElements;

// Cloth-shaped vector flags; the button owns input and language persistence.
internal sealed class LanguageBadge : VisualElement
{
    private int language;
    private static readonly Color Cream = new Color32(250, 232, 190, 255);
    private static readonly Color Red = new Color32(185, 47, 34, 255);
    private static readonly Color Blue = new Color32(39, 65, 105, 255);

    public LanguageBadge()
    {
        pickingMode = PickingMode.Ignore;
        style.flexGrow = 1;
        generateVisualContent += Draw;
    }

    public void SetLanguage(int value) { language = value; MarkDirtyRepaint(); }

    private void Draw(MeshGenerationContext context)
    {
        float w = contentRect.width, h = contentRect.height;
        if (w <= 0 || h <= 0) return;
        if (language == 0)
        {
            DrawFlag(context.painter2D, new Rect(w * .02f, h * .08f, w * .72f, h * .48f), 0);
            DrawFlag(context.painter2D, new Rect(w * .25f, h * .40f, w * .72f, h * .48f), 3);
        }
        else DrawFlag(context.painter2D, new Rect(w * .06f, h * .22f, w * .88f, h * .59f), language);
    }

    private static Vector2 Point(Rect r, float x, float y)
    {
        // A soft fold and tapered free edge make the flags feel like fabric.
        return new Vector2(r.x + x * r.width, r.y + (y + Mathf.Sin(x * Mathf.PI * 2) * .045f + x * .035f) * r.height);
    }

    private static void Patch(Painter2D p, Rect r, float x, float y, float w, float h, Color color)
    {
        p.fillColor = color;
        p.BeginPath();
        p.MoveTo(Point(r, x, y));
        for (int i = 1; i <= 12; i++) p.LineTo(Point(r, x + w * i / 12, y));
        for (int i = 12; i >= 0; i--) p.LineTo(Point(r, x + w * i / 12, y + h));
        p.ClosePath(); p.Fill();
    }

    private static void Polygon(Painter2D p, Rect r, Color color, params Vector2[] points)
    {
        p.fillColor = color; p.BeginPath();
        p.MoveTo(Point(r, points[0].x, points[0].y));
        for (int i = 1; i < points.Length; i++) p.LineTo(Point(r, points[i].x, points[i].y));
        p.ClosePath(); p.Fill();
    }

    private static void DrawFlag(Painter2D p, Rect r, int kind)
    {
        var shadow = r; shadow.position += new Vector2(1, 3);
        Patch(p, shadow, -.025f, -.035f, 1.05f, 1.07f, new Color32(33, 23, 13, 130));
        Patch(p, r, -.02f, -.035f, 1.04f, 1.07f, new Color32(79, 48, 25, 255));
        if (kind == 1)
        {
            Patch(p, r, 0, 0, 1, 1f / 3, Cream);
            Patch(p, r, 0, 1f / 3, 1, 1f / 3, Blue);
            Patch(p, r, 0, 2f / 3, 1, 1f / 3, Red);
        }
        else if (kind == 2)
        {
            Patch(p, r, 0, 0, 1, 1, Red);
            Star(p, r, .2f, .29f, .125f);
            Star(p, r, .35f, .12f, .042f);
            Star(p, r, .43f, .24f, .042f);
            Star(p, r, .43f, .41f, .042f);
            Star(p, r, .35f, .54f, .042f);
        }
        else if (kind == 0)
        {
            Patch(p, r, 0, 0, 1, 1, Blue);
            Polygon(p, r, Cream, new Vector2(0, 0), new Vector2(.13f, 0), new Vector2(1, .87f), new Vector2(1, 1), new Vector2(.87f, 1), new Vector2(0, .13f));
            Polygon(p, r, Cream, new Vector2(0, .87f), new Vector2(.87f, 0), new Vector2(1, 0), new Vector2(1, .13f), new Vector2(.13f, 1), new Vector2(0, 1));
            Polygon(p, r, Red, new Vector2(0, 0), new Vector2(.045f, 0), new Vector2(1, .955f), new Vector2(1, 1), new Vector2(.955f, 1), new Vector2(0, .045f));
            Polygon(p, r, Red, new Vector2(0, .955f), new Vector2(.955f, 0), new Vector2(1, 0), new Vector2(1, .045f), new Vector2(.045f, 1), new Vector2(0, 1));
            Patch(p, r, 0, .35f, 1, .3f, Cream);
            Patch(p, r, .39f, 0, .22f, 1, Cream);
            Patch(p, r, 0, .41f, 1, .18f, Red);
            Patch(p, r, .44f, 0, .12f, 1, Red);
        }
        else
        {
            Patch(p, r, 0, 0, 1, 1, Cream);
            for (int i = 0; i < 13; i += 2) Patch(p, r, 0, i / 13f, 1, 1f / 13, Red);
            Patch(p, r, 0, 0, .43f, 7f / 13, Blue);
            for (int row = 0; row < 9; row++)
                for (int col = 0; col < (row % 2 == 0 ? 6 : 5); col++)
                    Star(p, r, .035f + col * .071f + (row % 2) * .035f, .035f + row * .058f, .018f, Cream);
        }
        Patch(p, r, .13f, 0, .10f, 1, new Color32(255, 243, 209, 25));
        Patch(p, r, .57f, 0, .10f, 1, new Color32(44, 26, 15, 24));
        Patch(p, r, 0, .018f, 1, .018f, new Color32(255, 221, 155, 100));
        Patch(p, r, .015f, 0, .018f, 1, new Color32(249, 212, 146, 130));
    }

    private static void Star(Painter2D p, Rect r, float x, float y, float radius, Color? color = null)
    {
        p.fillColor = color ?? new Color32(249, 208, 95, 255); p.BeginPath();
        for (int i = 0; i < 10; i++)
        {
            float a = -Mathf.PI / 2 + i * Mathf.PI / 5;
            float rad = i % 2 == 0 ? radius : radius * .4f;
            var v = Point(r, x + Mathf.Cos(a) * rad * r.height / r.width, y + Mathf.Sin(a) * rad);
            if (i == 0) p.MoveTo(v); else p.LineTo(v);
        }
        p.ClosePath(); p.Fill();
    }
}
