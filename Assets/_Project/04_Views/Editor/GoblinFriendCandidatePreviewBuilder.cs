using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Diceforge.View.Editor
{
    public static class GoblinFriendCandidatePreviewBuilder
    {
        private const string SourceScene = "Assets/_Project/06_Scenes/Art/FirstTrailReference.unity";
        private const string PreviewScene = "Assets/_Project/06_Scenes/Art/FirstTrailGoblinCandidate.unity";
        private const string Model = "Assets/_Project/07_Art/GoblinFriendCandidate/GoblinFriend_Candidate.fbx";
        private const string IdleController = "Assets/_Project/07_Art/GoblinFriendCandidate/GoblinFriend_IdlePreview.controller";

        [MenuItem("Diceforge/Woodland/Preview goblin candidate on trail")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode before building the candidate preview.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save the current scene before building the candidate preview.");

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            if (model == null)
                throw new InvalidOperationException("Goblin candidate FBX is missing. Run build_goblin_candidate.py in Blender first.");
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(PreviewScene) != null)
            {
                EditorSceneManager.OpenScene(PreviewScene, OpenSceneMode.Single);
                var existing = GameObject.Find("New goblin candidate — idle preview")
                               ?? GameObject.Find("New goblin candidate — T pose");
                if (existing != null)
                {
                    var renamed = existing.name != "New goblin candidate — idle preview";
                    existing.name = "New goblin candidate — idle preview";
                    if (EnsureIdlePreview(existing) || renamed)
                        EditorSceneManager.SaveScene(existing.scene);
                    Selection.activeGameObject = existing;
                    return;
                }
            }
            else if (!AssetDatabase.CopyAsset(SourceScene, PreviewScene))
                throw new InvalidOperationException("Could not copy the trail scene for candidate preview.");

            var scene = EditorSceneManager.OpenScene(PreviewScene, OpenSceneMode.Single);
            var original = GameObject.Find("Friend 2");
            var route = GameObject.Find("Cell_2");
            if (original == null || route == null)
                throw new InvalidOperationException("The eight-cell trail scene is missing a friend or preview anchor.");

            original.name = "Friend 2 — existing model, disabled";
            original.SetActive(false);
            var candidate = (GameObject)PrefabUtility.InstantiatePrefab(model);
            candidate.name = "New goblin candidate — idle preview";
            candidate.transform.position = route.transform.position;
            candidate.transform.rotation = Quaternion.Euler(0, 180, 0);
            candidate.transform.localScale = Vector3.one * .42f;
            EnsureIdlePreview(candidate);

            var controller = UnityEngine.Object.FindAnyObjectByType<FirstTrailPlayController>();
            if (controller != null)
                controller.enabled = false;
            var hud = UnityEngine.Object.FindAnyObjectByType<UIDocument>();
            if (hud != null)
                hud.enabled = false;

            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = candidate;
            Debug.Log("Goblin candidate idle preview ready on cell 2. Original playable trail remains unchanged.");
        }

        private static bool EnsureIdlePreview(GameObject candidate)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(Model)
                .OfType<AnimationClip>()
                .FirstOrDefault(asset => asset.name.EndsWith("GF_Idle", StringComparison.Ordinal)
                                         && !asset.name.StartsWith("__preview__", StringComparison.Ordinal));
            if (clip == null)
                throw new InvalidOperationException("The goblin FBX has no GF_Idle clip. Re-export it from Blender.");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(IdleController);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(IdleController);

            var stateMachine = controller.layers[0].stateMachine;
            var state = stateMachine.states.Select(child => child.state)
                .FirstOrDefault(candidateState => candidateState.name == "Idle");
            if (state == null)
                state = stateMachine.AddState("Idle");
            if (state.motion != clip || stateMachine.defaultState != state)
            {
                state.motion = clip;
                stateMachine.defaultState = state;
                AssetDatabase.SaveAssets();
            }

            var animator = candidate.GetComponent<Animator>();
            if (animator == null)
                animator = candidate.AddComponent<Animator>();
            if (animator.runtimeAnimatorController == controller && !animator.applyRootMotion)
                return false;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            return true;
        }
    }
}
