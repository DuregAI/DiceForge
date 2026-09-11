using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Diceforge.TokenPlacement;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Diceforge.Tests.TokenPlacement
{
    // The legacy view/core live in Assembly-CSharp, which an asmdef cannot reference.
    // Keep the bridge here so the resolver and its test assembly stay independent.
    public class StonesTokensViewTests
    {
        private const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private GameObject _root;
        private GameObject _prefab;
        private ScriptableObject _layout;
        private Component _view;

        internal static Type RuntimeType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        internal static object Get(object target, string name) => target.GetType().GetField(name, Fields).GetValue(target);
        internal static void Set(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);
        internal static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        internal static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Fields).Invoke(target, args);
        private static object Player(int player) => Enum.ToObject(RuntimeType("Diceforge.Core.PlayerId"), player);

        [UnitySetUp]
        public IEnumerator EnterRuntime()
        {
            yield return new EnterPlayMode();
        }

        [UnityTearDown]
        public IEnumerator LeaveRuntime()
        {
            DestroyFixture();
            // Its OnDestroy unregisters from DiagnosticsRuntime and recreates it if already gone.
            // Dispose the integration first; leave the unrelated production shutdown bug out of this suite.
            foreach (Object client in Object.FindObjectsByType(RuntimeType("Diceforge.Integrations.SpacetimeDb.SpacetimeDbLocalDevRuntime"), FindObjectsSortMode.None))
                Object.DestroyImmediate(((Component)client).gameObject);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator AllMoveScenariosHaveIdenticalAssignmentsWithAndWithoutAnimation()
        {
            foreach (PlacementScenario scenario in TokenPlacementResolverTests.Scenarios())
            {
                Dictionary<string, Vector3> snapped = null;
                foreach (bool animate in new[] { false, true })
                {
                    CreateFixture(scenario.Before);
                    object record = Record(scenario.Move, scenario.Pip);
                    object state = State(scenario.Expected);
                    Call(_view, "HandleMoveApplied", record, state, animate, RootName(scenario.Preferred));
                    AssertAssignments(scenario.Expected);
                    Assert.That((bool)Property(_view, "IsAnimating"), Is.EqualTo(animate && scenario.Move.To == TokenLocation.Cell), scenario.Name);
                    // Duplicate notification must preserve the destination and the running mover.
                    Call(_view, "HandleMoveApplied", record, state, animate, RootName(scenario.Preferred));
                    yield return WaitForMovement(_view);
                    AssertAssignments(scenario.Expected, checkMover: true);
                    var positions = Tokens().ToDictionary(t => (string)Get(t, "stoneId"), t => ((GameObject)Get(t, "root")).transform.position);
                    if (!animate) snapped = positions;
                    else
                        foreach (TokenAssignment assignment in scenario.Expected.Where(t => t.Location != TokenLocation.BorneOff))
                            Assert.That(Vector3.Distance(snapped[assignment.Id], positions[assignment.Id]), Is.LessThan(0.0001f), scenario.Name);
                    DestroyFixture();
                }
            }
        }

        [UnityTest]
        public IEnumerator RestartDuringMovementCancelsCoroutinesAndClearsPendingSelection()
        {
            var initial = new[] { TokenPlacementResolverTests.Cell(0, 0, 1), TokenPlacementResolverTests.Cell(0, 1, 5) };
            CreateFixture(initial);
            object controller = _root.AddComponent(RuntimeType("Diceforge.View.BattleBoardViewController"));
            Set(controller, "stonesTokensView", _view);
            Call(controller, "SetPendingAnimatedTokenName", "StoneA_01");
            var action = new TokenMove(0, TokenLocation.Cell, 5, TokenLocation.Cell, 0);
            Call(_view, "HandleMoveApplied", Record(action), State(new[] { initial[0], initial[1].At(TokenLocation.Cell, 0) }), true, "StoneA_01");
            Assert.That((bool)Property(_view, "IsAnimating"), Is.True);
            yield return null;
            Call(controller, "HandleMatchStarted", State(initial));
            Assert.That(Get(controller, "_pendingAnimatedTokenName"), Is.Null);
            Assert.That((bool)Property(_view, "IsAnimating"), Is.False);
            yield return new WaitForSeconds(0.3f);
            AssertAssignments(initial, checkMover: true);
        }

        [UnityTest]
        public IEnumerator RecoveryAndPoolResizeWorkWithBothAnimationSettings()
        {
            foreach (bool animate in new[] { false, true })
            {
                var initial = new[] { TokenPlacementResolverTests.Cell(0, 0, 1), TokenPlacementResolverTests.Cell(0, 1, 5) };
                CreateFixture(initial);
                var expanded = new[] { TokenPlacementResolverTests.Cell(0, 0, 0), TokenPlacementResolverTests.Cell(0, 1, 1), TokenPlacementResolverTests.Cell(0, 2, 2) };
                LogAssert.Expect(LogType.Warning, new Regex("\\[StonesTokensView\\] Placement mismatch:.*"));
                Call(_view, "HandleMoveApplied", Record(new TokenMove(0, TokenLocation.Cell, 5, TokenLocation.Cell, 0)), State(expanded), animate, "StoneA_01");
                Assert.That((bool)Property(_view, "IsAnimating"), Is.False);
                AssertAssignments(expanded, checkMover: true);
                var reduced = new[] { TokenPlacementResolverTests.Cell(0, 0, 3) };
                Call(_view, "BuildTokensFromMatchState", State(reduced));
                AssertAssignments(reduced, checkMover: true);
                Assert.That(Tokens().Count(t => ((GameObject)Get(t, "root")).activeSelf), Is.EqualTo(1));
                yield return null;
                AssertAssignments(reduced, checkMover: true);
                DestroyFixture();
            }
        }

        private void CreateFixture(TokenAssignment[] assignments)
        {
            _root = new GameObject("Token placement test");
            _layout = ScriptableObject.CreateInstance(RuntimeType("Diceforge.Map.BoardLayout"));
            var cells = (IList)Get(_layout, "cells");
            for (int i = 0; i < 6; i++)
            {
                object cell = Activator.CreateInstance(RuntimeType("Diceforge.Map.CellData"));
                Set(cell, "cellId", i);
                Set(cell, "worldPos", new Vector3(i, 0, 0));
                cells.Add(cell);
            }
            _prefab = new GameObject("Test token prefab");
            _prefab.AddComponent(RuntimeType("Diceforge.View.BoardLayoutTokenMover"));
            _prefab.SetActive(false);
            _view = _root.AddComponent(RuntimeType("Diceforge.View.StonesTokensView"));
            Call(_view, "Configure", _layout, null, _root.transform, _prefab, _prefab, Color.white, Color.black);
            Call(_view, "BuildTokensFromMatchState", State(assignments));
            // Arrange previous identities, including noncanonical placements left by earlier moves.
            foreach (object token in Tokens())
            {
                TokenAssignment assignment = assignments.Single(t => t.Id == (string)Get(token, "stoneId"));
                Set(token, "placement", assignment);
                object mover = Get(token, "mover");
                Set(mover, "moveDuration", 0.015f);
                ((GameObject)Get(token, "root")).SetActive(assignment.Location != TokenLocation.BorneOff);
                if (assignment.Location == TokenLocation.Cell) Call(mover, "SnapTo", assignment.Cell);
                else if (assignment.Location == TokenLocation.Bar) Call(mover, "SnapToWorld", Vector3.zero, -1);
            }
        }

        [UnityTest]
        public IEnumerator ZeroDurationMoveFinishesAndMovedObjectCanBePickedAgain()
        {
            var initial = new[] { TokenPlacementResolverTests.Cell(0, 0, 1), TokenPlacementResolverTests.Cell(0, 1, 5) };
            CreateFixture(initial);
            foreach (object token in Tokens()) Set(Get(token, "mover"), "moveDuration", 0f);
            var expected = new[] { initial[0], initial[1].At(TokenLocation.Cell, 0) };
            Call(_view, "HandleMoveApplied", Record(new TokenMove(0, TokenLocation.Cell, 5, TokenLocation.Cell, 0)), State(expected), true, "StoneA_01");
            Assert.That((bool)Property(_view, "IsAnimating"), Is.False);
            yield return null;
            AssertAssignments(expected, checkMover: true);
            var cameraObject = new GameObject("Picking camera");
            cameraObject.transform.SetParent(_root.transform);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(2.5f, 0f, -10f);
            Component picker = _root.AddComponent(RuntimeType("Diceforge.View.BoardDebugView"));
            Set(picker, "_camera", camera);
            GameObject moved = (GameObject)Get(Tokens().Single(t => (string)Get(t, "stoneId") == "A-1"), "root");
            AssertPick(picker, camera, moved, 0);
        }

        internal static void AssertPick(object picker, Camera camera, GameObject token, int cell)
        {
            Vector3 screen = camera.WorldToScreenPoint(token.transform.position);
            object[] args = { new Vector2(screen.x, screen.y), -1, null };
            bool picked = (bool)Call(picker, "TryPickPlayerATokenCell", args);
            Assert.That(picked, Is.True, token.name);
            Assert.That(args[1], Is.EqualTo(cell));
            Assert.That(args[2], Is.EqualTo(token.name));
        }

        [UnityTest] public IEnumerator Level01NormalBattleEntry() => CheckLevel(1);
        [UnityTest] public IEnumerator Level05NormalBattleEntry() => CheckLevel(5);
        [UnityTest] public IEnumerator Level09NormalBattleEntry() => CheckLevel(9);

        private IEnumerator CheckLevel(int level)
        {
            string data = "Assets/_Project/05_Gameplay_Data/";
            Object preset = AssetDatabase.LoadMainAssetAtPath($"{data}GameModes/GM_Level_{level:D2}.asset");
            Object map = AssetDatabase.LoadMainAssetAtPath($"{data}Battle/Configs/Map_Level_{level:D2}.asset");
            object request = Activator.CreateInstance(RuntimeType("Diceforge.Battle.BattleStartRequest"), preset, map);
            RuntimeType("Diceforge.Battle.BattleLauncher").GetMethod("Start").Invoke(null, new[] { request });
            yield return null;
            yield return null;
            Component controller = (Component)Object.FindFirstObjectByType(RuntimeType("Diceforge.View.BattleDebugController"));
            Assert.That(controller, Is.Not.Null, "Normal battle bootstrap did not create a controller.");
            Call(controller, "StopMatch");
            _view = (Component)Object.FindFirstObjectByType(RuntimeType("Diceforge.View.StonesTokensView"));
            object boardController = Object.FindFirstObjectByType(RuntimeType("Diceforge.View.BattleBoardViewController"));
            object runner = Get(controller, "_runner");
            var seenPlayers = new HashSet<int>();
            for (int attempt = 0; attempt < 12 && seenPlayers.Count < 2; attempt++)
            {
                object gameState = Property(runner, "State");
                int player = Convert.ToInt32(Property(gameState, "CurrentPlayer"));
                Call(runner, "EnsureSelectedDie");
                var dice = ((IEnumerable)Property(runner, "RemainingDice")).Cast<int>().ToArray();
                if (dice.Length == 0) { Call(runner, "Tick"); continue; }
                int selected = Convert.ToInt32(Property(runner, "SelectedDieIndex"));
                var legal = ((IEnumerable)RuntimeType("Diceforge.Core.MoveGenerator").GetMethod("GenerateLegalMoves").Invoke(null,
                    new[] { gameState, (object)dice[selected], Property(runner, "HeadMovesUsed"), Property(runner, "HeadMovesLimit") })).Cast<object>().ToArray();
                if (legal.Length == 0) { Call(runner, "EndTurnIfNoMoves"); continue; }
                object move = legal.First(m => Property(m, "Kind").ToString() == "MoveStone");
                int from = (int)Property(move, "FromCell");
                TokenAssignment[] before = Tokens().Where(t => (bool)Get(t, "assigned")).Select(t => (TokenAssignment)Get(t, "placement")).ToArray();
                TokenAssignment chosen = before.First(t => t.Player == player && t.Location == TokenLocation.Cell && t.Cell == from);
                GameObject root = (GameObject)Get(Tokens().Single(t => (string)Get(t, "stoneId") == chosen.Id), "root");
                Call(boardController, "SetPendingAnimatedTokenName", root.name);
                Assert.That((bool)Call(runner, "TryApplyHumanMove", move), Is.True);
                yield return WaitForMovement(_view);
                TokenAssignment[] after = Tokens().Where(t => (bool)Get(t, "assigned")).Select(t => (TokenAssignment)Get(t, "placement")).ToArray();
                TokenAssignment moved = after.Single(t => t.Id == chosen.Id);
                Assert.That(moved.Cell, Is.Not.EqualTo(from), $"Level {level}, player {player}");
                foreach (TokenAssignment token in before.Where(t => t.Id != chosen.Id))
                    Assert.That(after.Single(t => t.Id == token.Id), Is.EqualTo(token), "An unrelated token changed cells.");
                AssertAssignments(after, checkMover: true);
                if (player == 0)
                {
                    object picker = Object.FindFirstObjectByType(RuntimeType("Diceforge.View.BoardDebugView"));
                    AssertPick(picker, Camera.main, root, moved.Cell);
                }
                seenPlayers.Add(player);
            }
            Assert.That(seenPlayers.Count, Is.EqualTo(2), $"Level {level}: both sides must move.");
            // Use the normal restart entry point while a mover is still traversing the board.
            Call(controller, "RestartMatch");
            TokenAssignment[] restarted = Tokens().Where(t => (bool)Get(t, "assigned")).Select(t => (TokenAssignment)Get(t, "placement")).ToArray();
            Call(runner, "Tick");
            Assert.That((bool)Property(_view, "IsAnimating"), Is.True);
            yield return null;
            Call(boardController, "SetPendingAnimatedTokenName", "StoneA_00");
            Call(controller, "RestartMatch");
            Assert.That(Get(boardController, "_pendingAnimatedTokenName"), Is.Null);
            Assert.That((bool)Property(_view, "IsAnimating"), Is.False);
            yield return new WaitForSeconds(0.35f);
            AssertAssignments(restarted, checkMover: true);
        }

        private void DestroyFixture()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            if (_prefab != null) Object.DestroyImmediate(_prefab);
            if (_layout != null) Object.DestroyImmediate(_layout);
        }

        private IEnumerable<object> Tokens() => ((IEnumerable)Get(_view, "_tokensA")).Cast<object>()
            .Concat(((IEnumerable)Get(_view, "_tokensB")).Cast<object>());

        private void AssertAssignments(TokenAssignment[] expected, bool checkMover = false)
        {
            object[] tokens = Tokens().Where(t => (bool)Get(t, "assigned")).ToArray();
            CollectionAssert.AreEquivalent(expected, tokens.Select(t => (TokenAssignment)Get(t, "placement")).ToArray());
            foreach (object token in tokens)
            {
                var assignment = (TokenAssignment)Get(token, "placement");
                Assert.That(((GameObject)Get(token, "root")).activeSelf, Is.EqualTo(assignment.Location != TokenLocation.BorneOff));
                if (checkMover && assignment.Location != TokenLocation.BorneOff)
                    Assert.That(Property(Get(token, "mover"), "CurrentCellId"), Is.EqualTo(assignment.Cell), assignment.Id);
            }
        }

        internal static IEnumerator WaitForMovement(object view)
        {
            float deadline = Time.realtimeSinceStartup + 8;
            while ((bool)Property(view, "IsAnimating") && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That((bool)Property(view, "IsAnimating"), Is.False, "Movement did not finish.");
            yield return null; // Include Start for freshly activated tokens.
        }

        private static string RootName(string id)
        {
            if (id == null) return null;
            string[] parts = id.Split('-');
            return parts.Length == 2 ? $"Stone{parts[0]}_{int.Parse(parts[1]):D2}" : id;
        }

        private static object State(TokenAssignment[] assignments)
        {
            object rules = Activator.CreateInstance(RuntimeType("Diceforge.Core.RulesetConfig"));
            Set(rules, "boardSize", 6);
            Set(rules, "totalStonesPerPlayer", 1);
            object state = Activator.CreateInstance(RuntimeType("Diceforge.Core.GameState"), rules);
            Array.Clear((int[])Get(state, "_stonesAByCell"), 0, 6);
            Array.Clear((int[])Get(state, "_stonesBByCell"), 0, 6);
            foreach (TokenAssignment assignment in assignments)
            {
                object player = Player(assignment.Player);
                if (assignment.Location == TokenLocation.Cell) Call(state, "AddStoneToCell", player, assignment.Cell);
                else if (assignment.Location == TokenLocation.Bar) Call(state, "AddToBar", player, 1);
                else Call(state, "AddBorneOff", player);
            }
            return state;
        }

        private static object Record(TokenMove action, int? selectedPip = null)
        {
            int pip = selectedPip ?? (action.FromCell < 0 || action.ToCell < 0 ? 1 : Math.Max(1, Math.Abs(action.ToCell - action.FromCell)));
            string kind = action.From == TokenLocation.Bar ? "EnterFromBar" : action.To == TokenLocation.BorneOff ? "BearOff" : "MoveStone";
            object move = RuntimeType("Diceforge.Core.Move").GetMethod(kind).Invoke(null,
                kind == "EnterFromBar" ? new object[] { pip } : new object[] { action.FromCell, pip });
            return Activator.CreateInstance(RuntimeType("Diceforge.Core.MoveRecord"), new object[]
            {
                0, Player(action.Player), move, action.FromCell < 0 ? null : (object)action.FromCell,
                action.ToCell < 0 ? null : (object)action.ToCell, pip,
                Activator.CreateInstance(RuntimeType("Diceforge.Core.DiceOutcomeResult")), Array.Empty<int>(),
                Enum.ToObject(RuntimeType("Diceforge.Core.ApplyResult"), 0),
                Enum.ToObject(RuntimeType("Diceforge.Core.MatchEndReason"), 0), null
            });
        }
    }
}
