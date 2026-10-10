using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Diceforge.View
{
    internal sealed class WorldAtmosphere : IDisposable
    {
        private readonly VisualElement root;
        private readonly Image[] islands = new Image[3];
        private readonly VisualElement[] weather = new VisualElement[3];
        private readonly Image[] clouds = new Image[3];
        private readonly Sprite[] sprites = new Sprite[3];
        private readonly Image trailingCloud;
        private readonly IVisualElementScheduledItem animation;
        private float phase;
        internal int AnimationFrames { get; private set; }
        [Serializable] private sealed class Regions { public Region[] regions; }
        [Serializable] private sealed class Region { public float x, y, width, height; }

        internal WorldAtmosphere(VisualElement root)
        {
            this.root = root;
            var texture = Resources.Load<Texture2D>("WorldSelection/WorldClouds");
            var regions = JsonUtility.FromJson<Regions>(Resources.Load<TextAsset>("WorldSelection/WorldCloudsRegions").text).regions;
            for (int index = 0; index < 3; index++)
            {
                var rect = regions[index];
                sprites[index] = Sprite.Create(texture, new Rect(rect.x, rect.y, rect.width, rect.height), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
                islands[index] = root.Q<Image>("worldIsland" + index);
                var host = new VisualElement { name = "worldWeather" + index, pickingMode = PickingMode.Ignore };
                host.AddToClassList("world-weather");
                var cloud = new Image { sprite = sprites[index], scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                cloud.AddToClassList("world-cloud");
                host.Add(cloud);
                weather[index] = host; clouds[index] = cloud;
                int kind = index;
                if (kind > 0) host.generateVisualContent += context => DrawPrecipitation(context.painter2D, host.contentRect.size, kind);
                islands[index].parent.Add(host);
            }
            trailingCloud = new Image { sprite = sprites[0], scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            trailingCloud.AddToClassList("world-cloud-trailing");
            islands[0].parent.Add(trailingCloud);
            animation = root.schedule.Execute(Animate).Every(33);
            animation.Pause();
        }

        internal void Resume() => animation.Resume();
        internal void Pause() => animation.Pause();

        private void Animate()
        {
            if (root.ClassListContains("world-hidden")) return;
            phase = Mathf.Repeat(Time.realtimeSinceStartup, 3600);
            AnimationFrames++;
            bool portrait = root.ClassListContains("world-portrait");
            for (int index = 0; index < 3; index++)
            {
                float t = phase * .65f + index * 2.1f;
                islands[index].style.translate = new Translate(0, Mathf.Sin(t) * (portrait ? 2 : 4));
                islands[index].style.rotate = new Rotate(Angle.Degrees(Mathf.Sin(t * .73f) * .45f));
                float cloudTime = phase * (.18f + index * .025f) + index * 1.7f;
                float travel = islands[index].layout.width * .13f;
                weather[index].style.translate = new Translate(Mathf.Sin(cloudTime) * travel, Mathf.Cos(cloudTime * .83f) * (portrait ? 3 : 5));
                if (index > 0) weather[index].MarkDirtyRepaint();
            }
            trailingCloud.style.translate = new Translate(Mathf.Sin(phase * .13f + 2) * islands[0].layout.width * .08f, Mathf.Cos(phase * .23f) * 3);
        }

        private void DrawPrecipitation(Painter2D painter, Vector2 size, int kind)
        {
            if (size.x < 1 || size.y < 1) return;
            bool snow = kind == 2;
            int count = snow ? 34 : 42;
            painter.lineCap = LineCap.Round;
            for (int index = 0; index < count; index++)
            {
                float seed = Mathf.Repeat(index * .618034f, 1);
                float progress = Mathf.Repeat(phase * (snow ? .25f : .90f) + index * .137f, 1);
                float alpha = Mathf.Sin(progress * Mathf.PI) * (snow ? 1f : .92f);
                float x = size.x * (.12f + seed * .70f);
                float y = size.y * (.24f + progress * .68f);
                x += snow ? Mathf.Sin(phase * .9f + index * 2) * 7 : progress * -7;
                painter.strokeColor = snow ? new Color(.86f, .94f, 1, alpha) : new Color(.27f, .50f, .66f, alpha);
                painter.lineWidth = snow ? 1.6f : 1.8f;
                painter.BeginPath();
                if (snow)
                {
                    float radius = 2.1f + seed * 1.8f;
                    for (int branch = 0; branch < 3; branch++)
                    {
                        float angle = branch * Mathf.PI / 3 + phase * .25f + index;
                        var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                        painter.MoveTo(new Vector2(x, y) - direction);
                        painter.LineTo(new Vector2(x, y) + direction);
                    }
                }
                else
                {
                    painter.MoveTo(new Vector2(x, y));
                    painter.LineTo(new Vector2(x - 2, y + 10 + seed * 5));
                }
                if (snow)
                {
                    painter.strokeColor = new Color(.36f, .57f, .72f, alpha * .42f);
                    painter.lineWidth = 3.2f;
                    painter.Stroke();
                    painter.strokeColor = new Color(.95f, .98f, 1, alpha);
                    painter.lineWidth = 1.6f;
                }
                painter.Stroke();
            }
        }

        public void Dispose()
        {
            animation.Pause();
            foreach (var sprite in sprites) if (sprite != null) UnityEngine.Object.Destroy(sprite);
        }
    }
}
