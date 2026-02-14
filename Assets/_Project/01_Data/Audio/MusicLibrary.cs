using System.Collections.Generic;
using UnityEngine;

namespace Diceforge.Audio
{
    [CreateAssetMenu(fileName = "MusicLibrary_Default", menuName = "Diceforge/Audio/Music Library")]
    public sealed class MusicLibrary : ScriptableObject
    {
        [SerializeField] private List<TrackDef> tracks = new();

        public bool TryGetById(string id, out TrackDef def)
        {
            if (!string.IsNullOrEmpty(id))
            {
                for (int i = 0; i < tracks.Count; i++)
                {
                    TrackDef candidate = tracks[i];
                    if (candidate != null && candidate.id == id)
                    {
                        def = candidate;
                        return true;
                    }
                }
            }

            def = null;
            return false;
        }

        public IReadOnlyList<TrackDef> GetEnabledTracks()
        {
            return GetTracksForContext(MusicContext.All);
        }

        public IReadOnlyList<TrackDef> GetTracksForContext(MusicContext context)
        {
            var result = new List<TrackDef>();
            for (int i = 0; i < tracks.Count; i++)
            {
                TrackDef track = tracks[i];
                if (track == null)
                    continue;

                if (!track.enabled || track.clip == null)
                    continue;

                if (!IsAllowedInContext(track, context))
                    continue;

                result.Add(track);
            }

            return result;
        }

        public bool IsTrackAllowedInContext(string trackId, MusicContext context)
        {
            if (!TryGetById(trackId, out TrackDef def) || def == null)
                return false;

            return IsAllowedInContext(def, context);
        }

        public string GetDisplayName(string id)
        {
            if (TryGetById(id, out TrackDef def))
            {
                if (!string.IsNullOrWhiteSpace(def.displayName))
                    return def.displayName;

                if (!string.IsNullOrWhiteSpace(def.id))
                    return def.id;
            }

            return $"Unknown ({id})";
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            var seen = new HashSet<string>();

            for (int i = 0; i < tracks.Count; i++)
            {
                TrackDef track = tracks[i];
                if (track == null)
                    continue;

                if (string.IsNullOrWhiteSpace(track.id))
                {
                    Debug.LogWarning($"[MusicLibrary] Track at index {i} has empty id.", this);
                }
                else if (!seen.Add(track.id))
                {
                    Debug.LogWarning($"[MusicLibrary] Duplicate track id '{track.id}'.", this);
                }

                if (track.clip == null)
                    Debug.LogWarning($"[MusicLibrary] Track '{track.id}' has null AudioClip.", this);
            }
        }
#endif

        private static bool IsAllowedInContext(TrackDef track, MusicContext context)
        {
            if (track == null)
                return false;

            MusicContext allowedContexts = track.allowedContexts;
            if (allowedContexts == MusicContext.None)
                allowedContexts = MusicContext.All;

            return (allowedContexts & context) != 0;
        }
    }
}
