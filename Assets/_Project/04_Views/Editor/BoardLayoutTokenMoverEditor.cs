using UnityEditor;
using UnityEngine;

namespace Diceforge.View.Editor
{
    [CustomEditor(typeof(BoardLayoutTokenMover))]
    public sealed class BoardLayoutTokenMoverEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(8f);
            GUILayout.Label("Token Mover Tools", EditorStyles.boldLabel);

            var mover = (BoardLayoutTokenMover)target;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Step -1"))
                mover.Step(-1);

            if (GUILayout.Button("Step +1"))
                mover.Step(1);
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Snap To Start"))
                mover.SnapTo(0);

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Button actions that animate movement are intended for Play Mode.", MessageType.Info);
        }
    }
}
