using UnityEngine;
using UnityEngine.UIElements;

namespace Diceforge.View
{
    internal static class WorldSelectionVisuals
    {
        internal static void Attach(VisualElement target, string kind)
        {
            var glyph = target;
            if (kind == "home")
            {
                glyph = new VisualElement { pickingMode = PickingMode.Ignore };
                glyph.AddToClassList("world-home-glyph");
                target.Add(glyph);
            }
            glyph.generateVisualContent += context => Draw(context.painter2D, glyph.contentRect.size, kind);
        }

        private static void Draw(Painter2D painter, Vector2 size, string kind)
        {
            if (size.x <= 0 || size.y <= 0) return;
            Vector2 Point(float x, float y) => new Vector2(x * size.x, y * size.y);
            painter.fillColor = painter.strokeColor = new Color(.36f, .20f, .10f);
            painter.lineWidth = Mathf.Min(size.x, size.y) * .13f;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;
            painter.BeginPath();
            if (kind == "route")
            {
                painter.fillColor = new Color(.55f, .57f, .24f, .85f);
                for (int index = 0; index < 8; index++)
                {
                    float t = index / 7f;
                    painter.BeginPath();
                    painter.Arc(Point(.05f + .9f * t, .6f - Mathf.Sin(t * Mathf.PI) * .32f), Mathf.Min(3.6f, size.x * .024f), Angle.Degrees(0), Angle.Degrees(360));
                    painter.Fill();
                }
                return;
            }
            if (kind == "check")
            {
                painter.strokeColor = new Color(.69f, .95f, .22f);
                painter.MoveTo(Point(.12f, .5f)); painter.LineTo(Point(.39f, .77f)); painter.LineTo(Point(.90f, .19f));
                painter.Stroke();
                return;
            }
            if (kind == "home")
            {
                float radius = Mathf.Min(size.x, size.y) * .5f;
                void Circle(float inset, float offset, Color fill, Color rim, float thickness)
                {
                    painter.BeginPath();
                    painter.Arc(Point(.5f, .5f) + Vector2.up * offset, radius - inset, Angle.Degrees(0), Angle.Degrees(360));
                    painter.fillColor = fill; painter.Fill();
                    painter.strokeColor = rim; painter.lineWidth = thickness; painter.Stroke();
                }
                Circle(3, 3, new Color(.30f, .20f, .10f, .12f), Color.clear, 0);
                Circle(5, 2, new Color(.69f, .52f, .32f), new Color(.75f, .60f, .40f), 1);
                Circle(5, -1, new Color(.98f, .90f, .75f), new Color(1, .99f, .92f), 2);
                Circle(9, -1, new Color(1, .95f, .83f), new Color(.85f, .71f, .51f), 1);
                Vector2 House(float x, float y) => Point(.25f + x * .5f, .22f + y * .5f);
                painter.BeginPath();
                painter.MoveTo(House(.06f, .44f)); painter.LineTo(House(.5f, .06f)); painter.LineTo(House(.94f, .44f));
                painter.LineTo(House(.82f, .44f)); painter.LineTo(House(.82f, .93f));
                painter.LineTo(House(.59f, .93f)); painter.LineTo(House(.59f, .64f));
                painter.LineTo(House(.41f, .64f)); painter.LineTo(House(.41f, .93f));
                painter.LineTo(House(.18f, .93f)); painter.LineTo(House(.18f, .44f));
                painter.ClosePath(); painter.fillColor = new Color(.44f, .27f, .14f); painter.Fill();
                painter.strokeColor = new Color(.31f, .17f, .08f); painter.lineWidth = 1.2f; painter.Stroke();
                painter.BeginPath(); painter.MoveTo(House(.10f, .42f)); painter.LineTo(House(.50f, .10f)); painter.LineTo(House(.90f, .42f));
                painter.strokeColor = new Color(.66f, .46f, .27f); painter.lineWidth = 1; painter.Stroke();
                return;
            }
            painter.MoveTo(Point(.27f, .47f)); painter.LineTo(Point(.27f, .28f));
            painter.BezierCurveTo(Point(.27f, -.01f), Point(.73f, -.01f), Point(.73f, .28f));
            painter.LineTo(Point(.73f, .47f)); painter.Stroke();
            painter.BeginPath(); painter.MoveTo(Point(.12f, .40f)); painter.LineTo(Point(.88f, .40f));
            painter.LineTo(Point(.88f, .97f)); painter.LineTo(Point(.12f, .97f)); painter.ClosePath(); painter.Fill();
            painter.strokeColor = new Color(1, .94f, .80f);
            painter.BeginPath(); painter.MoveTo(Point(.5f, .6f)); painter.LineTo(Point(.5f, .79f)); painter.Stroke();
        }
    }
}
