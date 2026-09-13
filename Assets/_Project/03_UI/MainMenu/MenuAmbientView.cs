using System;
using UnityEngine;
using UnityEngine.UIElements;

[Serializable]
public sealed class MenuAmbientSettings
{
    [Header("Dust / sparks")]
    [Range(0, 400)] public int dustCount = 220;
    [Range(0, 4)] public float dustSpeed = 1;
    [Range(0.25f, 3)] public float dustSize = 1;
    [Range(0, 1)] public float dustOpacity = 0.33f;

    [Header("Leaves")]
    [Range(0, 50)] public int leafCount = 20;
    [Tooltip("Seconds for one full fall. Larger values mean slower leaves.")]
    [Range(3, 60)] public float leafFallSeconds = 18;
    [Range(0.25f, 3)] public float leafSize = 1;
    [Range(0, 1)] public float leafOpacity = 0.7f;
}

// Presentation only: one non-interactive drawing layer, no spawned GameObjects.
internal sealed class MenuAmbientView : IDisposable
{
    private readonly VisualElement menu;
    private readonly VisualElement layer;
    private readonly VisualElement[] entrance;
    private readonly IVisualElementScheduledItem tick;
    private readonly MenuAmbientSettings settings;
    private float elapsed;
    private float dustTime;
    private float leafCycle = 4f / 18f;
    private float lastTime;
    private readonly float entranceStart;
    private bool entranceFinished;

    public MenuAmbientView(VisualElement root, MenuAmbientSettings settings)
    {
        this.settings = settings ?? new MenuAmbientSettings();
        menu = root.Q("MenuPanel");
        if (menu == null) return;
        layer = new VisualElement { name = "MenuAtmosphere", pickingMode = PickingMode.Ignore };
        layer.style.position = Position.Absolute;
        layer.style.left = layer.style.top = layer.style.right = layer.style.bottom = 0;
        layer.style.overflow = Overflow.Hidden;
        menu.Insert(0, layer);
        layer.generateVisualContent += Draw;
        entrance = new[] { root.Q("GlimblehopLogo"), root.Q("GlimblehopTagline"), root.Q("ModeButtonsColumn") };
        UpdateEntrance(0);
        lastTime = Time.realtimeSinceStartup;
        entranceStart = lastTime;
        tick = layer.schedule.Execute(Update).Every(33);
    }

    private void Update()
    {
        float now = Time.realtimeSinceStartup;
        float dt = Mathf.Clamp(now - lastTime, 0, 0.1f);
        lastTime = now;
        if (!entranceFinished) UpdateEntrance(menu.enabledInHierarchy ? now - entranceStart : 1);
        // Freeze while unfocused or covered by a modal; don't jump on return.
        if (!Application.isFocused || !menu.enabledInHierarchy || menu.resolvedStyle.display == DisplayStyle.None)
            return;
        elapsed += dt;
        dustTime += dt * Mathf.Clamp(settings.dustSpeed, 0, 4);
        leafCycle = Mathf.Repeat(leafCycle + dt / Mathf.Clamp(settings.leafFallSeconds, 3, 60), 1);
        layer.MarkDirtyRepaint();
    }

    private void UpdateEntrance(float time)
    {
        for (int i = 0; i < entrance.Length; i++)
        {
            if (entrance[i] == null) continue;
            float t = Mathf.Clamp01((time - i * 0.10f) / 0.55f);
            float eased = 1 - Mathf.Pow(1 - t, 3);
            entrance[i].style.opacity = eased;
            entrance[i].style.translate = new Translate(0, (1 - eased) * 18);
        }
        entranceFinished = time >= 0.75f;
    }

    private void Draw(MeshGenerationContext context)
    {
        float width = layer.contentRect.width;
        float height = layer.contentRect.height;
        if (width <= 0 || height <= 0) return;
        var painter = context.painter2D;
        float scale = Mathf.Min(width / 1920f, height / 1080f);
        // Fixed phases keep the animation repeatable without touching gameplay RNG.
        for (int i = 0; i < Mathf.Clamp(settings.dustCount, 0, 400); i++)
        {
            float phase = i * 2.39996f;
            float x = width * (0.46f + Mathf.Repeat(i * 0.137f, 0.49f)) + Mathf.Sin(dustTime * 0.19f + phase) * 15 * scale;
            float y = height * Mathf.Repeat(i * 0.173f - dustTime * (0.008f + i % 3 * 0.002f), 1);
            float alpha = (0.13f + 0.20f * (0.5f + 0.5f * Mathf.Sin(elapsed * 0.7f + phase))) * Mathf.Sin(Mathf.PI * y / height) * Mathf.Clamp01(settings.dustOpacity) / 0.33f;
            float radius = (1.7f + i % 3 * 0.6f) * scale * Mathf.Clamp(settings.dustSize, 0.25f, 3);
            painter.fillColor = new Color(1, 0.89f, 0.58f, alpha * 0.2f);
            Oval(painter, x, y, radius * 2.5f, radius * 2.5f);
            painter.fillColor = new Color(1, 0.94f, 0.72f, alpha);
            Oval(painter, x, y, radius, radius);
        }
        // Evenly stagger falls; bounded lanes keep every leaf on the illustration.
        int leafCount = Mathf.Clamp(settings.leafCount, 0, 50);
        for (int i = 0; i < leafCount; i++)
        {
            float phase = Mathf.Repeat(leafCycle + (float)i / leafCount, 1);
            float lane = Mathf.Repeat(i * 0.618034f, 1);
            float x = width * (0.62f + lane * 0.31f - phase * 0.10f) + Mathf.Sin(phase * 14 + i) * 24 * scale;
            float y = height * (-0.08f + phase * 1.16f);
            float angle = Mathf.Sin(phase * 12 + i) * 0.75f + phase * 2;
            float size = (i % 2 == 0 ? 12 : 9) * scale * Mathf.Clamp(settings.leafSize, 0.25f, 3);
            Vector2 axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * size;
            Vector2 side = new Vector2(-axis.y, axis.x) * (0.30f + 0.20f * Mathf.Abs(Mathf.Cos(phase * 18)));
            Vector2 center = new Vector2(x, y);
            float alpha = Mathf.Clamp01(Mathf.Min(phase, 1 - phase) * 10) * Mathf.Clamp01(settings.leafOpacity);
            painter.fillColor = i % 2 == 0 ? new Color(0.67f, 0.48f, 0.16f, alpha) : new Color(0.52f, 0.60f, 0.22f, alpha);
            painter.BeginPath();
            painter.MoveTo(center - axis);
            painter.BezierCurveTo(center + side * 2, center + axis + side, center + axis);
            painter.BezierCurveTo(center - side * 2, center - axis - side, center - axis);
            painter.ClosePath();
            painter.Fill();
            painter.strokeColor = new Color(0.93f, 0.77f, 0.39f, alpha * 0.65f);
            painter.lineWidth = Mathf.Max(0.6f, scale);
            painter.BeginPath(); painter.MoveTo(center - axis * 0.8f); painter.LineTo(center + axis * 0.85f); painter.Stroke();
        }
    }

    private static void Oval(Painter2D painter, float x, float y, float rx, float ry)
    {
        const float k = 0.5522848f;
        painter.BeginPath(); painter.MoveTo(new Vector2(x + rx, y));
        painter.BezierCurveTo(new Vector2(x + rx, y + ry * k), new Vector2(x + rx * k, y + ry), new Vector2(x, y + ry));
        painter.BezierCurveTo(new Vector2(x - rx * k, y + ry), new Vector2(x - rx, y + ry * k), new Vector2(x - rx, y));
        painter.BezierCurveTo(new Vector2(x - rx, y - ry * k), new Vector2(x - rx * k, y - ry), new Vector2(x, y - ry));
        painter.BezierCurveTo(new Vector2(x + rx * k, y - ry), new Vector2(x + rx, y - ry * k), new Vector2(x + rx, y));
        painter.ClosePath(); painter.Fill();
    }

    public void Dispose()
    {
        tick?.Pause();
        if (layer == null) return;
        layer.generateVisualContent -= Draw;
        layer.RemoveFromHierarchy();
        UpdateEntrance(1);
    }
}
