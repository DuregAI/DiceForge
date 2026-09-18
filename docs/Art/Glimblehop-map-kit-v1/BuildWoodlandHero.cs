using System;
using UnityEditor;
using UnityEngine;

// Explicit authoring helper; never runs in a player build.
public static class BuildWoodlandHero
{
    public static object Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        const string path = "Assets/_Project/Resources/Map/WoodlandHero.prefab";
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/99_Prefabs/Battle/Units/GoblinValidationPrefab.prefab");
        var root = UnityEngine.Object.Instantiate(source);
        try
        {
            root.name = "WoodlandHero";
            foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(script);
            foreach (var collider in root.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            var animator = root.GetComponentInChildren<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) throw new Exception("Missing hero idle controller");
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            AssetDatabase.SaveAssets();
            return new { path, controller = animator.runtimeAnimatorController.name, renderers = root.GetComponentsInChildren<Renderer>().Length };
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
