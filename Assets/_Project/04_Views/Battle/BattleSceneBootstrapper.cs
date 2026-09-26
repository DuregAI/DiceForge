using System;
using System.Collections;
using Diceforge.Battle;
using Diceforge.Audio;
using Diceforge.Core;
using Diceforge.Diagnostics;
using Diceforge.Map;
using Diceforge.MapSystem;
using UnityEngine;
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
        private BattleDebugController pendingHudBattle;
        private DioramaBoard pendingHudDiorama;

        private void Awake()
        {
            AudioManager.Instance?.EnsureGameplayMusic();
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

            if (activePreset.rulesetPreset == null)
                throw BuildBootstrapException("preset has no RulesetPreset", activePreset, map);

            if (activePreset.setupPreset == null)
                throw BuildBootstrapException("preset has no SetupPreset", activePreset, map);

            if (map.mapTheme == null)
                throw BuildBootstrapException("map has no MapTheme", activePreset, map);

            bool useDiorama = map.mapTheme.presentation == MapTheme.Presentation.Diorama;
            if (!useDiorama && map.mapTheme.tilemapPrefab == null)
                throw BuildBootstrapException("map theme has no tilemapPrefab", activePreset, map);

            if (map.mapTheme.backgroundPrefab != null && map.mapTheme.backgroundPrefab == map.mapTheme.tilemapPrefab)
                throw BuildBootstrapException("map theme backgroundPrefab must be a separate background prefab, not the same asset as tilemapPrefab", activePreset, map);

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

            if (boardViewController == null)
                throw BuildBootstrapException("BattleBoardViewController reference is missing", activePreset, map);

            if (unitsRoot == null)
                throw BuildBootstrapException("unitsRoot reference is missing", activePreset, map);

            if (tilemapRoot == null)
                throw BuildBootstrapException("tilemapRoot reference is missing", activePreset, map);

            DioramaBoard diorama = null;
            Tilemap positionTilemap = null;
            if (useDiorama)
            {
                if (map.mapTheme.dioramaPrefab == null) throw BuildBootstrapException("diorama prefab missing", activePreset, map);
                diorama = Instantiate(map.mapTheme.dioramaPrefab, tilemapRoot).GetComponent<DioramaBoard>();
                if (diorama == null || diorama.layout == null || !diorama.layout.Validate(cellsCount, out _))
                    throw BuildBootstrapException("invalid diorama layout", activePreset, map);
                diorama.Initialize();
                var legacyFloor = GameObject.Find("Floor");
                if (legacyFloor != null) legacyFloor.SetActive(false);
                var legacyMusicPanel = GameObject.Find("NowPlayingUI");
                if (legacyMusicPanel != null) legacyMusicPanel.SetActive(false);
            }
            else positionTilemap = InstantiateThemeAndResolvePositionTilemap(map);
            if (!useDiorama && positionTilemap == null)
                throw BuildBootstrapException($"position tilemap '{map.mapTheme.positionTilemapName}' was not found in tilemap prefab", activePreset, map);

            GameObject teamAUnitPrefab = map.mapTheme.unitPrefab;
            GameObject teamBUnitPrefab = map.mapTheme.teamBUnitPrefab != null
                ? map.mapTheme.teamBUnitPrefab
                : map.mapTheme.unitPrefab;

            VerifyUnitPrefabAnimator(teamAUnitPrefab);
            VerifyUnitPrefabAnimator(teamBUnitPrefab);

            // Legacy single-mover visuals are disabled; token view is the only runtime stone visual path.
            // UnitsRoot is just a runtime container; unit positions still resolve from the active tilemap.
            boardViewController.SetMovers(null, null);
            boardViewController.ConfigureTokensView(
                map.boardLayout,
                positionTilemap,
                unitsRoot,
                teamAUnitPrefab,
                teamBUnitPrefab,
                map.mapTheme.teamAColor,
                map.mapTheme.teamBColor);
            if (diorama != null) boardViewController.ConfigureDiorama(diorama);

            BattleDebugController battleDebugController = FindAnyObjectByType<BattleDebugController>();
            if (battleDebugController == null)
                throw BuildBootstrapException("BattleDebugController not found in scene", activePreset, map);

            battleDebugController.ConfigureBoardSelection(map.boardLayout, positionTilemap);
            if (diorama != null)
            {
                FindAnyObjectByType<BoardDebugView>().ConfigureGeometry(diorama);
                pendingHudBattle = battleDebugController;
                pendingHudDiorama = diorama;
            }
            battleDebugController.StartFromPreset(activePreset);

            // Publish battle start only after strict validation and match bootstrap succeed.
            ClientDiagnostics.RecordBattleStarted(new BattleStartDiagnosticsContext(
                activePreset.modeId,
                activePreset.rulesetPreset.rulesetId,
                activePreset.setupPreset.setupId,
                map.mapId,
                map.name,
                cellsCount));
        }

        private IEnumerator Start()
        {
            if (pendingHudBattle == null || pendingHudDiorama == null)
                yield break;

            // Scene loading can destroy the menu panel while UI Toolkit updates its native
            // transform hierarchy. Attach the battle panel after that scene transition frame.
            yield return null;
            if (pendingHudBattle != null && pendingHudDiorama != null)
                pendingHudBattle.gameObject.AddComponent<DioramaHud>()
                    .Initialize(pendingHudBattle, pendingHudDiorama);
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

            // Decoration prefabs are expected to be pre-authored in board-local space.
            if (theme.decorationsPrefab != null && decorationsRoot != null)
                Instantiate(theme.decorationsPrefab, decorationsRoot);

            GameObject tilemapInstance = null;
            if (theme.tilemapPrefab != null && tilemapRoot != null)
                tilemapInstance = Instantiate(theme.tilemapPrefab, tilemapRoot);

            if (tilemapInstance == null)
                return null;

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
    }
}
