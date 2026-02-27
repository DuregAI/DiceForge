using Diceforge.Battle;
using Diceforge.Core;
using Diceforge.Map;
using Diceforge.MapSystem;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

namespace Diceforge.View
{
    [DefaultExecutionOrder(-500)]
    public sealed class BattleSceneBootstrapper : MonoBehaviour
    {
        [SerializeField] private BattleMapConfig defaultMapConfig;
        [SerializeField] private Transform backgroundRoot;
        [SerializeField] private Transform tilemapRoot;
        [SerializeField] private Transform decorationsRoot;
        [SerializeField] private Transform unitsRoot;
        [SerializeField] private BattleBoardViewController boardViewController;
        [SerializeField] private GameObject deprecatedRingRoot;

        private void Awake()
        {
            BattleStartRequest request = BattleLauncher.ConsumePendingRequest();
            GameModePreset requestedPreset = request != null ? request.presetOverride : null;

            BattleMapConfig map = request?.mapConfigOverride ?? BattleMapSelectionService.SelectedMap ?? defaultMapConfig;
            if (map == null)
            {
                Debug.LogError("[BattleSceneBootstrapper] Missing selected/default BattleMapConfig.", this);
                enabled = false;
                return;
            }

            if (!map.TryValidate(out string validationError))
            {
                Debug.LogError($"[BattleSceneBootstrapper] Invalid map '{map.name}': {validationError}", map);
                enabled = false;
                return;
            }

            BattleMapSelectionService.SelectedMap = map;

            GameModePreset activePreset = requestedPreset ?? map.gameModePreset;
            if (activePreset == null)
            {
                Debug.LogError("[BattleSceneBootstrapper] Missing GameModePreset on request/map.", this);
                enabled = false;
                return;
            }

            RulesetConfig activeRules = activePreset.rulesetPreset != null
                ? RulesetConfig.FromPreset(activePreset.rulesetPreset)
                : null;

            if (activeRules == null)
            {
                Debug.LogError($"[BattleSceneBootstrapper] Missing RulesetPreset on '{activePreset.name}'.", activePreset);
                enabled = false;
                return;
            }

            int cellsCount = map.boardLayout != null && map.boardLayout.cells != null ? map.boardLayout.cells.Count : 0;
            if (cellsCount <= 0)
            {
                Debug.LogError("[BattleSceneBootstrapper] Board layout has no cells.", map);
                enabled = false;
                return;
            }

            int startA = Mathf.Clamp(activeRules.startCellA, 0, cellsCount - 1);
            int startB = Mathf.Clamp(activeRules.startCellB, 0, cellsCount - 1);

            int setupPlacements = activePreset.setupPreset != null && activePreset.setupPreset.unitPlacements != null
                ? activePreset.setupPreset.unitPlacements.Count
                : 0;

            Debug.Log(
                $"[BattleSceneBootstrapper] NewStart={request != null} preset={activePreset.name} modeId={activePreset.modeId} " +
                $"rulesetId={activePreset.rulesetPreset.rulesetId} setupId={(activePreset.setupPreset != null ? activePreset.setupPreset.setupId : "<none>")} " +
                $"cells={cellsCount} startA={startA} startB={startB} mapId={map.mapId} setupPlacements={setupPlacements}",
                this);

            if (setupPlacements > 2)
            {
                Debug.Log($"[BattleSceneBootstrapper] Setup has {setupPlacements} placement entries. Scene still spawns one mover per side by design.", this);
            }

            if (boardViewController == null)
                boardViewController = FindAnyObjectByType<BattleBoardViewController>();

            Tilemap positionTilemap = InstantiateThemeAndResolvePositionTilemap(map);
            if (positionTilemap == null)
            {
                Debug.LogError("[BattleSceneBootstrapper] Position Tilemap was not found. Aborting bootstrap.", this);
                enabled = false;
                return;
            }

            VerifyUnitPrefabAnimator(map.mapTheme.unitPrefab);

            BoardLayoutTokenMover moverA = SpawnUnit("Unit_A", map, positionTilemap);
            BoardLayoutTokenMover moverB = SpawnUnit("Unit_B", map, positionTilemap);

            moverA?.SnapTo(startA);
            moverB?.SnapTo(startB);

            Debug.Log($"[BattleSceneBootstrapper] MoverA snappedCell={(moverA != null ? moverA.CurrentCellId : -1)}", this);
            Debug.Log($"[BattleSceneBootstrapper] MoverB snappedCell={(moverB != null ? moverB.CurrentCellId : -1)}", this);

            ApplyTeamColor(moverA != null ? moverA.gameObject : null, map.mapTheme.teamAColor);
            ApplyTeamColor(moverB != null ? moverB.gameObject : null, map.mapTheme.teamBColor);

            boardViewController?.SetMovers(moverA, moverB);
            boardViewController?.SetVisualMode(map.visualMode);
            boardViewController?.ConfigureTokensView(
                map.boardLayout,
                positionTilemap,
                unitsRoot,
                map.mapTheme.unitPrefab,
                map.mapTheme.teamAColor,
                map.mapTheme.teamBColor);

            BattleDebugController battleDebugController = FindAnyObjectByType<BattleDebugController>();
            if (battleDebugController != null)
            {
                battleDebugController.StartFromPreset(activePreset);
            }
            else
            {
                Debug.LogWarning("[BattleSceneBootstrapper] BattleDebugController not found. Preset bootstrap was skipped.", this);
            }

            if (deprecatedRingRoot != null && map.visualMode == BoardVisualMode.Tilemap)
                deprecatedRingRoot.SetActive(false);
        }

        private Tilemap InstantiateThemeAndResolvePositionTilemap(BattleMapConfig map)
        {
            MapTheme theme = map.mapTheme;

            if (theme.backgroundPrefab != null && backgroundRoot != null)
                Instantiate(theme.backgroundPrefab, backgroundRoot);

            if (theme.decorationsPrefab != null && decorationsRoot != null)
                Instantiate(theme.decorationsPrefab, decorationsRoot);

            GameObject tilemapInstance = null;
            if (theme.tilemapPrefab != null && tilemapRoot != null)
                tilemapInstance = Instantiate(theme.tilemapPrefab, tilemapRoot);

            if (tilemapInstance == null)
                return null;

            string tilemapName = string.IsNullOrWhiteSpace(theme.positionTilemapName)
                ? "TM_Tiles"
                : theme.positionTilemapName;

            Tilemap[] tilemaps = tilemapInstance.GetComponentsInChildren<Tilemap>(true);
            for (int i = 0; i < tilemaps.Length; i++)
            {
                if (tilemaps[i].name == tilemapName)
                    return tilemaps[i];
            }

            return null;
        }

        private static void VerifyUnitPrefabAnimator(GameObject unitPrefab)
        {
            if (unitPrefab == null)
            {
                Debug.LogWarning("[BattleSceneBootstrapper] Unit prefab is null. Cannot verify Animator.");
                return;
            }

            Animator animator = unitPrefab.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                Debug.LogWarning($"[BattleSceneBootstrapper] Unit prefab '{unitPrefab.name}' has no Animator. Movement animation will be skipped.");
                return;
            }

            if (animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning($"[BattleSceneBootstrapper] Animator on unit prefab '{unitPrefab.name}' has no RuntimeAnimatorController.");
                return;
            }

            Debug.Log($"[BattleSceneBootstrapper] Verified unit Animator on prefab '{unitPrefab.name}' controller='{animator.runtimeAnimatorController.name}'.");
        }

        private BoardLayoutTokenMover SpawnUnit(string unitName, BattleMapConfig map, Tilemap positionTilemap)
        {
            if (unitsRoot == null || map?.mapTheme?.unitPrefab == null)
                return null;

            GameObject unit = Instantiate(map.mapTheme.unitPrefab, unitsRoot);
            unit.name = unitName;

            BoardLayoutTokenMover mover = unit.GetComponent<BoardLayoutTokenMover>();
            if (mover == null)
                mover = unit.AddComponent<BoardLayoutTokenMover>();

            mover.SetLayout(map.boardLayout);
            mover.SetPositionTilemap(positionTilemap);

            SortingGroup sortingGroup = unit.GetComponent<SortingGroup>();
            if (sortingGroup == null)
                sortingGroup = unit.AddComponent<SortingGroup>();

            sortingGroup.sortAtRoot = true;
            sortingGroup.sortingOrder = 0;
            sortingGroup.sortingLayerName = "Default";

            Animator animator = unit.GetComponentInChildren<Animator>(true);
            if (animator != null)
                animator.applyRootMotion = false;

            return mover;
        }

        private static void ApplyTeamColor(GameObject unitRoot, Color teamColor)
        {
            if (unitRoot == null)
                return;

            Renderer[] renderers = unitRoot.GetComponentsInChildren<Renderer>(true);
            MaterialPropertyBlock block = new MaterialPropertyBlock();

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                renderer.GetPropertyBlock(block);

                Material shared = renderer.sharedMaterial;
                if (shared != null && shared.HasProperty("_BaseColor"))
                    block.SetColor("_BaseColor", teamColor);
                else if (shared != null && shared.HasProperty("_Color"))
                    block.SetColor("_Color", teamColor);
                else
                {
                    block.SetColor("_BaseColor", teamColor);
                    block.SetColor("_Color", teamColor);
                }

                renderer.SetPropertyBlock(block);
            }
        }
    }
}
