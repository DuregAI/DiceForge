using System;
using UnityEngine;

namespace Diceforge.Audio
{
    [Serializable]
    public sealed class TrackDef
    {
        [Tooltip("Stable id without spaces. Example: trk_deep_sector_drift")]
        public string id;

        [Tooltip("Display name used in UI")]
        public string displayName;

        public AudioClip clip;

        [Tooltip("Can be disabled without breaking saved votes")]
        public bool enabled = true;

        [Tooltip("Music contexts where this track is allowed. None means all contexts for backward compatibility.")]
        public MusicContext allowedContexts = MusicContext.All;

        public string[] tags;
    }
}
