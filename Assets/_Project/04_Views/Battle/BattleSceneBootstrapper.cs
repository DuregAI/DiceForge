using System;
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
        [SerializeField] private Transform backgroundRoot;
        [SerializeField] private Transform tilemapRoot;
        [SerializeField] private Transform decorationsRoot;
        [SerializeField] private Transform unitsRoot;
        [SerializeField] private BattleBoardViewController boardViewController;
        [SerializeField] private GameObject deprecatedRingRoot;

        private void Awake()
        {
            BattleStartRequest request = BattleLauncher.ConsumePendingRequest();
            if (request == null)
                throw BuildBootstrapException("missing BattleStartRequest. All battle entries must use BattleLauncher.Start(BattleStartRequest)", null, null);

            const bool isNewPipeline = true;
            BattleMapConfig map = request.mapConfigOverride;
            GameModePreset activePreset = request.presetOverride;

            if (activePreset == null)
                throw BuildBootstrapException("request preset is null", null, map);

            if (map == null)
                throw BuildBootstrapException("request map is null", activePreset, null);

            if (!map.TryValidate(out string validationError))
                throw BuildBootstrapException($"map validation failed: {validationError}", activePreset, map);

            if (map.gameModePreset == null)
                throw BuildBootstrapException("map has no GameModePreset", activePreset, map);

            if (map.gameModePreset != activePreset)
                throw BuildBootstrapException($"request preset '{activePreset.name}' does not match map preset '{map.gameModePreset.name}'", activePreset, map);

            if (activePreset.rulesetPreset == null)
                throw BuildBootstrapException("preset has no RulesetPreset", activePreset, map);

            if (activePreset.setupPreset == null)
                throw BuildBootstrapException("preset has no SetupPreset", activePreset, map);

            if (map.mapTheme == null)
                throw BuildBootstrapException("map has no MapTheme", activePreset, map);

            if (map.mapTheme.tilemapPrefab == null)
                throw BuildBootstrapException("map theme has no tilemapPrefab", activePreset, map);

            if (map.mapTheme.unitPrefab == null)
                throw BuildBootstrapException("map theme has no unitPrefab", activePreset, map);

            if (map.boardLayout == null || map.boardLayout.cells == null || map.boardLayout.cells.Count == 0)
                throw BuildBootstrapException("map boardLayout has no cells", activePreset, map);

            BattleMapSelectionService.SelectedMap = map;

            RulesetConfig activeRules = RulesetConfig.FromPreset(activePreset.rulesetPreset);
            int cellsCount = map.boardLayout.cells.Count;

            if (activeRules.startCellA < 0 || activeRules.startCellA >= cellsCount)
                throw BuildBootstrapException($"startCellA={activeRules.startCellA} outside [0..{cellsCount - 1}]", activePreset, map);

            if (activeRules.startCellB < 0 || activeRules.startCellB >= cellsCount)
                throw BuildBootstrapException($"startCellB={activeRules.startCellB} outside [0..{cellsCount - 1}]", activePreset, map);

            int startA = activeRules.startCellA;
            int startB = activeRules.startCellB;

            int setupPlacements = activePreset.setupPreset != null && activePreset.setupPreset.unitPlacements != null
                ? activePreset.setupPreset.unitPlacements.Count
                : 0;

            Debug.Log(
                $"[BattleSceneBootstrapper] NewStart={isNewPipeline} preset={activePreset.name} modeId={activePreset.modeId} " +
                $"rulesetId={activePreset.rulesetPreset.rulesetId} setupId={(activePreset.setupPreset != null ? activePreset.setupPreset.setupId : "<none>")} " +
                $"cells={cellsCount} startA={startA} startB={startB} mapId={map.mapId} setupPlacements={setupPlacements}",
                this);

            if (setupPlacements > 2)
            {
                Debug.Log($"[BattleSceneBootstrapper] Setup has {setupPlacements} placement entries. Scene still spawns one mover per side by design.", this);
            }

            if (boardViewController == null)
                throw BuildBootstrapException("BattleBoardViewController reference is missing", activePreset, map);

            if (unitsRoot == null)
                throw BuildBootstrapException("unitsRoot reference is missing", activePreset, map);

            if (tilemapRoot == null)
                throw BuildBootstrapException("tilemapRoot reference is missing", activePreset, map);

            Tilemap positionTilemap = InstantiateThemeAndResolvePositionTilemap(map);
            if (positionTilemap == null)
                throw BuildBootstrapException($"position tilemap '{map.mapTheme.positionTilemapName}' was not found in tilemap prefab", activePreset, map);

            VerifyUnitPrefabAnimator(map.mapTheme.unitPrefab);

            BoardLayoutTokenMover moverA = SpawnUnit("Unit_A", map, positionTilemap);
            BoardLayoutTokenMover moverB = SpawnUnit("Unit_B", map, positionTilemap);

            moverA.SnapTo(startA);
            moverB.SnapTo(startB);

            Debug.Log($"[BattleSceneBootstrapper] MoverA snappedCell={(moverA != null ? moverA.CurrentCellId : -1)}", this);
            Debug.Log($"[BattleSceneBootstrapper] MoverB snappedCell={(moverB != null ? moverB.CurrentCellId : -1)}", this);

            ApplyTeamColor(moverA != null ? moverA.gameObject : null, map.mapTheme.teamAColor);
            ApplyTeamColor(moverB != null ? moverB.gameObject : null, map.mapTheme.teamBColor);

            boardViewController.SetMovers(moverA, moverB);
            boardViewController.SetVisualMode(map.visualMode);
            boardViewController.ConfigureTokensView(
                map.boardLayout,
                positionTilemap,
                unitsRoot,
                map.mapTheme.unitPrefab,
                map.mapTheme.teamAColor,
                map.mapTheme.teamBColor);

            BattleDebugController battleDebugController = FindAnyObjectByType<BattleDebugController>();
            if (battleDebugController == null)
                throw BuildBootstrapException("BattleDebugController not found in scene", activePreset, map);

            battleDebugController.StartFromPreset(activePreset);

            if (deprecatedRingRoot != null && map.visualMode == BoardVisualMode.Tilemap)
                deprecatedRingRoot.SetActive(false);
        }

        private static InvalidOperationException BuildBootstrapException(string reason, GameModePreset preset, BattleMapConfig map)
        {
            string presetName = preset != null ? preset.name : "<none>";
            string modeId = preset != null ? preset.modeId : "<none>";
            string mapName = map != null ? map.name : "<none>";
            string mapId = map != null ? map.mapId : "<none>";
            return new InvalidOperationException($"[BattleSceneBootstrapper] Strict bootstrap failure: {reason}. preset={presetName} modeId={modeId} map={mapName} mapId={mapId}");
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

            if (string.IsNullOrWhiteSpace(theme.positionTilemapName))
                throw BuildBootstrapException("map theme has empty positionTilemapName", map.gameModePreset, map);

            string tilemapName = theme.positionTilemapName;

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
                throw new InvalidOperationException("[BattleSceneBootstrapper] Strict bootstrap failure: unit prefab is null.");

            Animator animator = unitPrefab.GetComponentInChildren<Animator>(true);
            if (animator == null)
                throw new InvalidOperationException($"[BattleSceneBootstrapper] Strict bootstrap failure: unit prefab '{unitPrefab.name}' has no Animator component.");

            if (animator.runtimeAnimatorController == null)
                throw new InvalidOperationException($"[BattleSceneBootstrapper] Strict bootstrap failure: Animator on unit prefab '{unitPrefab.name}' has no RuntimeAnimatorController.");

            Debug.Log($"[BattleSceneBootstrapper] Verified unit Animator on prefab '{unitPrefab.name}' controller='{animator.runtimeAnimatorController.name}'.");
        }

        private BoardLayoutTokenMover SpawnUnit(string unitName, BattleMapConfig map, Tilemap positionTilemap)
        {
            if (unitsRoot == null)
                throw BuildBootstrapException("unitsRoot reference is missing", map != null ? map.gameModePreset : null, map);

            if (map == null)
                throw BuildBootstrapException("map is null while spawning unit", null, null);

            if (map.mapTheme == null)
                throw BuildBootstrapException("map theme is null while spawning unit", map.gameModePreset, map);

            if (map.mapTheme.unitPrefab == null)
                throw BuildBootstrapException("map unitPrefab is null while spawning unit", map.gameModePreset, map);

            if (positionTilemap == null)
                throw BuildBootstrapException("position tilemap is null while spawning unit", map.gameModePreset, map);

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
