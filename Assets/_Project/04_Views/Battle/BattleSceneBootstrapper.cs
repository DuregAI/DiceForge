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
            BattleMapConfig map = BattleMapSelectionService.SelectedMap ?? defaultMapConfig;
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

            if (boardViewController == null)
                boardViewController = FindAnyObjectByType<BattleBoardViewController>();

            Tilemap positionTilemap = InstantiateThemeAndResolvePositionTilemap(map);
            if (positionTilemap == null)
            {
                Debug.LogError("[BattleSceneBootstrapper] Position Tilemap was not found. Aborting bootstrap.", this);
                enabled = false;
                return;
            }

            BoardLayoutTokenMover moverA = SpawnUnit("Unit_A", map, positionTilemap);
            BoardLayoutTokenMover moverB = SpawnUnit("Unit_B", map, positionTilemap);

            ApplyTeamColor(moverA != null ? moverA.gameObject : null, map.mapTheme.teamAColor);
            ApplyTeamColor(moverB != null ? moverB.gameObject : null, map.mapTheme.teamBColor);

            boardViewController?.SetMovers(moverA, moverB);
            boardViewController?.SetVisualMode(map.visualMode);

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
