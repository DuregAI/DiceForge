using UnityEngine;
using UnityEngine.Rendering;

namespace Diceforge.MapSystem
{
    [CreateAssetMenu(menuName = "Diceforge/Battle/Map Theme", fileName = "Theme_New")]
    public sealed class MapTheme : ScriptableObject
    {
        public GameObject tilemapPrefab;
        public string positionTilemapName = "TM_Tiles";
        public GameObject backgroundPrefab;
        public GameObject decorationsPrefab;
        public GameObject unitPrefab;
        public Color teamAColor = Color.white;
        public Color teamBColor = Color.red;
        public VolumeProfile postProcessProfile;
    }
}
