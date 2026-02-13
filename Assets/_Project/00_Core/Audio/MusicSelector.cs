using System;
using System.Collections.Generic;
using UnityEngine;

namespace Diceforge.Audio
{
    public sealed class MusicSelector
    {
        public TrackDef PickNextTrack(IReadOnlyList<TrackDef> enabledTracks, PlayerMusicPrefs prefs, string currentTrackId)
        {
            if (enabledTracks == null || enabledTracks.Count == 0)
                return null;

            var notDisliked = new List<TrackDef>(enabledTracks.Count);
            for (int i = 0; i < enabledTracks.Count; i++)
            {
                TrackDef track = enabledTracks[i];
                if (track == null)
                    continue;

                bool isDisliked = prefs != null && prefs.GetVote(track.id) == TrackVote.Dislike;
                if (!isDisliked)
                    notDisliked.Add(track);
            }

            IReadOnlyList<TrackDef> source = notDisliked.Count > 0 ? notDisliked : enabledTracks;

            var withoutCurrent = new List<TrackDef>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                TrackDef track = source[i];
                if (track == null)
                    continue;

                if (!string.Equals(track.id, currentTrackId, StringComparison.Ordinal))
                    withoutCurrent.Add(track);
            }

            IReadOnlyList<TrackDef> finalPool = withoutCurrent.Count > 0 ? withoutCurrent : source;
            int index = UnityEngine.Random.Range(0, finalPool.Count);
            return finalPool[index];
        }
    }
}
