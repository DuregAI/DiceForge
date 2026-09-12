using System;
using System.Linq;
using Diceforge.View;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Diceforge.Editor
{
    public static class GoblinPresentationBuilder
    {
        private const string ModelPath = "Assets/_Project/07_3D_Models/goblin.fbx";
        private const string PrefabPath = "Assets/_Project/99_Prefabs/Battle/Units/GoblinValidationPrefab.prefab";
        private const string Folder = "Assets/_Project/07_Art/Character/Goblin/Anim/";

        [MenuItem("Tools/Goblin/Build Board Presentation")]
        public static void Build()
        {
            var source = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().First(x => x.name == "Walk");
            var walk = BuildWalk(source);
            var idle = BuildIdle(source);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "Goblin_Board.controller")
                ?? AnimatorController.CreateAnimatorControllerAtPath(Folder + "Goblin_Board.controller");
            if (!controller.parameters.Any(p => p.name == "isMoving")) controller.AddParameter("isMoving", AnimatorControllerParameterType.Bool);
            var machine = controller.layers[0].stateMachine;
            var idleState = machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name == "Idle") ?? machine.AddState("Idle", new Vector3(240,90));
            var walkState = machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name == "Walk") ?? machine.AddState("Walk", new Vector3(520,90));
            idleState.motion = idle; walkState.motion = walk;
            // One half-stride per 0.25-second cell step (the source cycle lasts 1.5 seconds).
            walkState.speed = 3f;
            machine.defaultState = idleState;
            Transition(idleState, walkState, AnimatorConditionMode.If);
            Transition(walkState, idleState, AnimatorConditionMode.IfNot);
            EditorUtility.SetDirty(controller);

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var animator = root.GetComponentInChildren<Animator>(true);
                if (animator == null) throw new InvalidOperationException("Goblin Animator is missing.");
                Transform model = animator.transform;
                Transform pivot = root.transform.Find("GoblinBoardPivot");
                if (pivot == null)
                {
                    pivot = new GameObject("GoblinBoardPivot").transform;
                    pivot.SetParent(root.transform, false);
                }
                model.SetParent(pivot, false);
                model.localPosition = new Vector3(0f, .46f, 0f);
                model.localRotation = Quaternion.identity;
                model.localScale = Vector3.one;
                root.transform.localScale = Vector3.one * .35f;
                root.transform.localRotation = Quaternion.identity;
                pivot.localRotation = Quaternion.Euler(10f,155f,0f);
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                foreach (var camera in root.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(camera.gameObject);
                foreach (var light in root.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(light.gameObject);
                var animation = root.GetComponent<UnitAnimationController>();
                var animationSettings = new SerializedObject(animation);
                animationSettings.FindProperty("isMovingBoolParam").stringValue = "isMoving";
                animationSettings.FindProperty("animator").objectReferenceValue = animator;
                animationSettings.ApplyModifiedPropertiesWithoutUndo();
                var mover = root.GetComponent<BoardLayoutTokenMover>() ?? root.AddComponent<BoardLayoutTokenMover>();
                var movement = new SerializedObject(mover);
                movement.FindProperty("tokenRoot").objectReferenceValue = root.transform;
                movement.FindProperty("heightOffset").floatValue = .02f;
                movement.FindProperty("rotateAlongPath").boolValue = false;
                movement.ApplyModifiedPropertiesWithoutUndo();
                var facing = root.GetComponent<BoardUnitFacing>() ?? root.AddComponent<BoardUnitFacing>();
                var facingSettings = new SerializedObject(facing);
                facingSettings.FindProperty("visualPivot").objectReferenceValue = pivot;
                facingSettings.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[Goblin] Board presentation rebuilt: in-place walk, breathing idle, grounded pivot and XY facing.");
        }

        private static AnimationClip BuildWalk(AnimationClip source)
        {
            var clip = new AnimationClip { name = "Goblin_BoardWalk", frameRate = 30f };
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                var sourceCurve = AnimationUtility.GetEditorCurve(source, binding);
                var keys = new Keyframe[46];
                float first = sourceCurve.Evaluate(0f), last = sourceCurve.Evaluate(source.length);
                for (int i = 0; i < keys.Length; i++)
                {
                    float fraction = i / (float)(keys.Length - 1), time = fraction * source.length;
                    float value = sourceCurve.Evaluate(time);
                    if (binding.path == "Armature/Hips" && binding.propertyName.StartsWith("m_LocalPosition."))
                        value -= (last - first) * fraction;
                    // Close the exported take's seam; retain the original gait for most of the cycle.
                    value = Mathf.Lerp(value, first, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.85f, 1f, fraction)));
                    keys[i] = new Keyframe(time, value);
                }
                var curve = new AnimationCurve(keys);
                for (int i = 0; i < keys.Length; i++) AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                for (int i = 0; i < keys.Length; i++) AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }
            return SaveClip(clip, "Goblin_BoardWalk.anim");
        }

        private static AnimationClip BuildIdle(AnimationClip source)
        {
            var clip = new AnimationClip { name = "Goblin_BoardIdle", frameRate = 30f };
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                float value = AnimationUtility.GetEditorCurve(source, binding).Evaluate(0f);
                AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, 3f, value));
            }
            foreach (var group in AnimationUtility.GetCurveBindings(source).Where(b=>b.propertyName.StartsWith("m_LocalRotation.") &&
                (b.path.EndsWith("/Spine2") || b.path.EndsWith("/Head"))).GroupBy(b=>b.path))
            {
                var bindings = group.OrderBy(b=>"xyzw".IndexOf(b.propertyName.Last())).ToArray();
                float[] v = bindings.Select(b=>AnimationUtility.GetEditorCurve(source,b).Evaluate(0f)).ToArray();
                if (v.Length != 4) continue;
                var rest = new Quaternion(v[0],v[1],v[2],v[3]);
                var curves = Enumerable.Range(0,4).Select(_=>new AnimationCurve()).ToArray();
                for (int i=0; i<=12; i++)
                {
                    float time = i / 4f, wave = Mathf.Sin(time / 3f * Mathf.PI * 2f);
                    var q = rest * Quaternion.Euler(wave * (group.Key.EndsWith("/Head") ? 2f : .8f), 0f, 0f);
                    for (int axis=0; axis<4; axis++) curves[axis].AddKey(time,q[axis]);
                }
                for (int axis=0; axis<4; axis++) AnimationUtility.SetEditorCurve(clip,bindings[axis],curves[axis]);
            }
            return SaveClip(clip, "Goblin_BoardIdle.anim");
        }

        private static AnimationClip SaveClip(AnimationClip clip, string name)
        {
            clip.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + name);
            if (existing == null) { AssetDatabase.CreateAsset(clip, Folder + name); return clip; }
            EditorUtility.CopySerialized(clip, existing);
            Object.DestroyImmediate(clip);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static void Transition(AnimatorState from, AnimatorState to, AnimatorConditionMode condition)
        {
            foreach (var old in from.transitions) from.RemoveTransition(old);
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = .12f;
            transition.AddCondition(condition, 0f, "isMoving");
        }
    }
}
