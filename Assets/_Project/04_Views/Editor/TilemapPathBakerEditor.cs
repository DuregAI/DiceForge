using UnityEditor;
using UnityEngine;

namespace Diceforge.View.Editor
{
    [CustomEditor(typeof(TilemapPathBaker))]
    public sealed class TilemapPathBakerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(8f);
            GUILayout.Label("Path Bake Tools", EditorStyles.boldLabel);

            var baker = (TilemapPathBaker)target;
            if (GUILayout.Button("Bake Path"))
            {
                bool baked = baker.BakePath();
                if (baked)
                    Debug.Log("Tilemap path bake completed.", baker);
            }
        }
    }
}
