using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Diceforge.Map
{
    public sealed class WoodlandWaterElement : VisualElement, IDisposable
    {
        // Coordinates match the 1672 x 941 scenery. Separate reaches leave the bridge dry.
        private readonly Reach[] reaches =
        {
            new(new[] { new Vector2(296, 83), new Vector2(333, 96), new Vector2(355, 116) }, 7, 22, 7, false),
            new(new[] { new Vector2(369, 145), new Vector2(387, 190), new Vector2(397, 254) }, 15, 78, 14, true),
            new(new[] { new Vector2(417, 294), new Vector2(391, 325), new Vector2(314, 366), new Vector2(280, 407), new Vector2(303, 449), new Vector2(330, 466) }, 13, 24, 20, false),
            new(new[] { new Vector2(420, 565), new Vector2(476, 591), new Vector2(541, 620), new Vector2(614, 659), new Vector2(707, 694), new Vector2(793, 735), new Vector2(889, 769), new Vector2(962, 806), new Vector2(1008, 831) }, 13, 29, 38, false),
            new(new[] { new Vector2(1014, 852), new Vector2(1016, 880) }, 17, 58, 7, true),
            new(new[] { new Vector2(1010, 909), new Vector2(1030, 938) }, 30, 20, 8, false)
        };
        private readonly IVisualElementScheduledItem animation;
        private float time;
        private readonly WoodlandWaterSettings settings;

        public WoodlandWaterElement(WoodlandWaterSettings settings = null)
        {
            this.settings = settings ?? new WoodlandWaterSettings();
            name = "WoodlandWater";
            pickingMode = PickingMode.Ignore;
            AddToClassList("wm-water");
            generateVisualContent += Draw;
            animation = schedule.Execute(Tick).Every(33);
        }

        public void SetActive(bool active)
        {
            if (active) animation.Resume();
            else animation.Pause();
        }

        private void Tick()
        {
            time = Time.unscaledTime;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            if (contentRect.width <= 0 || contentRect.height <= 0) return;
            var painter = context.painter2D;
            painter.lineCap = LineCap.Round;
            foreach (var reach in reaches)
            {
                float density = reach.Falling ? settings.waterfallDensity : settings.riverDensity;
                float speed = reach.Falling ? settings.waterfallSpeed : settings.riverSpeed;
                float opacity = reach.Falling ? settings.waterfallOpacity : settings.riverOpacity;
                int count = Mathf.Clamp(Mathf.RoundToInt(reach.Count * density), 1, reach.Count * 3);
                for (int i = 0; i < count; i++)
                {
                    float phase = Mathf.Repeat(i * .618034f + time * reach.Speed * speed / reach.Length, 1);
                    float fade = Mathf.Sin(phase * Mathf.PI);
                    float lane = Mathf.Sin(i * 12.9898f) * reach.Width;
                    Vector2 point = reach.Sample(phase * reach.Length);
                    Vector2 end = reach.Sample(Mathf.Min(phase * reach.Length + (reach.Falling ? settings.waterfallStreakLength : 7), reach.Length));
                    // Screen-horizontal ripples on pools; vertical streaks on waterfalls.
                    Vector2 offset = reach.Falling ? new Vector2(lane, 0) : new Vector2(0, lane);
                    point += offset;
                    end += offset;
                    if (!reach.Falling) end = point + new Vector2(5 + i % 5, -1);
                    painter.lineWidth = reach.Falling ? settings.waterfallLineWidth : settings.riverLineWidth;
                    painter.strokeColor = new Color(.79f, .97f, 1f, Mathf.Clamp01(settings.intensity * opacity * fade * (.65f + .35f * Mathf.Sin(time * 1.4f + i))));
                    painter.BeginPath();
                    painter.MoveTo(point);
                    painter.QuadraticCurveTo((point + end) * .5f + new Vector2(0, 1.5f), end);
                    painter.Stroke();
                }
            }
            DrawFoam(painter, new Vector2(403, 282), 31, 10, 0);
            DrawFoam(painter, new Vector2(1012, 899), 27, 8, .47f);
        }

        private void DrawFoam(Painter2D painter, Vector2 center, float width, float height, float offset)
        {
            int rings = Mathf.Clamp(settings.foamRings, 1, 10);
            for (int i = 0; i < rings; i++)
            {
                float phase = Mathf.Repeat(time * .42f * settings.foamSpeed + i / (float)rings + offset, 1);
                float radius = (.45f + phase * .65f) * settings.foamSpread;
                painter.lineWidth = settings.foamLineWidth;
                painter.strokeColor = new Color(.87f, .98f, 1, Mathf.Clamp01(settings.intensity * settings.foamOpacity * Mathf.Sin(phase * Mathf.PI)));
                painter.BeginPath();
                painter.MoveTo(center + new Vector2(-width * radius, 0));
                painter.BezierCurveTo(center + new Vector2(-width * radius, height * radius),
                    center + new Vector2(width * radius, height * radius), center + new Vector2(width * radius, 0));
                painter.Stroke();
            }
        }

        public void Dispose()
        {
            animation.Pause();
            generateVisualContent -= Draw;
        }

        private sealed class Reach
        {
            private readonly Vector2[] points;
            public readonly float Width, Speed, Length;
            public readonly int Count;
            public readonly bool Falling;

            public Reach(Vector2[] points, float width, float speed, int count, bool falling)
            {
                this.points = points;
                Width = width;
                Speed = speed;
                Count = count;
                Falling = falling;
                for (int i = 1; i < points.Length; i++) Length += Vector2.Distance(points[i - 1], points[i]);
            }

            public Vector2 Sample(float distance)
            {
                for (int i = 1; i < points.Length; i++)
                {
                    float length = Vector2.Distance(points[i - 1], points[i]);
                    if (distance <= length) return Vector2.Lerp(points[i - 1], points[i], distance / length);
                    distance -= length;
                }
                return points[points.Length - 1];
            }
        }
    }
}
