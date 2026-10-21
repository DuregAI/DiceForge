#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Diceforge.View.Editor
{
    [InitializeOnLoad, DefaultExecutionOrder(10000)]
    public sealed class LivingForestMovie : MonoBehaviour
    {
        private const string Pending = "Diceforge.LivingForestMovie.Pending";
        private const string Output = "docs/Validation/LivingForest/Beta-v03/MovieFrames";
        private Camera view;
        private RenderTexture target;
        private Texture2D image;
        private ForestWaterReflections reflections;
        private LivingForestForeground foreground;
        private GameObject luma;
        private RenderPipelineAsset originalPipeline;
        private bool configured, cleaned, originalBackground;
        private int frame, originalRate;
        private float originalScale;

        static LivingForestMovie()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
                SessionState.SetBool(Pending, false);
                new GameObject("Temporary living forest review").AddComponent<LivingForestMovie>();
            };
        }

        public static void Record()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Finish the current play session before recording.");
            SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
        }

        private IEnumerator Start()
        {
            yield return null;
            originalRate = Time.captureFramerate; originalScale = Time.timeScale;
            originalBackground = Application.runInBackground; originalPipeline = QualitySettings.renderPipeline;
            Time.captureFramerate = 24; Time.timeScale = 1; Application.runInBackground = true;
            var previousScene = SceneManager.GetActiveScene();
            var scene = SceneManager.CreateScene("Temporary forest art review");
            SceneManager.MoveGameObjectToScene(gameObject, scene); SceneManager.SetActiveScene(scene);
            foreach (var root in previousScene.GetRootGameObjects()) root.SetActive(false);
            view = new GameObject("Forest review camera").AddComponent<Camera>();
            view.tag = "MainCamera"; view.enabled = true; view.orthographic = true; view.aspect = 16f / 9;
            view.nearClipPlane = .1f; view.farClipPlane = 100;
            view.cullingMask = ~((1 << 5) | (1 << 30));
            var data = view.GetUniversalAdditionalCameraData(); data.renderPostProcessing = true;
            data.requiresDepthTexture = true; data.requiresColorTexture = true;
            var sun = new GameObject("Review sunlight").AddComponent<Light>(); sun.type = LightType.Directional;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/05_Gameplay_Data/Battle/Diorama/FirstTrail8.prefab");
            var world = Instantiate(prefab); var board = world.GetComponent<DioramaBoard>();
            reflections=world.GetComponentInChildren<ForestWaterReflections>();
            foreground=world.GetComponentInChildren<LivingForestForeground>();
            board.Initialize(); board.ConfigureTrail(true, 4, 1.15f);
            var hero = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/07_Art/Characters/TishTripo/Tish.prefab"));
            hero.transform.position = board.CellPosition(4) + Vector3.up * .02f;
            hero.transform.localScale = Vector3.one * .78f; hero.transform.rotation = Quaternion.Euler(0, 180, 0);
            hero.GetComponentInChildren<CharacterAppearance>()?.Apply(false);
            luma=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/07_Art/DemoRCCharacters/Prefabs/Luma.prefab"));
            luma.transform.position=board.CellPosition(5)+Vector3.up*.02f;
            luma.transform.localScale=Vector3.one*.62f;luma.transform.rotation=Quaternion.Euler(0,180,0);
            luma.GetComponentInChildren<CharacterAppearance>()?.Apply(false);
            // These models are posed for an art review, without a game board mover.
            foreach(var token in new[]{hero,luma})
            {
                var mover=token.GetComponent<BoardLayoutTokenMover>();
                if(mover!=null)mover.enabled=false;
            }
            yield return null; yield return null;
            board.GetComponent<DioramaCameraController>().enabled = false;
            view.enabled=false;
            target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32); target.Create();
            image = new Texture2D(1920, 1080, TextureFormat.RGB24, false); view.targetTexture = target;
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output+"/complete.json","{\"frames\":0,\"status\":\"recording\"}");
            Pose(false);
            // Let the actual render loop initialize materials, shadows and reflection cameras.
            for (int i = 0; i < 30; i++) { reflections.Capture(view); view.Render(); yield return null; }
            configured = true;
        }

        private void Pose(bool close)
        {
            view.orthographicSize = close ? 2.25f : 3.7f;
            view.transform.rotation = Quaternion.Euler(close ? 39 : 50, 0, 0);
            view.transform.position = (close ? new Vector3(1.65f, .3f, .55f) : new Vector3(0, .4f, .4f)) - view.transform.forward * 25;
            foreground?.Frame(view);
        }

        private void LateUpdate()
        {
            if (!configured) return;
            if (frame == 144) Pose(true);
            var active = RenderTexture.active;
            reflections.Capture(view);
            view.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/" + frame.ToString("0000") + ".png", image.EncodeToPNG());
            if (frame == 48 || frame == 192)
                File.WriteAllBytes("docs/Validation/LivingForest/Beta-v03/" + (frame == 48 ? "overview-runtime.png" : "close-runtime.png"), image.EncodeToPNG());
            RenderTexture.active = active;
            EditorApplication.QueuePlayerLoopUpdate();
            if (++frame < 288) return;
            File.WriteAllText(Output + "/complete.json", "{\"frames\":288,\"fps\":24,\"seconds\":12,\"source\":\"Unity runtime, actual FirstTrail8 prefab; art review camera, two fixed shots\"}");
            Cleanup();
            EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
        }

        private void Cleanup()
        {
            if (cleaned) return; cleaned = true; configured = false;
            Time.captureFramerate = originalRate; Time.timeScale = originalScale;
            Application.runInBackground = originalBackground; QualitySettings.renderPipeline = originalPipeline;
            if (view != null) view.targetTexture = null;
            if (target != null) { target.Release(); Destroy(target); }
            if (image != null) Destroy(image);
        }
        private void OnDestroy() { if (target != null && !cleaned) Cleanup(); }
    }
}
#endif
