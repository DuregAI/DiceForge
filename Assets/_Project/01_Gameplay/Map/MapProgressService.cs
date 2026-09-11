using System;
using System.IO;
using Diceforge.Progression;
using UnityEngine;

namespace Diceforge.Map
{
    public static class MapProgressService
    {
        private const string KeyPrefix = "map_state_";

        public static MapRunState Load(MapDefinitionSO map)
        {
            var profile = ProfileService.Snapshot();
            var chapter = profile.chapters.Find(x => x.chapterId == map.chapterId);
            if (chapter != null) return Clone(chapter.state);
            string json = PlayerPrefs.GetString(KeyPrefix + map.chapterId, string.Empty);
            MapRunState state;
            if (string.IsNullOrWhiteSpace(json)) state = CreateNewRun(map);
            else
            {
                state = JsonUtility.FromJson<MapRunState>(json);
                if (state == null || state.completedNodeIds == null || state.unlockedNodeIds == null ||
                    !json.Contains("\"completedNodeIds\"") || !json.Contains("\"unlockedNodeIds\""))
                    throw new InvalidDataException("Не удалось импортировать прогресс главы " + map.chapterId);
                if (string.IsNullOrEmpty(state.currentNodeId)) state.currentNodeId = map.startNodeId;
                state.Unlock(map.startNodeId);
            }
            profile.chapters.Add(new ChapterProgress { chapterId = map.chapterId, runId = Guid.NewGuid().ToString("N"), state = state });
            if (!ProfileService.TryCommit(profile, out string error)) throw new IOException(error);
            return Clone(state);
        }

        public static string GetRunId(string chapterId)
        {
            Load(MapDefinitionSO.LoadChapter(chapterId));
            return ProfileService.Current.chapters.Find(x => x.chapterId == chapterId).runId;
        }

        public static void Save(string chapterId, MapRunState state)
        {
            var profile = ProfileService.Snapshot();
            var chapter = profile.chapters.Find(x => x.chapterId == chapterId);
            if (chapter == null)
            {
                chapter = new ChapterProgress { chapterId = chapterId, runId = Guid.NewGuid().ToString("N") };
                profile.chapters.Add(chapter);
            }
            chapter.state = Clone(state);
            if (!ProfileService.TryCommit(profile, out string error)) throw new IOException(error);
        }

        public static void Reset(string chapterId)
        {
            var profile = ProfileService.Snapshot();
            profile.chapters.RemoveAll(x => x.chapterId == chapterId);
            profile.chapters.Add(new ChapterProgress
            {
                chapterId = chapterId, runId = Guid.NewGuid().ToString("N"), state = CreateNewRun(MapDefinitionSO.LoadChapter(chapterId))
            });
            if (!ProfileService.TryCommit(profile, out string error)) throw new IOException(error);
        }

        private static MapRunState Clone(MapRunState state) => JsonUtility.FromJson<MapRunState>(JsonUtility.ToJson(state));
        private static MapRunState CreateNewRun(MapDefinitionSO map)
        {
            var state = new MapRunState { currentNodeId = map.startNodeId };
            state.Unlock(map.startNodeId);
            return state;
        }
    }
}