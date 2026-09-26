using System;
using System.Collections;
using System.Collections.Generic;
using Diceforge.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Diceforge.View
{
    /// <summary>Presentation for the first eight-space solo trail; BattleRunner owns its rules and rolls.</summary>
    public sealed class FirstTrailPlayController : MonoBehaviour
    {
        [SerializeField] private Camera boardCamera;
        [SerializeField] private UIDocument hudDocument;
        [SerializeField] private Transform[] cells;
        [SerializeField] private Transform[] friends;

        private BattleRunner runner;
        private readonly int[] friendCells = { 0, 0 };
        private readonly bool[] exited = new bool[2];
        private readonly List<LineRenderer> availableRings = new List<LineRenderer>();
        private Label progressLabel;
        private Label hintLabel;
        private VisualElement stepChoices;
        private Button restartButton;
        private Material ringMaterial;
        private Coroutine moveRoutine;
        private bool animating;
        private bool ended;

        public void Configure(Camera camera, UIDocument document, Transform[] routeCells, Transform[] goblins)
        {
            boardCamera = camera;
            hudDocument = document;
            cells = routeCells;
            friends = goblins;
        }

        private void Start()
        {
            if (boardCamera == null || hudDocument == null || cells == null || cells.Length != 8 ||
                friends == null || friends.Length != 2)
            {
                Debug.LogError("First trail requires a camera, HUD, eight cells and two friends.", this);
                enabled = false;
                return;
            }

            BindHud();
            BuildPickTargets();
            BuildHighlights();
            InitializeMatch();
        }

        private void OnDestroy()
        {
            if (runner != null)
                runner.OnMatchEnded -= HandleMatchEnded;
            if (restartButton != null)
                restartButton.clicked -= Restart;
            if (ringMaterial != null)
                Destroy(ringMaterial);
        }

        private void InitializeMatch()
        {
            var rules = new RulesetConfig
            {
                rulesetId = "first_trail_solo",
                displayName = "Two friends on the first trail",
                gameMode = GameMode.SoloTrail,
                boardSize = 8,
                homeSize = 4,
                totalStonesPerPlayer = 2,
                startCellA = 0,
                startCellB = 7,
                moveDirA = 1,
                maxTurns = 80,
                verboseLog = false,
                headRules = new HeadRuleConfig { restrictHeadMoves = false }
            };
            var bag = new DiceBagConfigData(DiceBagDrawMode.Shuffled, new[]
            {
                new DiceOutcomeData("1 · 2", 2, new[] { 1, 2 }),
                new DiceOutcomeData("1 · 3", 1, new[] { 1, 3 }),
                new DiceOutcomeData("2 · 3", 1, new[] { 2, 3 })
            });
            runner = new BattleRunner();
            runner.OnMatchEnded += HandleMatchEnded;
            runner.Init(rules, bag, null, 1427);
            RestartPresentation();
        }

        private void BindHud()
        {
            var root = hudDocument.rootVisualElement;
            progressLabel = root.Q<Label>("trail-progress");
            hintLabel = root.Q<Label>("trail-hint");
            stepChoices = root.Q<VisualElement>("step-choices");
            restartButton = root.Q<Button>("trail-restart");
            if (progressLabel == null || hintLabel == null || stepChoices == null || restartButton == null)
                throw new InvalidOperationException("FirstTrailPlayHud.uxml is missing required elements.");
            restartButton.clicked += Restart;
        }

        private void BuildPickTargets()
        {
            for (int i = 0; i < cells.Length; i++)
            {
                var collider = cells[i].GetComponent<BoxCollider>();
                if (collider == null)
                    collider = cells[i].gameObject.AddComponent<BoxCollider>();
                collider.center = new Vector3(0, -.075f, 0);
                collider.size = new Vector3(.94f, .20f, .90f);
                var target = cells[i].GetComponent<FirstTrailPickTarget>();
                if (target == null)
                    target = cells[i].gameObject.AddComponent<FirstTrailPickTarget>();
                target.cellId = i;
                target.friendId = -1;
            }

            for (int i = 0; i < friends.Length; i++)
            {
                var collider = friends[i].GetComponent<CapsuleCollider>();
                if (collider == null)
                    collider = friends[i].gameObject.AddComponent<CapsuleCollider>();
                collider.center = new Vector3(0, .57f, 0);
                collider.height = 1.37f;
                collider.radius = .32f;
                var target = friends[i].GetComponent<FirstTrailPickTarget>();
                if (target == null)
                    target = friends[i].gameObject.AddComponent<FirstTrailPickTarget>();
                target.friendId = i;
                target.cellId = -1;
            }
        }

        private void BuildHighlights()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            ringMaterial = new Material(shader);
            ringMaterial.color = new Color(1f, .78f, .25f, 1f);
            for (int i = 0; i < cells.Length; i++)
            {
                var ring = new GameObject("Available origin " + i).AddComponent<LineRenderer>();
                ring.transform.SetParent(cells[i], false);
                ring.useWorldSpace = false;
                ring.loop = true;
                ring.positionCount = 40;
                ring.widthMultiplier = .035f;
                ring.material = ringMaterial;
                ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                for (int j = 0; j < 40; j++)
                {
                    float angle = 2 * Mathf.PI * j / 40;
                    ring.SetPosition(j, new Vector3(Mathf.Cos(angle) * .43f, .018f,
                        Mathf.Sin(angle) * .40f));
                }
                ring.enabled = false;
                availableRings.Add(ring);
            }
        }

        private void Update()
        {
            if (runner == null || animating || ended)
                return;

            Vector2 pointer;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                pointer = Mouse.current.position.ReadValue();
            else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                pointer = Touchscreen.current.primaryTouch.position.ReadValue();
            else
                return;

            if (IsOverHud(pointer))
                return;

            if (!Physics.Raycast(boardCamera.ScreenPointToRay(pointer), out RaycastHit hit, 90f))
                return;
            var target = hit.collider.GetComponentInParent<FirstTrailPickTarget>();
            if (target == null)
                return;

            int friendId = target.friendId;
            if (friendId < 0)
                friendId = FindFriendAtCell(target.cellId);
            if (friendId < 0 || exited[friendId])
            {
                hintLabel.text = "Выбери гоблина на подсвеченной плите";
                return;
            }

            int from = friendCells[friendId];
            int dieIndex = runner.SelectedDieIndex ?? 0;
            if (dieIndex < 0 || dieIndex >= runner.RemainingDice.Count)
                return;
            int steps = runner.RemainingDice[dieIndex];
            int to = from + steps;
            var move = to >= cells.Length ? Move.BearOff(from, steps) : Move.MoveStone(from, steps);
            if (!runner.TryApplyHumanMove(move))
            {
                hintLabel.text = "Сейчас этот ход недоступен";
                return;
            }

            animating = true;
            moveRoutine = StartCoroutine(AnimateMove(friendId, from, to));
            RefreshHud();
        }

        private bool IsOverHud(Vector2 screenPoint)
        {
            var root = hudDocument.rootVisualElement;
            if (root.panel == null)
                return false;
            Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(root.panel,
                new Vector2(screenPoint.x, Screen.height - screenPoint.y));
            var picked = root.panel.Pick(panelPoint);
            return picked != null && picked != root && picked.pickingMode != PickingMode.Ignore;
        }

        private int FindFriendAtCell(int cellId)
        {
            for (int i = 0; i < friendCells.Length; i++)
                if (!exited[i] && friendCells[i] == cellId)
                    return i;
            return -1;
        }

        private IEnumerator AnimateMove(int friendId, int from, int to)
        {
            Transform friend = friends[friendId];
            Vector3 start = friend.position;
            int lastCell = Mathf.Min(to, cells.Length - 1);
            float stepDuration = Mathf.Min(.26f, 1.25f / Mathf.Max(1, lastCell - from + 1));
            for (int cell = from + 1; cell <= lastCell; cell++)
            {
                Vector3 end = cells[cell].position;
                float elapsed = 0;
                while (elapsed < stepDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / stepDuration);
                    friend.position = Vector3.Lerp(start, end, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * .24f);
                    yield return null;
                }
                friend.position = end;
                start = end;
            }

            if (to >= cells.Length)
            {
                Vector3 exit = cells[7].position + (cells[7].position - cells[6].position) * .65f;
                float elapsed = 0;
                while (elapsed < .23f)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / .23f);
                    friend.position = Vector3.Lerp(start, exit, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * .19f);
                    yield return null;
                }
                exited[friendId] = true;
                friend.gameObject.SetActive(false);
            }
            else
                friendCells[friendId] = to;

            LayoutFriends();
            animating = false;
            moveRoutine = null;
            RefreshHud();
        }

        private void LayoutFriends()
        {
            for (int i = 0; i < friends.Length; i++)
            {
                if (exited[i])
                    continue;
                int cell = friendCells[i];
                bool together = !exited[1-i] && friendCells[1-i] == cell;
                friends[i].position = cells[cell].position + (together
                    ? new Vector3(i == 0 ? -.235f : .235f, 0, 0)
                    : Vector3.zero);
            }
        }

        private void RefreshHud()
        {
            if (runner == null)
                return;
            int rescued = runner.State.BorneOffA;
            progressLabel.text = "У мостика " + rescued + " / 2";
            stepChoices.Clear();
            for (int i = 0; i < runner.RemainingDice.Count; i++)
            {
                int index = i;
                var button = new Button(() => SelectStep(index)) { text = runner.RemainingDice[i].ToString() };
                button.AddToClassList("step-token");
                if (runner.SelectedDieIndex == i)
                    button.AddToClassList("step-token-selected");
                button.SetEnabled(!animating && !ended);
                stepChoices.Add(button);
            }
            if (ended && !animating)
                hintLabel.text = rescued == 2 ? "Оба друга дошли!" : "Попробуем ещё раз?";
            else if (!animating)
                hintLabel.text = "Выбери шаг, затем гоблина";
            UpdateHighlights();
        }

        private void SelectStep(int index)
        {
            if (animating || ended || !runner.SelectDieIndex(index))
                return;
            RefreshHud();
        }

        private void UpdateHighlights()
        {
            for (int i = 0; i < availableRings.Count; i++)
                availableRings[i].enabled = false;
            if (animating || ended || runner == null || !runner.SelectedDieIndex.HasValue)
                return;
            int index = runner.SelectedDieIndex.Value;
            if (index >= runner.RemainingDice.Count)
                return;
            var moves = MoveGenerator.GenerateLegalMoves(runner.State, runner.RemainingDice[index],
                runner.HeadMovesUsed, runner.HeadMovesLimit);
            foreach (var move in moves)
                if (move.FromCell >= 0 && move.FromCell < availableRings.Count)
                    availableRings[move.FromCell].enabled = true;
        }

        private void HandleMatchEnded(MatchResult result)
        {
            ended = true;
            // The result text is shown after the final jump in AnimateMove.
        }

        private void Restart()
        {
            if (runner == null)
                return;
            if (moveRoutine != null)
                StopCoroutine(moveRoutine);
            moveRoutine = null;
            runner.Reset();
            RestartPresentation();
        }

        private void RestartPresentation()
        {
            animating = false;
            ended = false;
            for (int i = 0; i < friends.Length; i++)
            {
                friendCells[i] = 0;
                exited[i] = false;
                friends[i].gameObject.SetActive(true);
            }
            LayoutFriends();
            RefreshHud();
        }
    }

    public sealed class FirstTrailPickTarget : MonoBehaviour
    {
        public int cellId = -1;
        public int friendId = -1;
    }
}
