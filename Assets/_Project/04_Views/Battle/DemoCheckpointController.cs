using System;
using System.Linq;
using Diceforge.Core;
using Diceforge.Map;
using Diceforge.Progression;
using UnityEngine;

namespace Diceforge.View
{
    // Persists only settled presentations; it never turns an in-flight animation into a save point.
    public sealed class DemoCheckpointController : MonoBehaviour
    {
        private BattleDebugController battle;
        private BattleRunner runner;
        private GameModePreset preset;
        private string chapter, run, node;
        private bool dirty = true, enabledWatching;
        private int expectedEvidence;
        public bool Resumed { get; private set; }
        public bool ResumeFailed { get; private set; }
        public bool SavePending { get; private set; }
        public bool RunChanged { get; private set; }
        public DemoCheckpoint Restored { get; private set; }

        public void Configure(BattleDebugController controller, BattleRunner source, GameModePreset definition)
        {
            battle = controller; runner = source; preset = definition;
            chapter = MapFlowRuntime.IsMapBattleActive ? MapFlowRuntime.ChapterId : null;
            node = MapFlowRuntime.IsMapBattleActive ? MapFlowRuntime.SelectedNodeId : null;
            run = chapter == null ? null : MapProgressService.GetRunId(chapter);
            var saved = ProfileService.Current.demoCheckpoint;
            if (saved == null || saved.levelId != preset.demoLevel.levelId) return;
            bool receiptExists = ProfileService.Current.progressionReceipts.Any(r => r.operationId == saved.operationId);
            if (!receiptExists && saved.Matches(preset, runner.Rules, chapter, run, node) &&
                runner.TryRestoreDemoCheckpoint(saved.battle, out _))
            {
                Restored = JsonUtility.FromJson<DemoCheckpoint>(JsonUtility.ToJson(saved));
                Resumed = true; dirty = false;
                battle.RestoreDemoSession(saved.operationId, saved.selectedHeroId);
                return;
            }
            ResumeFailed = !receiptExists;
            var candidate = ProfileService.Snapshot(); candidate.demoCheckpoint = null;
            candidate.demoCheckpointActive = false;
            SavePending = !ProfileService.TryCommit(candidate, out _, false);
        }

        public void BeginWatching()
        {
            enabledWatching = true;
            battle.OnHumanMoveApplied += MoveApplied;
            battle.OnDemoRestarted += Restarted;
        }
        private void MoveApplied(MoveRecord _) { dirty = true; expectedEvidence = battle.DemoNarrative.EvidenceCount + 1; }
        private void Restarted() { Resumed = false; Restored = null; ResumeFailed = false; expectedEvidence = 0; dirty = true; }
        public void MarkDirty() => dirty = true;

        private void LateUpdate()
        {
            if (!enabledWatching || !dirty || battle.DemoNarrative?.Ready != true || battle.PresentationIsAnimating ||
                battle.DemoNarrative.EvidenceCount < expectedEvidence) return;
            // Result commit removes the checkpoint in the same atomic transaction.
            if (battle.RewardSession?.CommitResult?.Succeeded == true) { dirty = false; return; }
            RetrySave();
        }
        public void RetrySave()
        {
            if (battle.RewardSession?.CommitResult?.Succeeded == true)
            {
                SavePending = false; dirty = false;
                battle.DemoNarrative.RefreshSaveStatus();
                return;
            }
            if (battle.DemoNarrative?.Ready != true || battle.PresentationIsAnimating ||
                battle.DemoNarrative.EvidenceCount < expectedEvidence) return;
            if (chapter != null && ProfileService.Current.chapters.Find(c => c.chapterId == chapter)?.runId != run)
            {
                SavePending = true; RunChanged = true; dirty = false;
                battle.DemoNarrative.RefreshSaveStatus();
                return;
            }
            var heroes = preset.demoLevel.heroIds.Select(id =>
            {
                if (!battle.TryGetHero(id, out int cell, out _, out bool exited))
                    throw new InvalidOperationException("Cannot checkpoint an unbound demo hero: " + id);
                return new DemoHeroCheckpoint { id = id, cell = cell, exited = exited };
            }).ToArray();
            var candidate = ProfileService.Snapshot();
            var learned = battle.DemoNarrative.ExportLearning();
            candidate.demoLearning.RemoveAll(s => s.levelId == learned.levelId);
            candidate.demoLearning.Add(learned);
            candidate.demoGuidanceHidden = learned.guidanceHidden;
            candidate.demoCheckpoint = new DemoCheckpoint
            {
                scenarioSignature = DemoCheckpoint.Signature(preset, runner.Rules), levelId = preset.demoLevel.levelId,
                chapterId = chapter, runId = run, nodeId = node, operationId = battle.RewardSession.OperationId,
                selectedHeroId = battle.SelectedHeroId, heroes = heroes, battle = runner.CaptureDemoCheckpoint(),
                learning = learned, completionLinesSeen = battle.DemoNarrative.CompletionLinesSeen
            };
            candidate.demoCheckpointActive = true;
            SavePending = !ProfileService.TryCommit(candidate, out _, false);
            dirty = false; // Retry is explicit after an IO failure, never a disk write every frame.
            battle.DemoNarrative.RefreshSaveStatus();
        }
        private void OnDestroy()
        {
            if (battle == null) return;
            battle.OnHumanMoveApplied -= MoveApplied; battle.OnDemoRestarted -= Restarted;
        }
    }
}
