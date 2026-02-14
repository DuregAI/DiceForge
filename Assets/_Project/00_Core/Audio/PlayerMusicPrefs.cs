using System;
using System.Collections.Generic;
using UnityEngine;

namespace Diceforge.Audio
{
    [Serializable]
    public sealed class PlayerMusicPrefs : ISerializationCallbackReceiver
    {
        [Serializable]
        private struct TrackVoteEntry
        {
            public string trackId;
            public TrackVote vote;
        }

        public float musicVolume = 0.7f;
        public float sfxVolume = 0.7f;

        [SerializeField] private List<TrackVoteEntry> voteEntries = new();

        [NonSerialized] private Dictionary<string, TrackVote> votes = new();

        public TrackVote GetVote(string trackId)
        {
            if (string.IsNullOrWhiteSpace(trackId))
                return TrackVote.Neutral;

            EnsureMap();
            return votes.TryGetValue(trackId, out TrackVote vote) ? vote : TrackVote.Neutral;
        }

        public bool SetVote(string trackId, TrackVote vote)
        {
            if (string.IsNullOrWhiteSpace(trackId))
                return false;

            EnsureMap();
            TrackVote current = GetVote(trackId);
            if (current == vote)
                return false;

            if (vote == TrackVote.Neutral)
                votes.Remove(trackId);
            else
                votes[trackId] = vote;

            return true;
        }

        public void ClearVote(string trackId)
        {
            if (string.IsNullOrWhiteSpace(trackId))
                return;

            EnsureMap();
            votes.Remove(trackId);
        }

        public int CountVotes(TrackVote vote)
        {
            EnsureMap();
            int count = 0;
            foreach (var pair in votes)
            {
                if (pair.Value == vote)
                    count++;
            }

            return count;
        }

        public void OnBeforeSerialize()
        {
            EnsureMap();
            voteEntries.Clear();

            foreach (var pair in votes)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                    continue;

                voteEntries.Add(new TrackVoteEntry
                {
                    trackId = pair.Key,
                    vote = pair.Value
                });
            }
        }

        public void OnAfterDeserialize()
        {
            votes = new Dictionary<string, TrackVote>();
            for (int i = 0; i < voteEntries.Count; i++)
            {
                TrackVoteEntry entry = voteEntries[i];
                if (string.IsNullOrWhiteSpace(entry.trackId))
                    continue;

                if (entry.vote == TrackVote.Neutral)
                    continue;

                votes[entry.trackId] = entry.vote;
            }
        }

        private void EnsureMap()
        {
            votes ??= new Dictionary<string, TrackVote>();
        }
    }
}
