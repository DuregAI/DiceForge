using UnityEngine;
using UnityEngine.UIElements;

// Coordinates refer to the authored background, then follow its centered cover scaling.
internal sealed class MenuCharacterMotion
{
    private readonly Texture2D flag = Resources.Load<Texture2D>("GlimblehopMenu/finish-flag");
    private readonly Texture2D blink = Resources.Load<Texture2D>("GlimblehopMenu/background-blink");
    private readonly Texture2D secondBlink = Resources.Load<Texture2D>("GlimblehopMenu/hat-blink-source");
    private readonly MenuAmbientSettings settings;
    private readonly System.Random random = new System.Random();
    private readonly float[] nextBlink = { -1, -1 };

    public MenuCharacterMotion(MenuAmbientSettings settings) => this.settings = settings;

    public void Draw(MeshGenerationContext context, float width, float height, float time)
    {
        float scale = Mathf.Max(width / 1672f, height / 941f);
        Vector2 offset = new Vector2((width - 1672 * scale) / 2, (height - 941 * scale) / 2);
        if (flag != null)
        {
            Patch(context, flag, new Rect(1493, 130, 137, 114), new Rect(106f / 1536, 69f / 1024, 1357f / 1536, 885f / 1024), offset, scale, settings.flagEnabled ? time : 0, true, 1);

        }
        float close = BlinkAmount(time, 0);
        if (blink != null && settings.blinkEnabled && close > 0)
        {
            // Both foreground eyes close together; the rest of the face stays still.
            Rect eye = new Rect(786, 403, 81, 78);
            Patch(context, blink, eye, new Rect(eye.x / 1672, eye.y / 941, eye.width / 1672, eye.height / 941), offset, scale, 0, false, close);
            Rect otherEye = new Rect(883, 449, 62, 59);
            Patch(context, blink, otherEye, new Rect(otherEye.x / 1672, otherEye.y / 941, otherEye.width / 1672, otherEye.height / 941), offset, scale, 0, false, close);
        }
        float secondClose = BlinkAmount(time, 1);
        if (secondBlink != null && settings.secondGoblinBlinkEnabled && secondClose > 0)
        {
            EyePatch(context, new Rect(1328, 431, 45, 42), offset, scale, secondClose);
            EyePatch(context, new Rect(1380, 421, 34, 43), offset, scale, secondClose);
        }
    }

    private void EyePatch(MeshGenerationContext context, Rect eye, Vector2 offset, float scale, float close)
    {
        Patch(context, secondBlink, eye, new Rect(eye.x / 1672, eye.y / 941, eye.width / 1672, eye.height / 941), offset, scale, 0, false, close);
    }

    private float BlinkAmount(float time, int index)
    {
        if (nextBlink[index] < 0 || time > nextBlink[index] + 0.22f)
        {
            float min = Mathf.Clamp(Mathf.Min(settings.blinkPauseRange.x, settings.blinkPauseRange.y), 0.5f, 60);
            float max = Mathf.Clamp(Mathf.Max(settings.blinkPauseRange.x, settings.blinkPauseRange.y), min, 60);
            nextBlink[index] = time + Mathf.Lerp(min, max, (float)random.NextDouble());
        }
        float age = time - nextBlink[index];
        if (age < 0) return 0;
        // Fast closing, brief opaque hold, then opening: avoids a ghostly half-visible pupil.
        return Mathf.Min(Mathf.Clamp01(age / 0.045f), Mathf.Clamp01((0.22f - age) / 0.075f));
    }

    private static void Patch(MeshGenerationContext context, Texture2D texture, Rect rect, Rect uv,
        Vector2 offset, float scale, float time, bool waving, float opacity)
    {
        const int columns = 16;
        const int rows = 8;
        var mesh = context.Allocate((columns + 1) * (rows + 1), columns * rows * 6, texture);
        Rect atlas = mesh.uvRegion;
        for (int y = 0; y <= rows; y++)
        for (int x = 0; x <= columns; x++)
        {
            float u = (float)x / columns;
            float v = (float)y / rows;
            // Rope attachments stay still; motion grows towards the free pennant tip.
            float wave = waving ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.16f, 1, u)) * Mathf.Sin(u * 9 - time * 2.8f) * 8f * (0.7f + 0.3f * Mathf.Sin(time * 0.6f)) : 0;
            float edge = waving ? 1 : Mathf.Clamp01(Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v)) * 8);
            mesh.SetNextVertex(new Vertex
            {
                position = new Vector3(offset.x + (rect.x + u * rect.width) * scale, offset.y + (rect.y + v * rect.height + wave) * scale, Vertex.nearZ),
                tint = new Color(1, 1, 1, opacity * edge),
                uv = new Vector2(atlas.x + (uv.x + u * uv.width) * atlas.width, atlas.y + (1 - uv.y - v * uv.height) * atlas.height)
            });
        }
        for (int y = 0; y < rows; y++)
        for (int x = 0; x < columns; x++)
        {
            ushort a = (ushort)(y * (columns + 1) + x);
            ushort b = (ushort)(a + columns + 1);
            mesh.SetNextIndex(a); mesh.SetNextIndex((ushort)(a + 1)); mesh.SetNextIndex(b);
            mesh.SetNextIndex((ushort)(a + 1)); mesh.SetNextIndex((ushort)(b + 1)); mesh.SetNextIndex(b);
        }
    }
}
