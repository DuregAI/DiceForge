using UnityEngine;
using UnityEngine.Rendering;

namespace Diceforge.MapSystem
{
    [CreateAssetMenu(menuName = "Diceforge/Battle/Map Theme", fileName = "Theme_New")]
    public sealed class MapTheme : ScriptableObject
    {
        public GameObject tilemapPrefab;
        public string positionTilemapName = "TM_Tiles";
        // Optional separate background prefab. Do not reuse the board/tilemap prefab here.
        public GameObject backgroundPrefab;
        // Optional grouped decoration prefab authored in the same local space as the board/tilemap.
        // Do not assign single loose props here, or they will spawn at the theme root origin.
        public GameObject decorationsPrefab;
        public GameObject unitPrefab;
        public Color teamAColor = Color.white;
        public Color teamBColor = Color.red;
        public VolumeProfile postProcessProfile;
    }
}
