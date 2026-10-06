using System.Collections;
using Diceforge.Core;
using Diceforge.GameModes;
using UnityEngine;

namespace Diceforge.View
{
    public sealed class DemoTrailHazardView : MonoBehaviour
    {
        private BattleRunner runner;
        private DioramaBoard board;
        private StonesTokensView heroes;
        private GameObject figure;
        private LineRenderer nextStep;
        private Coroutine movement;
        public bool IsAnimating { get; private set; }
        public int PresentedCell { get; private set; } = -1;

        public void Configure(BattleRunner source, DioramaBoard geometry, StonesTokensView tokens, DemoLevelDefinition level)
        {
            Unbind();
            runner = source;
            board = geometry;
            heroes = tokens;
            if (runner == null || level == null || board == null || runner.Rules.soloTrailHazard == SoloTrailHazard.None) return;
            if (level.hazardPrefab == null) throw new System.InvalidOperationException($"Demo {level.levelId} has no hazard figure.");
            figure = Instantiate(level.hazardPrefab, board.transform);
            figure.name = runner.Rules.soloTrailHazard == SoloTrailHazard.Ryzh ? "Ryzh" : "Bark";
            if (runner.Rules.soloTrailHazard == SoloTrailHazard.Ryzh)
            {
                figure.transform.localScale *= .65f;
                figure.transform.rotation = Quaternion.Euler(0, 180, 0);
            }
            foreach (var collider in figure.GetComponentsInChildren<Collider>()) collider.enabled = false;
            var arrow = new GameObject("HazardNextStep");
            arrow.transform.SetParent(board.transform, false);
            nextStep = arrow.AddComponent<LineRenderer>();
            nextStep.sharedMaterial = board.landscapeRoot.GetComponentInChildren<DioramaCell>().highlight.sharedMaterial;
            nextStep.widthMultiplier = .045f;
            nextStep.startColor = nextStep.endColor = new Color(1, .76f, .3f);
            nextStep.positionCount = 5;
            runner.OnMatchStarted += ResetPresentation;
            runner.OnTrailHazardMoved += HandleMove;
            board.GeometryChanged += RefreshGeometry;
            ResetPresentation(runner.State);
        }

        private void ResetPresentation(GameState state)
        {
            if (movement != null) StopCoroutine(movement);
            movement = null;
            IsAnimating = false;
            PresentedCell = state.TrailHazardCell;
            figure.transform.position = Position(PresentedCell, state.TrailHazardYielded);
            DrawArrow();
        }

        private Vector3 Position(int cell, bool yielded)
        {
            if (cell >= 0) return board.CellPosition(cell);
            return board.ExitPosition(0) + (yielded ? Vector3.right * .8f : Vector3.zero);
        }

        private void HandleMove(TrailHazardMove move)
        {
            if (movement != null) StopCoroutine(movement);
            IsAnimating = true;
            nextStep.enabled = false;
            movement = StartCoroutine(Present(move));
        }

        private IEnumerator Present(TrailHazardMove move)
        {
            while (heroes.IsAnimating) yield return null;
            Vector3 start = figure.transform.position;
            Vector3 end = Position(move.ToCell, move.Yielded);
            float duration = DioramaBoard.ReducedMotion ? .08f : .4f;
            float elapsed = 0;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                figure.transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0, 1, elapsed / duration));
                yield return null;
            }
            figure.transform.position = end;
            PresentedCell = move.ToCell;
            IsAnimating = false;
            movement = null;
            DrawArrow();
        }

        private void DrawArrow()
        {
            nextStep.enabled = runner.Rules.soloTrailHazard == SoloTrailHazard.Ryzh && PresentedCell >= 0 && !runner.MatchEnded;
            if (!nextStep.enabled) return;
            Vector3 start = board.CellPosition(PresentedCell) + Vector3.up * .08f;
            Vector3 end = (PresentedCell < 7 ? board.CellPosition(PresentedCell + 1) : board.ExitPosition(0)) + Vector3.up * .08f;
            Vector3 direction = (end - start).normalized;
            Vector3 side = Vector3.Cross(direction, Vector3.up) * .12f;
            nextStep.SetPositions(new[] { start, end, end - direction * .22f + side, end, end - direction * .22f - side });
        }

        private void RefreshGeometry() => ResetPresentation(runner.State);
        private void Unbind()
        {
            if (runner != null)
            {
                runner.OnMatchStarted -= ResetPresentation;
                runner.OnTrailHazardMoved -= HandleMove;
            }
            if (board != null) board.GeometryChanged -= RefreshGeometry;
            if (movement != null) StopCoroutine(movement);
            movement = null;
            IsAnimating = false;
            if (figure != null) Destroy(figure);
            if (nextStep != null) Destroy(nextStep.gameObject);
        }
        private void OnDestroy() => Unbind();
    }
}
