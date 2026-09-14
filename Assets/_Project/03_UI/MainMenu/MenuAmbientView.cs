using System;
using UnityEngine;
using UnityEngine.UIElements;

[Serializable]
public sealed class MenuAmbientSettings
{
    [Header("Menu entrance (seconds)")]
    [Range(0, 3)] public float entranceDelay = 0.08f;
    [Range(0, 3)] public float entranceDuration = 0.8f;
    [Tooltip("Pause between the title, tagline and each button.")]
    [Range(0, 1)] public float entranceStagger = 0.16f;

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

    [Header("Distant birds")]
    public bool birdsEnabled = true;
    [Range(15, 90)] public float birdIntervalSeconds = 32;
    [Range(0.5f, 3)] public float birdSize = 1.8f;
    [Range(0, 1)] public float birdOpacity = 0.8f;
    [Header("Characters and flag")]
    public bool flagEnabled = true;
    public bool blinkEnabled = true;
    [Tooltip("Random pause between blinks, in seconds. Each goblin has an independent timer.")]
    public Vector2 blinkPauseRange = new Vector2(3, 8);
    public bool secondGoblinBlinkEnabled = true;
}

// Presentation only: one non-interactive drawing layer, no spawned GameObjects.
internal sealed class MenuAmbientView : IDisposable
{
    private readonly VisualElement menu;
    private readonly VisualElement layer;
    private readonly VisualElement[] entrance;
    private readonly IVisualElementScheduledItem tick;
    private readonly MenuAmbientSettings settings;
    private readonly MenuCharacterMotion characterMotion;
    private float elapsed;
    private float dustTime;
    private float leafCycle = 4f / 18f;
    private float birdTime = -2;
    private float lastTime;
    private float entranceStart = -1;
    private bool entranceFinished;

    public MenuAmbientView(VisualElement root, MenuAmbientSettings settings)
    {
        this.settings = settings ?? new MenuAmbientSettings();
        characterMotion = new MenuCharacterMotion(this.settings);
        menu = root.Q("MenuPanel");
        if (menu == null) return;
        layer = new VisualElement { name = "MenuAtmosphere", pickingMode = PickingMode.Ignore };
        layer.style.position = Position.Absolute;
        layer.style.left = layer.style.top = layer.style.right = layer.style.bottom = 0;
        layer.style.overflow = Overflow.Hidden;
        menu.Insert(0, layer);
        layer.generateVisualContent += Draw;
        entrance = new[] { root.Q("GlimblehopLogo"), root.Q("btnLong"), root.Q("btnTutorial") };
        foreach (var element in entrance) element?.AddToClassList("menu-entering");
        UpdateEntrance(0);
        lastTime = Time.realtimeSinceStartup;
        tick = layer.schedule.Execute(Update).Every(33);
    }

    private void Update()
    {
        float now = Time.realtimeSinceStartup;
        float dt = Mathf.Clamp(now - lastTime, 0, 0.1f);
        lastTime = now;
        if (!entranceFinished)
        {
            if (Diceforge.Transitions.ScreenTransition.IsBusy) { entranceStart = -1; return; }
            // Start after layout, so scene loading doesn't consume the entrance animation.
            if (entranceStart < 0) entranceStart = now;
            UpdateEntrance(menu.enabledInHierarchy ? now - entranceStart : float.PositiveInfinity);
        }
        // Freeze while unfocused or covered by a modal; don't jump on return.
        if (!Application.isFocused || !menu.enabledInHierarchy || menu.resolvedStyle.display == DisplayStyle.None)
            return;
        elapsed += dt;
        birdTime += dt;
        if (birdTime > Mathf.Clamp(settings.birdIntervalSeconds, 15, 90)) birdTime = 0;
        dustTime += dt * Mathf.Clamp(settings.dustSpeed, 0, 4);
        leafCycle = Mathf.Repeat(leafCycle + dt / Mathf.Clamp(settings.leafFallSeconds, 3, 60), 1);
        layer.MarkDirtyRepaint();
    }

    private void UpdateEntrance(float time)
    {
        if (entrance == null) return;
        for (int i = 0; i < entrance.Length; i++)
        {
            if (entrance[i] == null) continue;
            float delay = Mathf.Max(0, settings.entranceDelay) + i * Mathf.Max(0, settings.entranceStagger);
            float duration = Mathf.Max(0, settings.entranceDuration);
            float t = time < delay ? 0 : duration <= 0 ? 1 : Mathf.Clamp01((time - delay) / duration);
            float eased = 1 - Mathf.Pow(1 - t, 3);
            entrance[i].style.opacity = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t * 1.5f));
            entrance[i].style.translate = new Translate(0, (1 - eased) * (i == 0 ? 30 : 22));
            // Scale the title only; button hover/press scaling stays owned by USS.
            if (i == 0) entrance[i].style.scale = new Scale(Vector3.one * Mathf.Lerp(0.94f, 1, eased));
            if (t >= 1)
            {
                entrance[i].style.opacity = StyleKeyword.Null;
                entrance[i].style.translate = StyleKeyword.Null;
                if (i == 0) entrance[i].style.scale = StyleKeyword.Null;
                entrance[i].RemoveFromClassList("menu-entering");
            }
        }
        entranceFinished = time >= Mathf.Max(0, settings.entranceDelay)
            + (entrance.Length - 1) * Mathf.Max(0, settings.entranceStagger)
            + Mathf.Max(0, settings.entranceDuration);
    }

    private void Draw(MeshGenerationContext context)
    {
        float width = layer.contentRect.width;
        float height = layer.contentRect.height;
        if (width <= 0 || height <= 0) return;
        characterMotion.Draw(context, width, height, elapsed);
        var painter = context.painter2D;
        float scale = Mathf.Min(width / 1920f, height / 1080f);
        DrawBirds(painter, width, height, scale);
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

    private void DrawBirds(Painter2D painter, float width, float height, float scale)
    {
        if (!settings.birdsEnabled || birdTime < 0 || birdTime > 7) return;
        // Small distant silhouettes cross the sunlit clearing, never the menu text.
        for (int i = 0; i < 2; i++)
        {
            float t = (birdTime - i * 0.45f) / 6;
            if (t < 0 || t > 1) continue;
            float x = width * Mathf.Lerp(0.58f, 0.93f, t);
            float y = height * (0.12f + i * 0.025f - Mathf.Sin(t * Mathf.PI) * 0.025f);
            float birdScale = scale * Mathf.Clamp(settings.birdSize, 0.5f, 3);
            float wing = Mathf.Sin(birdTime * 9 + i) * 4 * birdScale;
            float size = (i == 0 ? 6 : 4.5f) * birdScale;
            float alpha = Mathf.Clamp01(Mathf.Min(t, 1 - t) * 8) * Mathf.Clamp01(settings.birdOpacity);
            painter.strokeColor = new Color(0.25f, 0.29f, 0.20f, alpha);
            painter.lineWidth = 1.5f * birdScale;
            painter.BeginPath();
            painter.MoveTo(new Vector2(x - size, y - wing));
            painter.QuadraticCurveTo(new Vector2(x - size * 0.4f, y - 2 * scale), new Vector2(x, y));
            painter.QuadraticCurveTo(new Vector2(x + size * 0.4f, y - 2 * scale), new Vector2(x + size, y - wing));
            painter.Stroke();
        }
    }

    public void Dispose()
    {
        tick?.Pause();
        if (layer == null) return;
        layer.generateVisualContent -= Draw;
        layer.RemoveFromHierarchy();
        UpdateEntrance(float.PositiveInfinity);
    }
}
