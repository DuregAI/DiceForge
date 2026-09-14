using UnityEngine;
using UnityEngine.UIElements;

// Decorative vectors stay sharp at every panel scale and never intercept clicks.
internal sealed class MenuButtonIcon : VisualElement
{
    private readonly bool book;

    private MenuButtonIcon(bool book)
    {
        this.book = book;
        name = "MenuActionIcon";
        pickingMode = PickingMode.Ignore;
        AddToClassList("gh-action-icon");
        generateVisualContent += Draw;
    }

    public static void Attach(Button button, bool book)
    {
        if (button != null && button.Q("MenuActionIcon") == null)
            button.Add(new MenuButtonIcon(book));
    }

    private void Draw(MeshGenerationContext context)
    {
        var p = context.painter2D;
        float w = contentRect.width;
        float h = contentRect.height;
        if (w <= 0 || h <= 0) return;
        if (!book)
        {
            DrawCarvedPlay(p, w, h);
            return;
        }
        p.strokeColor = new Color32(62, 41, 22, 255);
        p.lineWidth = book ? 2.5f : 2f;
        p.fillColor = book ? new Color32(244, 211, 155, 255) : new Color32(255, 241, 213, 255);
        p.BeginPath();
        if (book)
        {
            p.MoveTo(new Vector2(w * .08f, h * .2f));
            p.LineTo(new Vector2(w * .3f, h * .16f));
            p.LineTo(new Vector2(w * .5f, h * .27f));
            p.LineTo(new Vector2(w * .7f, h * .16f));
            p.LineTo(new Vector2(w * .92f, h * .2f));
            p.LineTo(new Vector2(w * .92f, h * .79f));
            p.LineTo(new Vector2(w * .7f, h * .75f));
            p.LineTo(new Vector2(w * .5f, h * .86f));
            p.LineTo(new Vector2(w * .3f, h * .75f));
            p.LineTo(new Vector2(w * .08f, h * .79f));
        }
        else
        {
            p.MoveTo(new Vector2(w * .22f, h * .12f));
            p.LineTo(new Vector2(w * .86f, h * .5f));
            p.LineTo(new Vector2(w * .22f, h * .88f));
        }
        p.ClosePath();
        p.Fill();
        p.Stroke();
        if (!book) return;
        p.BeginPath();
        p.MoveTo(new Vector2(w * .5f, h * .27f));
        p.LineTo(new Vector2(w * .5f, h * .86f));
        p.Stroke();
    }

    private static void DrawCarvedPlay(Painter2D p, float w, float h)
    {
        var a = new Vector2(w * .17f, h * .08f);
        var b = new Vector2(w * .91f, h * .5f);
        var c = new Vector2(w * .17f, h * .92f);
        var ia = a + new Vector2(4, 5);
        var ib = b + new Vector2(-6, 0);
        var ic = c + new Vector2(4, -5);
        // Recess, with a dark upper cut and exposed warm wood on the lower cut.
        Face(p, new Color32(39, 42, 14, 255), a, b, c);
        Face(p, new Color32(190, 161, 78, 255), c, b, ib, ic);
        Face(p, new Color32(122, 125, 46, 255), a, c, ic, ia);
        Face(p, new Color32(78, 85, 28, 255), ia, ib, ic);
        p.lineWidth = 1.1f;
        p.strokeColor = new Color32(219, 187, 103, 255);
        p.BeginPath();
        p.MoveTo(c + new Vector2(0, 1));
        p.LineTo(b + new Vector2(0, 1));
        p.Stroke();
        // Short grain marks tie the inset face to the wooden board.
        p.strokeColor = new Color32(46, 56, 18, 180);
        p.lineWidth = 1;
        p.BeginPath();
        p.MoveTo(new Vector2(w * .31f, h * .46f));
        p.LineTo(new Vector2(w * .65f, h * .48f));
        p.MoveTo(new Vector2(w * .31f, h * .61f));
        p.LineTo(new Vector2(w * .48f, h * .60f));
        p.Stroke();
    }

    private static void Face(Painter2D p, Color color, Vector2 a, Vector2 b, Vector2 c, Vector2? d = null)
    {
        p.fillColor = color;
        p.BeginPath();
        p.MoveTo(a);
        p.LineTo(b);
        p.LineTo(c);
        if (d.HasValue) p.LineTo(d.Value);
        p.ClosePath();
        p.Fill();
    }
}
