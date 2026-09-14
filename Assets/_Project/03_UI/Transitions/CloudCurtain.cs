using UnityEngine;
using UnityEngine.UIElements;

namespace Diceforge.Transitions
{
    internal sealed class CloudCurtain
    {
        private readonly VisualElement root;
        private readonly VisualElement backing;
        private readonly VisualElement[] clouds = new VisualElement[8];
        private readonly CloudTransitionSettings settings;
        private float coverage;
        private bool opening;
        private Vector2 lastSize;

        public CloudCurtain(VisualElement root, CloudTransitionSettings settings)
        {
            this.root = root;
            this.settings = settings;
            root.style.overflow = Overflow.Hidden;
            backing = new VisualElement { pickingMode = PickingMode.Ignore };
            Fill(backing);
            Color opaqueColor = settings.coverageColor;
            opaqueColor.a = 1;
            backing.style.backgroundColor = opaqueColor;
            root.Add(backing);
            for (int i = 0; i < clouds.Length; i++)
            {
                var cloud = new VisualElement
                {
                    name = "Cloud" + i,
                    pickingMode = PickingMode.Ignore,
                    usageHints = UsageHints.DynamicTransform
                };
                cloud.style.position = Position.Absolute;
                cloud.style.backgroundImage = settings.cloudTexture;
                cloud.style.unityBackgroundImageTintColor = settings.cloudTint;
                float scale = 1 + (i % 3) * 0.045f;
                cloud.style.scale = new Scale(new Vector3(i % 2 == 0 ? scale : -scale, scale, 1));
                clouds[i] = cloud;
                root.Add(cloud);
            }
            root.RegisterCallback<GeometryChangedEvent>(_ => SetCoverage(coverage, opening));
            SetCoverage(0, false);
        }

        public static void Fill(VisualElement element)
        {
            element.style.position = Position.Absolute;
            element.style.left = element.style.top = element.style.right = element.style.bottom = 0;
        }

        public void SetCoverage(float value, bool isOpening)
        {
            coverage = Mathf.Clamp01(value);
            opening = isOpening;
            float width = root.contentRect.width;
            float height = root.contentRect.height;
            if (width <= 0 || height <= 0) return;
            bool simple = settings.reducedMotion || settings.cloudTexture == null;
            // An opaque backing guarantees concealment even at unusual aspect ratios.
            backing.style.opacity = simple ? coverage : Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.72f, 0.98f, coverage));
            float size = Mathf.Max(width, height) * 1.12f;
            bool resized = lastSize != new Vector2(width, height);
            lastSize = new Vector2(width, height);
            for (int i = 0; i < clouds.Length; i++)
            {
                var cloud = clouds[i];
                cloud.style.display = simple ? DisplayStyle.None : DisplayStyle.Flex;
                float phase = ((opening ? 7 - i : i) * 3 % 8) / 7f * Mathf.Clamp(settings.stagger, 0, 0.4f);
                float t = Mathf.Clamp01((coverage - phase) / (1 - phase));
                t = Mathf.SmoothStep(0, 1, t);
                float angle = i * Mathf.PI / 4;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 center = new Vector2(width / 2, height / 2);
                Vector2 closed = center + new Vector2(direction.x * width * 0.38f, direction.y * height * 0.38f);
                float horizontalExit = Mathf.Abs(direction.x) < 0.001f ? float.PositiveInfinity
                    : (width / 2 + size * 0.6f) / Mathf.Abs(direction.x);
                float verticalExit = Mathf.Abs(direction.y) < 0.001f ? float.PositiveInfinity
                    : (height / 2 + size * 0.6f) / Mathf.Abs(direction.y);
                Vector2 outside = center + direction * Mathf.Min(horizontalExit, verticalExit);
                Vector2 position = Vector2.Lerp(outside, closed, t);
                if (resized) cloud.style.width = cloud.style.height = size;
                cloud.style.translate = new Translate(position.x - size / 2, position.y - size / 2);
            }
        }
    }
}
