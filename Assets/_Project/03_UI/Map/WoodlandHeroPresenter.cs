using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace Diceforge.Map
{
    /// <summary>Small, independently lit 3D portrait. Owns every transient rendering resource.</summary>
    public sealed class WoodlandHeroPresenter : MonoBehaviour
    {
        private const int PortraitLayer = 30;
        private GameObject studio;
        private Camera portraitCamera;
        private RenderTexture texture;
        private Image image;
        private Transform hero;
        private Quaternion restRotation;
        private float nextFrame;
        private float greetingUntil;

        public void Show(Image target)
        {
            if (image == target && studio != null) return;
            StopPresentation();
            var prefab = Resources.Load<GameObject>("Map/WoodlandHero");
            if (prefab == null)
            {
                Debug.LogWarning("[WoodlandMap] Hero presentation prefab is missing.", this);
                return;
            }
            image = target;
            studio = new GameObject("WoodlandHeroStudio") { hideFlags = HideFlags.DontSave };
            studio.transform.SetParent(transform, false);
            studio.transform.position = new Vector3(10000f, 10000f, 10000f);
            var model = Instantiate(prefab, studio.transform);
            hero = model.transform;
            restRotation = hero.localRotation;
            foreach (var child in model.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = PortraitLayer;
            var animator = model.GetComponentInChildren<Animator>();
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;
            animator.Rebind();
            animator.Update(0f);

            var renderers = model.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float height = bounds.size.y;
            var cameraObject = new GameObject("WoodlandHeroCamera");
            cameraObject.transform.SetParent(studio.transform, false);
            portraitCamera = cameraObject.AddComponent<Camera>();
            portraitCamera.enabled = false;
            portraitCamera.orthographic = true;
            portraitCamera.orthographicSize = Mathf.Max(height * .45f, bounds.size.x * .45f);
            portraitCamera.clearFlags = CameraClearFlags.SolidColor;
            portraitCamera.backgroundColor = Color.clear;
            portraitCamera.cullingMask = 1 << PortraitLayer;
            portraitCamera.nearClipPlane = .01f;
            portraitCamera.farClipPlane = height * 12f;
            portraitCamera.allowHDR = false;
            portraitCamera.allowMSAA = false;
            portraitCamera.useOcclusionCulling = false;
            portraitCamera.transform.position = bounds.center + new Vector3(0, height * .7f, -height * 4f);
            portraitCamera.transform.LookAt(bounds.center);
            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = false;
            cameraData.renderShadows = false;
            cameraData.requiresColorTexture = false;
            cameraData.requiresDepthTexture = false;

            AddLight("WarmKey", bounds.center + new Vector3(-height, height * 2, -height * 2), height, new Color(1f, .87f, .66f), 3f);
            AddLight("SoftFill", bounds.center + new Vector3(height * 2, height, -height), height, new Color(.72f, .85f, 1f), 1.5f);
            texture = new RenderTexture(384, 384, 24, RenderTextureFormat.ARGB32)
            {
                name = "WoodlandHeroPortrait", hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            texture.Create();
            portraitCamera.targetTexture = texture;
            image.image = texture;
            image.scaleMode = ScaleMode.ScaleToFit;
            nextFrame = 0;
            greetingUntil = Time.unscaledTime + 1.3f;
        }

        private void AddLight(string lightName, Vector3 position, float height, Color color, float intensity)
        {
            var source = new GameObject(lightName);
            source.transform.SetParent(studio.transform, false);
            source.transform.position = position;
            var light = source.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = height * 7f;
            light.cullingMask = 1 << PortraitLayer;
            light.shadows = LightShadows.None;
        }

        private void LateUpdate()
        {
            if (studio == null) return;
            if (image == null || image.panel == null || !image.visible || image.worldBound.width < 1f)
            {
                StopPresentation();
                return;
            }
            // A brief greeting turn; breathing/head motion comes from the existing skeletal idle.
            float remaining = Mathf.Max(0, greetingUntil - Time.unscaledTime);
            hero.localRotation = restRotation * Quaternion.Euler(0, Mathf.Sin(remaining / 1.3f * Mathf.PI * 2f) * remaining * 5f, 0);
            if (Time.unscaledTime < nextFrame) return;
            nextFrame = Time.unscaledTime + 1f / 30f;
            portraitCamera.Render();
        }

        public void StopPresentation()
        {
            if (image != null) image.image = null;
            if (portraitCamera != null) portraitCamera.targetTexture = null;
            if (texture != null) { texture.Release(); Destroy(texture); }
            if (studio != null) { studio.SetActive(false); Destroy(studio); }
            image = null;
            studio = null;
            texture = null;
            portraitCamera = null;
            hero = null;
        }

        private void OnDisable() => StopPresentation();
        private void OnDestroy() => StopPresentation();
    }
}

