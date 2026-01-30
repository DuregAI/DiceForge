using UnityEditor;
using UnityEngine;

namespace Diceforge.View.Editor
{
    [CustomEditor(typeof(BattleDebugController))]
    public sealed class BattleDebugControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(8f);
            GUILayout.Label("Runtime Controls", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                var controller = (BattleDebugController)target;

                if (GUILayout.Button("Start"))
                    controller.StartMatch();

                if (GUILayout.Button("Stop"))
                    controller.StopMatch();

                if (GUILayout.Button("Step"))
                    controller.StepOnce();

                if (GUILayout.Button("Restart"))
                    controller.RestartMatch();
            }
        }
    }
}
