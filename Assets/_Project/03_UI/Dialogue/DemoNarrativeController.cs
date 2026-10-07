using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Diceforge.Core;
using Diceforge.Dialogue;
using Diceforge.Progression;
using Diceforge.View;
using UnityEngine;
using UnityEngine.UIElements;

namespace Diceforge.UI.Dialogue
{
    public sealed class DemoNarrativeController : MonoBehaviour
    {
        private BattleDebugController battle;
        private DemoNarrativeCatalog catalog;
        private DemoLearningPolicy learning;
        private readonly HashSet<string> storySeen = new();
        private readonly HashSet<string> observedMoves = new();
        private UIDocument document;
        private VisualElement root, story, hintPanel, helpPanel;
        private Label heading, storyText, hintText, speaker, page, saveError;
        private Image art, portrait;
        private DemoStoryScene activeScene;
        private DemoDialogueEvent[] sequence;
        private DemoDialogueEvent hint;
        private int frame, line, level, generation, previousHazard;
        private string language;
        private bool started, initialized, sawBlocked;
        private Coroutine opening;
        public bool IsModal { get; private set; }
        public bool IsStoryVisible => IsModal && (activeScene != null || sequence != null);
        public string StoryId => activeScene?.id;
        public int StoryFrame => frame;
        public string PendingHintId => learning?.State.pendingHintId;
        public int EvidenceCount => learning?.Moves ?? 0;
        public bool SavePending { get; private set; }
        public bool Ready => started;
        public bool CompletionLinesSeen { get; private set; }
        public DemoLearningState ExportLearning() => JsonUtility.FromJson<DemoLearningState>(JsonUtility.ToJson(learning.State));
        public DemoCompletionProgress ExportCompletion() => new DemoCompletionProgress { learning = ExportLearning(), storySeen = storySeen.ToArray() };
        private bool Russian => PlayerPrefs.GetString("ui.language", "en") == "ru";
        private string T(string ru, string en) => Russian ? ru : en;

        public void Configure(BattleDebugController controller)
        {
            battle = controller;
            catalog = Resources.Load<DemoNarrativeCatalog>("DemoRC/Narrative");
            if (catalog == null) throw new InvalidOperationException("Demo RC narrative catalog is missing. Import story and dialogue.");
            level = int.Parse(battle.DemoLevel.levelId.Substring(1));
            var profile = ProfileService.Current;
            profile.demoLearning ??= new(); profile.demoStorySeen ??= new();
            var saved = profile.demoLearning.Find(s => s.levelId == battle.DemoLevel.levelId);
            if (battle.DemoCheckpoint?.Resumed == true) saved = battle.DemoCheckpoint.Restored.learning;
            learning = new DemoLearningPolicy(saved == null ? new DemoLearningState { levelId = battle.DemoLevel.levelId }
                : JsonUtility.FromJson<DemoLearningState>(JsonUtility.ToJson(saved)), battle.DemoCheckpoint?.Resumed == true);
            sawBlocked = learning.State.sawBlocked;
            CompletionLinesSeen = battle.DemoCheckpoint?.Restored?.completionLinesSeen == true;
            learning.State.guidanceHidden = profile.demoGuidanceHidden;
            foreach (string id in profile.demoStorySeen) storySeen.Add(id);
            battle.OnHumanMoveApplied += MoveApplied;
            battle.OnDemoStepSelected += StepSelected;
            battle.OnDemoInputRejected += InputRejected;
            battle.OnDemoBlockedPreview += BlockedPreview;
            battle.OnDemoRestarted += Restarted;
            previousHazard = battle.PresentationState.TrailHazardCell;
            IsModal = true;
            opening = StartCoroutine(OpenWhenReady());
        }

        private IEnumerator OpenWhenReady()
        {
            yield return null;
            while (GetComponent<DioramaHud>()?.Document == null) yield return null;
            var uiObject = new GameObject("DemoNarrativeUI");
            uiObject.transform.SetParent(transform, false);
            document = uiObject.AddComponent<UIDocument>();
            document.panelSettings = Resources.Load<PanelSettings>("WoodlandPanel");
            document.visualTreeAsset = Resources.Load<VisualTreeAsset>("DemoNarrative");
            document.sortingOrder = 20;
            root = document.rootVisualElement; root.pickingMode = PickingMode.Ignore;
            root.style.position = Position.Absolute;
            root.style.left = root.style.right = root.style.top = root.style.bottom = 0;
            story = root.Q("demoStory"); hintPanel = root.Q("demoHint"); helpPanel = root.Q("demoHelp");
            heading = root.Q<Label>("storyHeading"); storyText = root.Q<Label>("storyText"); page = root.Q<Label>("storyPage");
            hintText = root.Q<Label>("hintText"); speaker = root.Q<Label>("hintSpeaker");
            saveError = root.Q<Label>("demoSaveError"); art = root.Q<Image>("storyArt"); portrait = root.Q<Image>("hintPortrait");
            root.Q<Button>("storyNext").clicked += AdvanceStory;
            root.Q<Button>("storySkip").clicked += SkipStory;
            root.Q<Button>("demoHelpButton").clicked += OpenHelp;
            root.Q<Button>("hintSkip").clicked += () => SkipHint(false);
            root.Q<Button>("hideGuidance").clicked += () => SkipHint(true);
            root.Q<Button>("helpClose").clicked += () => helpPanel.style.display = DisplayStyle.None;
            root.Q<Button>("replayStory").clicked += ReplayStory;
            root.Q<Button>("guidanceOn").clicked += () => { learning.State.guidanceHidden = false; Save(); OpenHelp(); };
            root.Q<Button>("retryDemoSave").clicked += () => { Save(); battle.DemoCheckpoint?.RetrySave(); };
            initialized = true; language = null;
            RefreshLanguage();
            if (battle.DemoCheckpoint?.Resumed == true)
            {
                IsModal = false; started = true; opening = null;
                if (!battle.IsMatchEnded)
                    Show(catalog.Find(learning.State.pendingHintId) ?? catalog.Find(Prefix + "R01"), false);
                Save(); yield break;
            }
            if (!learning.State.introSeen)
            {
                if (!storySeen.Contains("S" + (level - 1).ToString("00")))
                { PlayScene(level - 1); while (IsModal) yield return null; }
                else IsModal = false;
                PlayLines("I"); while (IsModal) yield return null;
                learning.State.introSeen = true; Save();
            }
            else { IsModal = false; Show(catalog.Find(Prefix + "R01"), false); }
            started = true; IsModal = false; opening = null;
            Evaluate("story_finished");
            if (hint == null) Evaluate("step_selected");
            if (hint == null) Evaluate("step_offer_ready");
            if (battle.DemoCheckpoint?.ResumeFailed == true) Show(catalog.Find("G_R01"), false);
        }

        private string Prefix => "L" + level.ToString("00") + "_";
        private void Update()
        {
            if (!initialized || root == null || learning == null || battle == null || battle.PresentationState == null) return;
            root.EnableInClassList("portrait", Screen.height > Screen.width);
            string current = PlayerPrefs.GetString("ui.language", "en");
            if (current != language) { language = current; RefreshLanguage(); }
            if (IsStoryVisible && UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true) SkipStory();
            if (started && !battle.PresentationIsAnimating && !IsModal && !DioramaHud.BlocksGameplay)
            {
                int hazard = battle.PresentationState.TrailHazardCell;
                if (hazard != previousHazard)
                {
                    previousHazard = hazard;
                    Evaluate(battle.PresentationState.TrailHazardYielded ? "hazard_yielded" : "hazard_moved");
                }
                if (battle.IsPassingTrailTurn) ShowOnce(catalog.Find("G_E06"), false);
            }
        }

        private void RefreshLanguage()
        {
            var font = Resources.Load<Font>("Localization/RobotoSlab");
            if (font != null) root.style.unityFontDefinition = FontDefinition.FromFont(font);
            root.Q<Button>("storyNext").text = T("Дальше", "Next");
            root.Q<Button>("storySkip").text = T("Пропустить сцену", "Skip scene");
            root.Q<Button>("demoHelpButton").text = T("Подсказка", "Help");
            root.Q<Button>("hintSkip").text = T("Закрыть", "Close");
            root.Q<Button>("hideGuidance").text = T("Скрыть обучение", "Hide guidance");
            root.Q<Button>("helpClose").text = T("Закрыть справку", "Close help");
            root.Q<Button>("replayStory").text = T("Повторить историю", "Replay story");
            root.Q<Button>("guidanceOn").text = T("Включить обучение", "Enable guidance");
            root.Q<Button>("retryDemoSave").text = T("Повторить сохранение", "Retry save");
            if (hint != null) RenderHint();
            if (IsStoryVisible) RenderStory();
            BuildHelp();
        }

        private void PlayScene(int index)
        {
            activeScene = catalog.Scene(index); sequence = null; frame = line = 0;
            IsModal = true; hintPanel.style.display = DisplayStyle.None; helpPanel.style.display = DisplayStyle.None;
            story.style.display = DisplayStyle.Flex; RenderStory();
        }
        private void PlayLines(string kind)
        {
            sequence = catalog.events.Where(e => e.id.StartsWith(Prefix + kind, StringComparison.Ordinal)).OrderBy(e => e.id).ToArray();
            activeScene = null; line = 0; frame = 0;
            if (sequence.Length == 0) { sequence = null; IsModal = false; return; }
            IsModal = true; story.style.display = DisplayStyle.Flex; RenderStory();
        }
        private void RenderStory()
        {
            root.Q<Button>("demoHelpButton").SetEnabled(false);
            root.Q<Button>("storyNext").Focus();
            art.style.display = activeScene == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (activeScene != null)
            {
                var current = activeScene.frames[frame];
                art.image = activeScene.art;
                art.sourceRect = new Rect(activeScene.art.width * frame / 3f, 0, activeScene.art.width / 3f, activeScene.art.height);
                art.scaleMode = ScaleMode.ScaleToFit;
                art.MarkDirtyRepaint();
                heading.text = activeScene.title + " · " + current.title;
                storyText.text = string.Join("\n\n", current.lines.Select(l => catalog.Speaker(l.speakerId, Russian) + ":\n" + l.ru));
                page.text = (frame + 1) + " / 3";
            }
            else
            {
                var current = sequence[line];
                heading.text = catalog.Speaker(current.speakerId, Russian);
                storyText.text = current.Text(Russian); page.text = (line + 1) + " / " + sequence.Length;
            }
        }
        public void AdvanceStory()
        {
            if (!IsStoryVisible) return;
            if (activeScene != null && ++frame < activeScene.frames.Length) { RenderStory(); return; }
            if (sequence != null && ++line < sequence.Length) { RenderStory(); return; }
            SkipStory();
        }
        public void SkipStory()
        {
            if (!IsStoryVisible) return;
            if (activeScene != null) storySeen.Add(activeScene.id);
            else if (sequence != null && sequence.Length > 0 && sequence[0].id.StartsWith(Prefix + "V", StringComparison.Ordinal)) CompletionLinesSeen = true;
            activeScene = null; sequence = null; IsModal = false;
            story.style.display = DisplayStyle.None; Save();
            root.Q<Button>("demoHelpButton").SetEnabled(true);
        }
        public void ReplayStory()
        {
            if (IsModal || battle.PresentationIsAnimating || battle.IsMatchEnded) return;
            PlayScene(level - 1);
        }
        public IEnumerator PlayCompletion(MatchResult result)
        {
            if (!initialized) yield break;
            hint = null; hintPanel.style.display = DisplayStyle.None; helpPanel.style.display = DisplayStyle.None;
            if (result.Winner != PlayerId.A || !CompletionLinesSeen)
                PlayLines(result.Winner == PlayerId.A ? "V" : "F");
            while (IsModal) yield return null;
            if (result.Winner == PlayerId.A)
            {
                if (!storySeen.Contains("S" + level.ToString("00")))
                { PlayScene(level); while (IsModal) yield return null; }
                if (level == 6)
                {
                    // The final result stays readable after either Next or Skip closes the wedding scene.
                    Show(new DemoDialogueEvent { speakerId = "luma", ru = "Общий свет вернулся. Свадьба состоялась.",
                        en = "The shared light is back. The wedding took place." }, false);
                }
            }
        }

        private DemoLearningContext Context()
        {
            int Position(string id) => battle.TryGetHero(id, out int cell, out _, out bool exited) && !exited ? cell : -1;
            var state = battle.PresentationState;
            int selected = battle.PresentationSelectedDie.HasValue && battle.PresentationSelectedDie.Value < battle.PresentationDice.Count
                ? battle.PresentationDice[battle.PresentationSelectedDie.Value] : 0;
            var c = new DemoLearningContext { tish = Position("tish"), luma = Position("luma"), bum = Position("bum"),
                selectedStep = selected, remaining = battle.PresentationDice.Count, hazard = state.TrailHazardCell,
                hazardYielded = state.TrailHazardYielded, exited = state.BorneOffA, active = battle.TotalStonesPerPlayer - state.BorneOffA,
                moves = learning.Moves, hero = battle.SelectedHeroId, legalMoves = battle.PresentationHasLegalMove ? 1 : 0 };
            var positions = battle.DemoLevel.heroIds.Select(Position).Where(p => p >= 0).ToArray();
            c.shared = positions.Distinct().Count() < positions.Length;
            c.anyExit = positions.Any(p => battle.PresentationDice.Any(s => p + s >= 8));
            return c;
        }
        private void Evaluate(string domain, DemoLearningContext context = null)
        {
            if (!initialized || IsModal || battle.PresentationIsAnimating) return;
            var c = context ?? Context();
            var candidates = catalog.events.Where(e => e.levelId == battle.DemoLevel.levelId && e.kind == "tutorial" && e.domainEvent == domain)
                .OrderByDescending(e => e.priority);
            foreach (var e in candidates)
            {
                int number = int.Parse(e.id.Substring(e.id.Length - 2));
                bool instruction = DemoLearningPolicy.IsAutomaticInstruction(level, number) && !(level == 6 && number == 1);
                if (!string.IsNullOrEmpty(e.helpTopic) || !DemoLearningPolicy.Eligible(level, number, c, sawBlocked)) continue;
                // L6 only shows its goal automatically; familiar rules remain in contextual help.
                if (level == 6 && number != 1 && number != 3) continue;
                if (ShowOnce(e, instruction)) break;
            }
        }
        private bool ShowOnce(DemoDialogueEvent e, bool instruction)
        {
            if (e == null || !learning.ShowOnce(e.id, instruction)) return false;
            Show(e, true); return true;
        }
        private void Show(DemoDialogueEvent e, bool persist)
        {
            if (e == null) return;
            hint = e; learning.State.pendingHintId = e.id; hintPanel.style.display = DisplayStyle.Flex;
            RenderHint(); if (persist) Save();
        }
        private void RenderHint()
        {
            speaker.text = hint.speakerId == "luma" && level <= 2 ? T("Совет Лумы", "Luma's note") : catalog.Speaker(hint.speakerId, Russian);
            hintText.text = hint.Text(Russian);
            portrait.sprite = Array.Find(catalog.speakers, s => s.id == hint.speakerId)?.neutralPortrait;
            portrait.style.display = portrait.sprite == null ? DisplayStyle.None : DisplayStyle.Flex;
        }
        public void SkipHint(bool all)
        {
            learning.Skip(all); hint = null; hintPanel.style.display = DisplayStyle.None; Save();
        }
        public void OpenHelp()
        {
            if (!initialized || IsModal) return;
            learning.Help(); BuildHelp(); helpPanel.style.display = DisplayStyle.Flex;
        }
        private void BuildHelp()
        {
            var topics = root.Q("helpTopics"); topics.Clear();
            var c = Context();
            foreach (var e in catalog.events.Where(e => e.levelId == battle.DemoLevel.levelId && !string.IsNullOrEmpty(e.helpTopic)))
            {
                int number = int.Parse(e.id.Substring(e.id.Length - 2));
                if (!DemoLearningPolicy.Eligible(level, number, c, sawBlocked)) continue;
                var button = new Button(() => { learning.Help(); Show(e, false); helpPanel.style.display = DisplayStyle.None; });
                button.text = e.Text(Russian); button.AddToClassList("help-topic"); topics.Add(button);
            }
        }
        private void StepSelected()
        {
            if (!started || IsModal || battle.PresentationIsAnimating) return;
            ClearHint(); Evaluate("step_selected");
            if (hint == null) Evaluate("step_offer_ready");
        }
        private void BlockedPreview(string hero)
        {
            if (!started || IsModal || battle.PresentationIsAnimating) return;
            var c = Context(); c.hero = hero; c.blocked = true;
            Evaluate("illegal_destination_previewed", c); sawBlocked = true;
            learning.State.sawBlocked = true; Save();
        }
        private void InputRejected(DemoInputRejection reason, string hero)
        {
            if (!started || IsModal) return;
            string instance = battle.PresentationState.TurnIndex + ":" + battle.PresentationDice.Count + ":" + battle.PresentationSelectedDie;
            if (!learning.CanNarrateError(reason, instance, hero)) return;
            var c = Context(); c.rejection = reason; c.hero = hero;
            var specific = catalog.events.Where(e => e.levelId == battle.DemoLevel.levelId && e.domainEvent == "input_rejected" && e.kind == "tutorial")
                .FirstOrDefault(e => DemoLearningPolicy.Eligible(level, int.Parse(e.id.Substring(e.id.Length - 2)), c, sawBlocked));
            string fallback = reason switch { DemoInputRejection.NoStepSelected => "G_E01", DemoInputRejection.EmptyOrigin or DemoInputRejection.UnknownTarget => "G_E02",
                DemoInputRejection.HeroAlreadyExited => "G_E03", DemoInputRejection.BlockedDestination => "G_E04", DemoInputRejection.MovementAnimation => "G_E07", _ => "G_E09" };
            Show(specific ?? catalog.Find(fallback), true);
        }
        private void MoveApplied(MoveRecord record)
        {
            if (!record.Move.HasValue || record.ApplyResult == ApplyResult.Illegal || !initialized) return;
            string hero = battle.SelectedHeroId;
            if (!observedMoves.Add(record.TurnIndex + ":" + hero + ":" + record.FromCell + ":" + record.PipUsed)) return;
            int hazard = battle.PresentationState.TrailHazardCell;
            ClearHint(); StartCoroutine(AfterMove(record, hero, hazard, generation));
        }
        private IEnumerator AfterMove(MoveRecord record, string hero, int hazard, int moveGeneration)
        {
            yield return null;
            while (battle.PresentationIsAnimating && moveGeneration == generation) yield return null;
            if (moveGeneration != generation || !battle.TryGetHero(hero, out int cell, out _, out bool exited)) yield break;
            bool changed = record.Move.Value.Kind == MoveKind.BearOff ? exited : cell == record.ToCell && cell != record.FromCell;
            bool jumped = record.FromCell < hazard && cell > hazard && hazard >= 0;
            if (!learning.ObserveMove(record.Move.HasValue, changed, hero, record.FromCell ?? -1, record.PipUsed ?? 0, exited, jumped)) yield break;
            Save();
            if (IsModal || battle.IsMatchEnded) yield break;
            var c = Context(); c.from = record.FromCell ?? -1; c.to = cell; c.step = record.PipUsed ?? 0; c.hero = hero;
            Evaluate(exited ? "hero_exited" : "legal_move_applied", c);
            if (hint == null) Evaluate("step_offer_ready");
            if (learning.UnassistedMoves >= 3 && hint != null && DemoLearningPolicy.IsAutomaticInstruction(level, int.TryParse(hint.id?.Substring(hint.id.Length - 2), out int n) ? n : 0)) ClearHint();
        }
        private void ClearHint() { hint = null; learning.State.pendingHintId = null; if (hintPanel != null) hintPanel.style.display = DisplayStyle.None; }
        private void Restarted()
        {
            generation++; StopAllCoroutines(); opening = null;
            if (story != null) story.style.display = DisplayStyle.None;
            activeScene = null; sequence = null; IsModal = false; ClearHint();
            learning.Restart(); previousHazard = battle.PresentationState.TrailHazardCell; sawBlocked = false;
            CompletionLinesSeen = false;
            observedMoves.Clear();
            if (!initialized) { IsModal = true; opening = StartCoroutine(OpenWhenReady()); return; }
            started = true; Show(catalog.Find(Prefix + "R02"), true);
        }
        private void Save()
        {
            var candidate = ProfileService.Snapshot(); candidate.demoLearning ??= new(); candidate.demoStorySeen ??= new();
            candidate.demoLearning.RemoveAll(s => s.levelId == learning.State.levelId);
            candidate.demoLearning.Add(JsonUtility.FromJson<DemoLearningState>(JsonUtility.ToJson(learning.State)));
            candidate.demoGuidanceHidden = learning.State.guidanceHidden;
            foreach (string id in storySeen) if (!candidate.demoStorySeen.Contains(id)) candidate.demoStorySeen.Add(id);
            if (started && !battle.PresentationIsAnimating && candidate.demoCheckpoint?.operationId == battle.RewardSession?.OperationId &&
                candidate.demoCheckpoint.learning.boardRevision == learning.State.boardRevision)
            {
                candidate.demoCheckpoint.learning = ExportLearning();
                candidate.demoCheckpoint.completionLinesSeen = CompletionLinesSeen;
            }
            SavePending = !ProfileService.TryCommit(candidate, out string error, false);
            RefreshSaveStatus();
        }
        public void RefreshSaveStatus()
        {
            if (battle.RewardSession?.CommitResult?.Succeeded == true) SavePending = false;
            if (saveError != null)
            {
                bool pending = SavePending || battle.DemoCheckpoint?.SavePending == true;
                bool changed = battle.DemoCheckpoint?.RunChanged == true;
                saveError.text = changed ? T("Прохождение обновилось. Продолжи с карты.", "The campaign changed. Continue from the map.") :
                    pending ? T("Не удалось сохранить прогресс. Можно повторить сохранение.", "Could not save progress. Retry is available.") : string.Empty;
                root.Q<Button>("retryDemoSave").SetEnabled(!changed);
                root.Q("demoSave").style.display = pending ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
        private void OnDestroy()
        {
            if (battle == null) return;
            battle.OnHumanMoveApplied -= MoveApplied; battle.OnDemoStepSelected -= StepSelected;
            battle.OnDemoInputRejected -= InputRejected; battle.OnDemoBlockedPreview -= BlockedPreview; battle.OnDemoRestarted -= Restarted;
        }
    }
}
