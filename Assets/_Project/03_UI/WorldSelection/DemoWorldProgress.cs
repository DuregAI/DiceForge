using System;
using Diceforge.Map;
using Diceforge.Progression;

namespace Diceforge.View
{
    public static class DemoWorldProgress
    {
        public static bool IsWoodlandComplete
        {
            get
            {
                var chapter = ProfileService.Current.chapters.Find(item => item.chapterId == "Chapter1");
                if (chapter?.state == null) return false;
                for (int level = 1; level <= 6; level++)
                    if (!chapter.state.IsCompleted("C1_0" + level)) return false;
                return true;
            }
        }

        public static bool TryStartReplay()
        {
            if (!IsWoodlandComplete) return false;
            var candidate = ProfileService.Snapshot();
            var chapter = candidate.chapters.Find(item => item.chapterId == "Chapter1");
            var state = new MapRunState { currentNodeId = "C1_01" };
            state.Unlock("C1_01");
            chapter.state = state;
            chapter.runId = Guid.NewGuid().ToString("N");
            for (int level = 1; level <= 6; level++)
            {
                string levelId = "L" + level;
                candidate.demoLearning?.RemoveAll(item => item.levelId == levelId);
                candidate.demoStorySeen?.Remove("S0" + level);
            }
            candidate.demoStorySeen?.Remove("S00");
            candidate.demoCheckpoint = null;
            candidate.demoCheckpointActive = false;
            return ProfileService.TryCommit(candidate, out _, false);
        }
    }
}
