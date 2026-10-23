using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Diceforge.View.Editor
{
    /// <summary>Repeatable art review of the shipped prefab, with no player-save changes.</summary>
    public static class LivingForestReview
    {
        public static void Capture(string output, float time = 3f, bool close = false, string prefabPath = null, bool foliageStudy = false, bool heroContour = false, bool lakeReview = false, int imageWidth = 1600, bool saveReflections = true, string chapterLevel = null)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) throw new InvalidOperationException("Finish Play Mode/player build before taking an art review.");
            var originalScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var originalPipeline = QualitySettings.renderPipeline;
            var originalTarget = RenderTexture.active;
            bool originalAsync = ShaderUtil.allowAsyncCompilation;
            var originalLights = Object.FindObjectsByType<Light>();
            var lightEnabled = new bool[originalLights.Length];
            for (int i = 0; i < originalLights.Length; i++) { lightEnabled[i] = originalLights[i].enabled; originalLights[i].enabled = false; }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            RenderTexture target = null;
            Texture2D image = null;
            ForestWaterReflections reflections=null;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath ?? "Assets/_Project/05_Gameplay_Data/Battle/Diorama/FirstTrail8.prefab");
                var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var board = root.GetComponent<DioramaBoard>();
                root.GetComponentInChildren<TrailChapterDressing>(true)?.Apply(chapterLevel ?? "L01");
                board.landscapeRoot.SetActive(true); board.portraitRoot.SetActive(false);
                if (foliageStudy)
                {
                    board.Initialize(); board.ConfigureTrail(true, 4, 1.15f);
                    var hero = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/07_Art/Characters/TishTripo/Tish.prefab"), scene);
                    hero.transform.position = board.CellPosition(4) + Vector3.up * .02f;
                    hero.transform.localScale = Vector3.one * .78f; hero.transform.rotation = Quaternion.Euler(0, 180, 0);
                    hero.GetComponentInChildren<CharacterAppearance>()?.Apply(false);
                    var mover = hero.GetComponent<BoardLayoutTokenMover>(); if (mover != null) mover.enabled = false;
                    if (heroContour) DioramaStyleSetup.AddContour(hero);
                }
                var stage = root.GetComponent<WoodlandHeroStage>();
                QualitySettings.renderPipeline = stage.pipelineOverride;
                RenderSettings.fog = stage.fogEnabled; RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = stage.fogColor;
                RenderSettings.fogStartDistance = stage.fogStartDistance; RenderSettings.fogEndDistance = stage.fogEndDistance;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = stage.ambientSkyColor;
                RenderSettings.ambientEquatorColor = stage.ambientEquatorColor;
                RenderSettings.ambientGroundColor = stage.ambientGroundColor;
                var ambient = new SphericalHarmonicsL2(); ambient.AddAmbientLight(stage.ambientProbeColor); RenderSettings.ambientProbe = ambient;
                var sun = AddLight("Forest review sun", stage.sunEuler, stage.sunColor, stage.sunIntensity, true);
                sun.shadowStrength = stage.sunShadowStrength; sun.shadowBias = stage.sunShadowBias; sun.shadowNormalBias = stage.sunShadowNormalBias;
                AddLight("Forest review fill", stage.fillEuler, stage.fillColor, stage.fillIntensity, false);
                var volume = new GameObject("Forest review grade").AddComponent<Volume>();
                volume.isGlobal = true; volume.priority = 100; volume.sharedProfile = stage.grade;
                var camera = new GameObject("Forest review camera").AddComponent<Camera>();
                camera.enabled = false; camera.orthographic = true; camera.aspect = 16f / 9;
                camera.orthographicSize = close ? 2.2f : 3.8f;
                camera.nearClipPlane = .1f; camera.farClipPlane = 100;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = RenderSettings.fogColor;
                camera.cullingMask = ~((1 << 5) | (1 << 30));
                camera.transform.rotation = Quaternion.Euler(close ? 39 : 50, 0, 0);
                camera.transform.position = (close ? new Vector3(1.7f, .3f, .6f) : new Vector3(0, .4f, .4f)) - camera.transform.forward * 25;
                if (foliageStudy)
                {
                    camera.orthographicSize = 1.8f;
                    camera.transform.rotation = Quaternion.Euler(42, 0, 0);
                    camera.transform.position = new Vector3(-.15f, .3f, -.65f) - camera.transform.forward * 25;
                }
                if (lakeReview)
                {
                    camera.orthographicSize = 2.3f;
                    camera.transform.rotation = Quaternion.Euler(50, 0, 0);
                    camera.transform.position = new Vector3(2.2f, -.5f, -1.65f) - camera.transform.forward * 25;
                }
                var data = camera.GetUniversalAdditionalCameraData(); data.renderPostProcessing = true; data.SetRenderer(0);
                data.requiresColorTexture = true; data.requiresDepthTexture = true;
                root.GetComponentInChildren<LivingForestMotion>(true).Apply(time);
                root.GetComponentInChildren<LivingForestForeground>(true)?.Frame(camera);
                reflections = root.GetComponentInChildren<ForestWaterReflections>(true);
                reflections.Capture(camera);
                int imageHeight = Mathf.RoundToInt(imageWidth * 9f / 16);
                target = new RenderTexture(imageWidth, imageHeight, 24, RenderTextureFormat.ARGB32); target.Create();
                image = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
                camera.targetTexture = target;
                // Editor shader variants and render-graph resources need a warm draw.
                // Player builds compile these variants during the build, not at capture time.
                camera.Render(); reflections.Capture(camera); camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0); image.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllBytes(output, image.EncodeToPNG());
                var block = new MaterialPropertyBlock();
                foreach (var plane in saveReflections ? reflections.planes : Array.Empty<ForestWaterReflections.Plane>())
                {
                    plane.surface.GetPropertyBlock(block);
                    var reflection = block.GetTexture("_PlanarReflectionTex") as RenderTexture;
                    if (reflection == null) continue;
                    RenderTexture.active = reflection;
                    var pixels = new Texture2D(reflection.width, reflection.height, TextureFormat.RGB24, false);
                    pixels.ReadPixels(new Rect(0, 0, reflection.width, reflection.height), 0, 0); pixels.Apply();
                    File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(output), "reflection-" + plane.surface.name + ".png"), pixels.EncodeToPNG());
                    Object.DestroyImmediate(pixels);
                }
                camera.targetTexture = null;
            }
            finally
            {
                RenderTexture.active = originalTarget;
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                if (image != null) Object.DestroyImmediate(image);
                if (reflections != null) reflections.ReleaseResources();
                EditorSceneManager.CloseScene(scene, true);
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(originalScene);
                QualitySettings.renderPipeline = originalPipeline; ShaderUtil.allowAsyncCompilation = originalAsync;
                for (int i = 0; i < originalLights.Length; i++) if (originalLights[i] != null) originalLights[i].enabled = lightEnabled[i];
            }
        }

        private static Light AddLight(string name, Vector3 angles, Color color, float intensity, bool shadows)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.type = LightType.Directional; light.color = color; light.intensity = intensity;
            light.transform.rotation = Quaternion.Euler(angles);
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            light.shadowBias = .035f; light.shadowNormalBias = .15f;
            return light;
        }
    }
}
