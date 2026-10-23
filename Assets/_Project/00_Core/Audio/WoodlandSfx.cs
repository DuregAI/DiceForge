using System;
using System.Collections.Generic;
using Diceforge.Transitions;
using UnityEngine;

namespace Diceforge.Audio
{
    public enum WoodlandCue
    {
        WindowOpen, WindowClose, TransitionClose, TransitionOpen,
        Jump, Land, Footstep, LevelComplete, Retry, TokenExit, TokenHit, DiceRoll
    }

    /// <summary>Small original foley bank; all playback uses the saved SFX volume.</summary>
    public static class WoodlandSfx
    {
        private sealed class Cue
        {
            internal readonly string prefix;
            internal readonly float gain, interval;
            internal AudioClip[] clips;
            internal int previous = -1;
            internal double nextTime = -1;
            internal Cue(string prefix, float gain, float interval)
            { this.prefix = prefix; this.gain = gain; this.interval = interval; }
        }

        private static readonly Cue[] Cues =
        {
            new("window_open_", .65f, .12f), new("window_close_", .65f, .12f),
            new("transition_close_", .65f, .18f), new("transition_open_", .65f, .18f),
            new("jump_", .62f, .10f), new("land_", .72f, .10f),
            new("footstep_", .58f, .13f), new("level_complete_", .85f, 1.5f),
            new("retry_", .78f, 1.2f), new("token_exit_", .68f, .22f),
            new("token_hit_", .70f, .14f), new("dice_roll_", .66f, .24f)
        };
        private static System.Random variation = new(461091);
        private static bool loaded;
        private static int burstFrame = -1, burstCount;
        public static int LoadedClipCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            loaded = false;
            LoadedClipCount = 0;
            burstFrame = -1;
            variation = new System.Random(461091);
            foreach (var cue in Cues) { cue.clips = null; cue.previous = -1; cue.nextTime = -1; }
        }

        public static void Preload()
        {
            if (loaded) return;
            loaded = true;
            var clips = Resources.LoadAll<AudioClip>("Audio/Woodland");
            Array.Sort(clips, (a, b) => string.CompareOrdinal(a.name, b.name));
            LoadedClipCount = clips.Length;
            foreach (var cue in Cues)
            {
                var matches = new List<AudioClip>();
                foreach (var clip in clips)
                    if (clip.name.StartsWith(cue.prefix, StringComparison.Ordinal)) matches.Add(clip);
                cue.clips = matches.ToArray();
            }
        }

        public static void Play(WoodlandCue id, float gain = 1f)
        {
            var manager = AudioManager.Instance;
            if (!Application.isPlaying || manager == null || manager.IsMuted || manager.SfxVolume <= 0 || gain <= 0) return;
            bool movement = id == WoodlandCue.Jump || id == WoodlandCue.Land || id == WoodlandCue.Footstep || id == WoodlandCue.DiceRoll;
            bool window = id == WoodlandCue.WindowOpen || id == WoodlandCue.WindowClose;
            if ((movement || window) && ScreenTransition.IsBusy) return;
            if (movement && Time.timeScale <= 0) return;
            Preload();
            var cue = Cues[(int)id];
            double now = Time.realtimeSinceStartupAsDouble;
            if (cue.clips.Length == 0 || now < cue.nextTime) return;
            if (burstFrame != Time.frameCount) { burstFrame = Time.frameCount; burstCount = 0; }
            if (burstCount >= 3) return;

            // Do not consume Unity's gameplay random stream or repeat the last variant.
            int index = cue.clips.Length > 1 ? variation.Next(cue.clips.Length - 1) : 0;
            if (cue.clips.Length > 1 && index >= cue.previous) index++;
            cue.previous = index;
            cue.nextTime = now + cue.interval;
            burstCount++;
            manager.PlayGameSfx(cue.clips[index], cue.gain * Mathf.Clamp01(gain));
        }
    }
}
