using System;
using System.IO;
using System.Linq;
using Diceforge.Core;
using Diceforge.Map;
using Diceforge.MapSystem;
using Diceforge.Presets;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Diceforge.View.Editor
{
    /// <summary>Connects the existing campaign battles to the authored woodland presentation.</summary>
    public static class PlayableDioramaRollout
    {
        private const string Configs = "Assets/_Project/05_Gameplay_Data/Battle/Configs";
        private const string Diorama = "Assets/_Project/05_Gameplay_Data/Battle/Diorama";
        private const string Hero = "Assets/_Project/05_Gameplay_Data/Battle/WoodlandHero";
        private const string Goblin = "Assets/_Project/07_Art/GoblinFriendCandidate";
        private const string TrailArt = "Assets/_Project/07_Art/FirstTrail/FirstTrail.fbx";
        private const string FriendModel = Goblin + "/GoblinFriend_Candidate.fbx";
        private const string FriendWalk = Goblin + "/GoblinFriend_Walk.fbx";
        private const string FriendController = Goblin + "/GoblinFriend_Gameplay.controller";
        private const string EightCellLayout = "Assets/_Project/05_Gameplay_Data/Battle/Tilemap/BoardLayout_Level_02.asset";
        private const string EightCellPrefab = Diorama + "/FirstTrail8.prefab";

        [MenuItem("Diceforge/Woodland/Enable playable dioramas for levels 01-09")]
        public static void BuildAndEnable()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode before changing campaign visuals.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scene changes before building campaign visuals.");

            AssetDatabase.Refresh();
            ConfigureGoblinClips();
            var controller = BuildGameplayController();
            var originalScene = SceneManager.GetActiveScene();
            var temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject red;
            GameObject blue;
            GameObject firstTrail;
            try
            {
                SceneManager.SetActiveScene(temporary);
                red = BuildFriend("Red", controller, null, null);
                blue = BuildFriend("Blue", controller, BuildBlueScarf(), BuildBlueScarfFold());
                firstTrail = BuildFirstTrailBoard();
            }
            finally
            {
                SceneManager.SetActiveScene(originalScene);
                EditorSceneManager.CloseScene(temporary, true);
            }

            RebalanceEarlyCampaign();
            int connected = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:BattleMapConfig", new[] { Configs }))
            {
                var map = AssetDatabase.LoadAssetAtPath<BattleMapConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (map == null || !map.name.StartsWith("Map_Level_", StringComparison.Ordinal))
                    continue;
                if (!int.TryParse(map.name.Substring("Map_Level_".Length), out var level) || level < 1 || level > 9)
                    continue;

                var themePath = Diorama + "/Theme_" + map.name + ".asset";
                var theme = AssetDatabase.LoadAssetAtPath<MapTheme>(themePath);
                if (theme == null)
                    throw new InvalidOperationException("Missing woodland theme: " + themePath);
                if (level <= 6)
                    theme.dioramaPrefab = firstTrail;
                else
                    AddWaterBackdrop(theme.dioramaPrefab);
                var board = theme.dioramaPrefab != null ? theme.dioramaPrefab.GetComponent<DioramaBoard>() : null;
                string error = null;
                if (board == null || board.layout == null ||
                    !board.layout.Validate(map.boardLayout.cells.Count, out error))
                    throw new InvalidOperationException("Invalid diorama for " + map.name + ": " + error);
                for (int i = 0; i < board.layout.cellIds.Length; i++)
                    if (board.layout.cellIds[i] != map.boardLayout.cells[i].cellId)
                        throw new InvalidOperationException("Cell order differs on " + map.name + " at index " + i);

                theme.presentation = MapTheme.Presentation.Diorama;
                theme.unitPrefab = red;
                theme.teamBUnitPrefab = blue;
                map.mapTheme = theme;
                EditorUtility.SetDirty(theme);
                EditorUtility.SetDirty(map);
                connected++;
            }
            if (connected != 9)
                throw new InvalidOperationException("Expected 9 campaign maps, found " + connected);
            AssetDatabase.SaveAssets();
            Debug.Log("Woodland diorama enabled for levels 01-09; levels 01-06 share the eight-cell trail and new goblin counts.");
        }

        [MenuItem("Diceforge/Woodland/Use eight-cell trail for levels 01-06")]
        public static void RebalanceEarlyCampaign()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode before changing campaign balance.");

            var layout = AssetDatabase.LoadAssetAtPath<BoardLayout>(EightCellLayout);
            var firstTrail = AssetDatabase.LoadAssetAtPath<GameObject>(EightCellPrefab);
            var board = firstTrail != null ? firstTrail.GetComponent<DioramaBoard>() : null;
            var red = AssetDatabase.LoadAssetAtPath<GameObject>(Goblin + "/GoblinFriend_Red.prefab");
            var blue = AssetDatabase.LoadAssetAtPath<GameObject>(Goblin + "/GoblinFriend_Blue.prefab");
            string error = null;
            if (layout == null || layout.cells == null || layout.cells.Count != 8 ||
                board == null || board.layout == null || !board.layout.Validate(8, out error) ||
                red == null || blue == null)
                throw new InvalidOperationException("The playable eight-cell trail or goblins are missing: " + error);
            for (int cell = 0; cell < 8; cell++)
                if (layout.cells[cell].cellId != board.layout.cellIds[cell])
                    throw new InvalidOperationException("Trail cell order differs at index " + cell);

            int[] goblinsPerSide = { 2, 2, 2, 3, 3, 4 };
            for (int level = 1; level <= goblinsPerSide.Length; level++)
            {
                string suffix = level.ToString("00");
                var rules = AssetDatabase.LoadAssetAtPath<RulesetPreset>(
                    "Assets/_Project/05_Gameplay_Data/Rulesets/Ruleset_Level_" + suffix + ".asset");
                var setup = AssetDatabase.LoadAssetAtPath<SetupPreset>(
                    "Assets/_Project/05_Gameplay_Data/Setups/Setup_Level_" + suffix + ".asset");
                var map = AssetDatabase.LoadAssetAtPath<BattleMapConfig>(Configs + "/Map_Level_" + suffix + ".asset");
                var theme = AssetDatabase.LoadAssetAtPath<MapTheme>(Diorama + "/Theme_Map_Level_" + suffix + ".asset");
                var preset = AssetDatabase.LoadAssetAtPath<GameModePreset>(
                    "Assets/_Project/05_Gameplay_Data/GameModes/GM_Level_" + suffix + ".asset");
                if (rules == null || setup == null || map == null || theme == null || preset == null ||
                    preset.rulesetPreset != rules || preset.setupPreset != setup || preset.mapConfig != map ||
                    setup.unitPlacements == null || setup.unitPlacements.Count != 0)
                    throw new InvalidOperationException("Incomplete or customized campaign setup for level " + suffix);

                rules.boardSize = 8;
                rules.homeSize = 2;
                rules.startCellA = 0;
                rules.startCellB = 7;
                rules.maxUnitsPerSide = goblinsPerSide[level - 1];
                rules.totalStonesPerPlayer = goblinsPerSide[level - 1];
                setup.boardSize = 8;
                map.boardLayout = layout;
                map.mapTheme = theme;
                theme.presentation = MapTheme.Presentation.Diorama;
                theme.dioramaPrefab = firstTrail;
                theme.unitPrefab = red;
                theme.teamBUnitPrefab = blue;
                EditorUtility.SetDirty(rules);
                EditorUtility.SetDirty(setup);
                EditorUtility.SetDirty(map);
                EditorUtility.SetDirty(theme);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Campaign levels 01-06 now share the eight-cell trail with 2/2/2/3/3/4 goblins per side.");
        }

        private static void ConfigureGoblinClips()
        {
            foreach (var path in new[] { FriendModel, FriendWalk })
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                    throw new InvalidOperationException("Missing exported goblin FBX: " + path);
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.importAnimation = true;
                var clips = importer.defaultClipAnimations;
                if (clips.Length == 0)
                    throw new InvalidOperationException("No animation found in " + path);
                clips[0].name = path == FriendWalk ? "GF_Walk" : "GF_Idle";
                clips[0].loopTime = true;
                if (path == FriendWalk)
                {
                    clips[0].firstFrame = 0;
                    clips[0].lastFrame = 24;
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        private static void AddWaterBackdrop(GameObject boardPrefab)
        {
            if (boardPrefab == null)
                throw new InvalidOperationException("A numbered level has no woodland board prefab.");
            var path = AssetDatabase.GetAssetPath(boardPrefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var existing = root.transform.Find("Campaign water backdrop");
                var water = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Plane);
                water.name = "Campaign water backdrop";
                water.transform.SetParent(root.transform, false);
                water.transform.localPosition = new Vector3(0, -.57f, 0);
                water.transform.localScale = Vector3.one * 10f;
                if (water.GetComponent<Collider>() != null)
                    UnityEngine.Object.DestroyImmediate(water.GetComponent<Collider>());
                var renderer = water.GetComponent<Renderer>();
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(FirstTrailArtMaterials.Folder + "TrailLake.mat");
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                if (water.GetComponent<WoodlandWater>() == null)
                    water.AddComponent<WoodlandWater>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static AnimatorController BuildGameplayController()
        {
            var idle = AssetDatabase.LoadAllAssetsAtPath(FriendModel).OfType<AnimationClip>()
                .FirstOrDefault(clip => clip.name == "GF_Idle");
            var walk = AssetDatabase.LoadAllAssetsAtPath(FriendWalk).OfType<AnimationClip>()
                .FirstOrDefault(clip => clip.name == "GF_Walk");
            if (idle == null || walk == null)
                throw new InvalidOperationException("Goblin idle or movement clip failed to import.");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(FriendController);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(FriendController);
            var states = controller.layers[0].stateMachine;
            SetState(states, "Idle", idle);
            SetState(states, "Walk", walk);
            states.defaultState = states.states.First(child => child.state.name == "Idle").state;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void SetState(AnimatorStateMachine machine, string name, AnimationClip clip)
        {
            var state = machine.states.Select(child => child.state).FirstOrDefault(item => item.name == name);
            if (state == null)
                state = machine.AddState(name);
            state.motion = clip;
        }

        private static Material BuildBlueScarf()
        {
            return BuildBlueCloth("GoblinFriend_BlueScarf", new Color(.11f, .51f, .57f));
        }

        private static Material BuildBlueScarfFold()
        {
            return BuildBlueCloth("GoblinFriend_BlueScarfFold", new Color(.06f, .34f, .40f));
        }

        private static Material BuildBlueCloth(string name, Color color)
        {
            var path = Goblin + "/" + name + ".mat";
            var blue = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (blue == null)
            {
                blue = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(blue, path);
            }
            blue.SetColor("_BaseColor", color);
            blue.SetTexture("_BaseMap", null);
            blue.SetFloat("_Smoothness", .18f);
            EditorUtility.SetDirty(blue);
            return blue;
        }

        private static GameObject BuildFriend(string team, AnimatorController controller, Material scarfOverride, Material foldOverride)
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FriendModel);
            if (modelAsset == null)
                throw new InvalidOperationException("Goblin model is missing.");
            var root = new GameObject("GoblinFriend_" + team);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
            model.name = "Model";
            model.transform.localScale = Vector3.one * .52f;
            var animator = model.GetComponent<Animator>();
            if (animator == null)
                animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            if (scarfOverride != null)
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                        if (materials[i] != null && materials[i].name.StartsWith("GF_Scarf_Red", StringComparison.Ordinal))
                            materials[i] = scarfOverride;
                        else if (materials[i] != null && materials[i].name.StartsWith("GF_Scarf_Fold", StringComparison.Ordinal))
                            materials[i] = foldOverride;
                    renderer.sharedMaterials = materials;
                }
            root.AddComponent<BoardLayoutTokenMover>();
            root.AddComponent<GoblinLife>();
            var path = Goblin + "/GoblinFriend_" + team + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildFirstTrailBoard()
        {
            var map = AssetDatabase.LoadAssetAtPath<BattleMapConfig>(Configs + "/Map_Level_02.asset");
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(TrailArt);
            if (map == null || map.boardLayout == null || map.boardLayout.cells.Count != 8 || modelAsset == null)
                throw new InvalidOperationException("Level 02 and the eight-cell trail art are required.");
            FirstTrailArtMaterials.Build();
            var atlas = AssetDatabase.LoadAssetAtPath<Material>(Diorama + "/Atlas.mat");
            var root = new GameObject("FirstTrail8_Diorama");
            var board = root.AddComponent<DioramaBoard>();
            board.landscapeOnly = true;
            board.desktopPipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Hero + "/HeroPipeline.asset");
            board.mobilePipeline = board.desktopPipeline;
            var landscape = new GameObject("Landscape");
            landscape.transform.SetParent(root.transform, false);
            board.landscapeRoot = landscape;
            var portrait = new GameObject("Portrait deferred");
            portrait.transform.SetParent(root.transform, false);
            portrait.SetActive(false);
            board.portraitRoot = portrait;
            var art = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, landscape.transform);
            art.transform.localRotation = Quaternion.Euler(0, 180, 0) * art.transform.localRotation;
            foreach (var renderer in art.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(material =>
                    material == null ? atlas :
                    AssetDatabase.LoadAssetAtPath<Material>(FirstTrailArtMaterials.Folder + material.name.Split('.')[0] + ".mat")
                    ?? AssetDatabase.LoadAssetAtPath<Material>(Hero + "/" + material.name.Split('.')[0] + ".mat")
                    ?? atlas).ToArray();
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            var layoutPath = Diorama + "/FirstTrail8.asset";
            var layout = AssetDatabase.LoadAssetAtPath<DioramaLayout>(layoutPath);
            if (layout == null)
            {
                layout = ScriptableObject.CreateInstance<DioramaLayout>();
                AssetDatabase.CreateAsset(layout, layoutPath);
            }
            layout.cellIds = map.boardLayout.cells.Select(cell => cell.cellId).ToArray();
            layout.landscape = new Vector3[8];
            var glow = AssetDatabase.LoadAssetAtPath<Material>(Diorama + "/Selection.mat");
            var tiles = art.GetComponentsInChildren<Transform>();
            for (int i = 0; i < 8; i++)
            {
                var tile = tiles.FirstOrDefault(item => item.name.Split('.')[0] == "TrailCell_" + i.ToString("00"));
                if (tile == null || tile.GetComponent<Renderer>() == null)
                    throw new InvalidOperationException("Missing first-trail stone " + i);
                var renderer = tile.GetComponent<Renderer>();
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                    FirstTrailArtMaterials.Folder + "TrialStone" + (i % 2) + ".mat");
                var bounds = renderer.bounds;
                var point = new Vector3(bounds.center.x, bounds.max.y + .012f, bounds.center.z);
                layout.landscape[i] = root.transform.InverseTransformPoint(point);
                var cell = new GameObject("Cell_" + layout.cellIds[i]);
                cell.transform.SetParent(landscape.transform, false);
                cell.transform.position = point;
                var collider = cell.AddComponent<BoxCollider>();
                collider.center = Vector3.down * .12f;
                collider.size = new Vector3(1.05f, .33f, 1.04f);
                var marker = cell.AddComponent<DioramaCell>();
                marker.cellId = layout.cellIds[i];
                var highlight = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                highlight.name = "Available move";
                highlight.transform.SetParent(cell.transform, false);
                highlight.transform.localScale = new Vector3(.96f, .005f, .96f);
                UnityEngine.Object.DestroyImmediate(highlight.GetComponent<Collider>());
                marker.highlight = highlight.GetComponent<Renderer>();
                marker.highlight.sharedMaterial = glow;
                marker.highlight.enabled = false;
            }
            layout.portrait = (Vector3[])layout.landscape.Clone();
            layout.landscapeSize = new Vector3(9.8f, 2, 7.4f);
            layout.portraitSize = layout.landscapeSize;
            EditorUtility.SetDirty(layout);
            board.layout = layout;
            var stage = root.AddComponent<WoodlandHeroStage>();
            stage.cameraSize = 3.65f;
            stage.pipelineOverride = board.desktopPipeline;
            stage.grade = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/07_Art/FirstTrail/ReferenceGrade.asset");
            stage.water = AssetDatabase.LoadAssetAtPath<Material>(Hero + "/HeroWater.mat");
            stage.sunIntensity = 2.15f;
            stage.fillIntensity = .48f;
            stage.perspectiveShowcase = true;
            var life = root.AddComponent<FirstTrailLife>();
            life.pennant = tiles.FirstOrDefault(item => item.name.StartsWith("Exit pennant", StringComparison.Ordinal));
            life.creek = art.GetComponentsInChildren<Renderer>()
                .FirstOrDefault(item => item.name.StartsWith("Water creek flowing", StringComparison.Ordinal));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Diorama + "/FirstTrail8.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
