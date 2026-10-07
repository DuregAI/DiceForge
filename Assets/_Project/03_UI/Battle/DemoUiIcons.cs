using UnityEngine;
using UnityEngine.UIElements;

namespace Diceforge.View
{
    internal enum DemoUiIcon { Pause, Settings, Help, Close, Next }

    internal static class DemoUiIcons
    {
        public static VisualElement Attach(Button button, DemoUiIcon icon)
        {
            button.text = string.Empty;
            var glyph = new VisualElement { name = "demoIcon", pickingMode = PickingMode.Ignore };
            glyph.AddToClassList("demo-service-icon");
            glyph.generateVisualContent += context => Draw(context.painter2D, icon, button.resolvedStyle.color);
            button.Add(glyph);
            button.RegisterCallback<CustomStyleResolvedEvent>(_ => glyph.MarkDirtyRepaint());
            return glyph;
        }

        private static void Draw(Painter2D painter, DemoUiIcon icon, Color color)
        {
            painter.strokeColor = painter.fillColor = color;
            painter.lineWidth = 2;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;
            painter.BeginPath();
            switch (icon)
            {
                case DemoUiIcon.Pause:
                    painter.MoveTo(new Vector2(8, 5)); painter.LineTo(new Vector2(8, 19));
                    painter.MoveTo(new Vector2(16, 5)); painter.LineTo(new Vector2(16, 19));
                    break;
                case DemoUiIcon.Settings:
                    for (int point = 0; point < 32; point++)
                    {
                        float angle = point * Mathf.PI / 16 - Mathf.PI / 16;
                        float radius = point % 4 == 0 || point % 4 == 3 ? 7.5f : 9.5f;
                        var vertex = new Vector2(12 + Mathf.Cos(angle) * radius, 12 + Mathf.Sin(angle) * radius);
                        if (point == 0) painter.MoveTo(vertex); else painter.LineTo(vertex);
                    }
                    painter.ClosePath();
                    painter.Stroke();
                    painter.BeginPath();
                    painter.Arc(new Vector2(12, 12), 3, Angle.Degrees(0), Angle.Degrees(360));
                    break;
                case DemoUiIcon.Help:
                    painter.MoveTo(new Vector2(8, 8));
                    painter.BezierCurveTo(new Vector2(8, 3), new Vector2(17, 3), new Vector2(17, 8));
                    painter.BezierCurveTo(new Vector2(17, 11), new Vector2(12, 11), new Vector2(12, 14));
                    painter.LineTo(new Vector2(12, 15));
                    painter.Stroke();
                    painter.BeginPath();
                    painter.Arc(new Vector2(12, 19), 1, Angle.Degrees(0), Angle.Degrees(360));
                    painter.Fill();
                    return;
                case DemoUiIcon.Close:
                    painter.MoveTo(new Vector2(7, 7)); painter.LineTo(new Vector2(17, 17));
                    painter.MoveTo(new Vector2(17, 7)); painter.LineTo(new Vector2(7, 17));
                    break;
                case DemoUiIcon.Next:
                    painter.MoveTo(new Vector2(9, 5));
                    painter.LineTo(new Vector2(16, 12)); painter.LineTo(new Vector2(9, 19));
                    break;
            }
            painter.Stroke();
        }
    }
}
