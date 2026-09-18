using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Diceforge.Map
{
    /// <summary>Shared-atlas animation extracted from the supplied green-screen footage.</summary>
    public sealed class WoodlandSpriteHeroPresenter : MonoBehaviour
    {
        private const float RunFramesPerSecond = 24f;
        private readonly Sprite[] run = new Sprite[20];
        private readonly Sprite[] idle = new Sprite[10];
        private Image image;
        private float startedAt;
        private bool moving;
        private bool loaded;
        private Sprite displayed;

        public void Show(Image target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            StopPresentation();
            if (!loaded)
            {
                foreach (var sprite in Resources.LoadAll<Sprite>("Map/HeroSprites/goblin-atlas"))
                {
                    if (sprite.name.StartsWith("run_") && int.TryParse(sprite.name.Substring(4), out int r) && r < run.Length) run[r] = sprite;
                    if (sprite.name.StartsWith("idle_") && int.TryParse(sprite.name.Substring(5), out int i) && i < idle.Length) idle[i] = sprite;
                }
                loaded = Array.TrueForAll(run, frame => frame != null) && Array.TrueForAll(idle, frame => frame != null);
                if (!loaded) { Debug.LogError("[WoodlandMap] Incomplete hero sprite atlas.", this); return; }
            }
            image = target;
            image.scaleMode = ScaleMode.ScaleToFit;
            image.pickingMode = PickingMode.Ignore;
            startedAt = Time.unscaledTime;
            ShowFrame(idle[0]);
        }

        public void SetMoving(bool isMoving, bool faceLeft = false)
        {
            if (moving != isMoving) startedAt = Time.unscaledTime;
            moving = isMoving;
            if (image != null) image.style.scale = new Scale(new Vector3(faceLeft ? -1f : 1f, 1f, 1f));
        }

        private void Update()
        {
            if (image == null) return;
            if (image.panel == null) { StopPresentation(); return; }
            if (!image.visible || image.resolvedStyle.display == DisplayStyle.None) return;
            float elapsed = Time.unscaledTime - startedAt;
            if (moving)
            {
                ShowFrame(run[(int)(elapsed * RunFramesPerSecond) % run.Length]);
                return;
            }
            // Hold the standing pose, then play a short reversible blink from the original footage.
            float phase = elapsed % 3.2f;
            int index = phase < 2f ? 0 : Mathf.Min(18, (int)((phase - 2f) * 15f));
            ShowFrame(idle[index <= 9 ? index : 18 - index]);
        }

        private void ShowFrame(Sprite frame)
        {
            if (displayed == frame) return;
            displayed = frame;
            image.sprite = frame;
        }

        public void StopPresentation()
        {
            if (image != null)
            {
                image.sprite = null;
                image.style.scale = new Scale(Vector3.one);
            }
            image = null;
            displayed = null;
            moving = false;
        }

        private void OnDisable() => StopPresentation();
        private void OnDestroy() => StopPresentation();
    }
}
