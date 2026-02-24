using UnityEditor;
using UnityEngine;

namespace Diceforge.View.Editor
{
    [CustomEditor(typeof(TileShadowSync))]
    public sealed class TileShadowSyncEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(8f);
            GUILayout.Label("Tile Shadow Tools", EditorStyles.boldLabel);

            var sync = (TileShadowSync)target;

            if (GUILayout.Button("Sync Now"))
                sync.SyncNow();

            if (GUILayout.Button("Clear Shadows"))
                sync.ClearShadows();
        }
    }
}
