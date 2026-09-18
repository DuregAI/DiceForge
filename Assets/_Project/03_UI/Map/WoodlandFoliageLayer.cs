using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Diceforge.Map
{
    public sealed class WoodlandFoliageLayer : VisualElement, IDisposable
    {
        private readonly WoodlandFoliageSettings settings;
        private readonly Image[] plants = new Image[3];
        private readonly IVisualElementScheduledItem animation;
        private float phase;
        private float lastTime;

        public WoodlandFoliageLayer(WoodlandFoliageSettings settings = null)
        {
            this.settings = settings ?? new WoodlandFoliageSettings();
            name = "WoodlandFoliage";
            pickingMode = PickingMode.Ignore;
            AddToClassList("wm-foliage");
            Texture2D texture = Resources.Load<Texture2D>("Map/Foliage/woodland-shrub");
            if (texture == null) Debug.LogWarning("Woodland foliage texture is missing.");
            Vector2[] bases = { new(380, 600), new(687, 637), new(1192, 350) };
            float[] widths = { 105, 83, 122 };
            for (int i = 0; i < plants.Length; i++)
            {
                float height = texture != null ? widths[i] * texture.height / texture.width : widths[i];
                var plant = new Image { name = "WoodlandShrub" + (i + 1), image = texture, pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit };
                plant.AddToClassList("wm-shrub");
                plant.style.width = widths[i];
                plant.style.height = height;
                plant.style.left = bases[i].x - widths[i] * .5f;
                plant.style.top = bases[i].y - height;
                plant.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(98), 0);
                plant.tintColor = new Color(.87f, .9f, .77f, 1);
                plants[i] = plant;
                Add(plant);
            }
            lastTime = Time.unscaledTime;
            animation = schedule.Execute(Tick).Every(33);
            Tick();
        }

        public void SetActive(bool active)
        {
            if (active)
            {
                lastTime = Time.unscaledTime;
                animation.Resume();
            }
            else animation.Pause();
        }

        private void Tick()
        {
            float now = Time.unscaledTime;
            phase += Mathf.Clamp(now - lastTime, 0, .1f) * settings.speed;
            lastTime = now;
            style.opacity = settings.enabled ? settings.opacity : 0;
            for (int i = 0; i < plants.Length; i++)
            {
                float wind = Mathf.Sin(phase * (1.15f + i * .12f) + i * 2.1f);
                float gust = Mathf.Sin(phase * .43f + i) * Mathf.Sin(phase * 2.3f + i * .7f);
                float angle = (wind + gust * settings.gustStrength) * settings.swayDegrees;
                plants[i].style.rotate = new Rotate(Angle.Degrees(angle));
                plants[i].style.scale = new Scale(new Vector3(settings.size, settings.size, 1));
            }
        }

        public void Dispose() => animation.Pause();
    }
}
