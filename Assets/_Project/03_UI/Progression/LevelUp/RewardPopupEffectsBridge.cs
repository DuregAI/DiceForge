using System;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class RewardPopupEffectsBridge : MonoBehaviour
{
    private readonly struct RewardPopupEffectPreset
    {
        public RewardPopupEffectPreset(int burstCount, float ambientRate, Color primaryColor, Color secondaryColor)
        {
            BurstCount = burstCount;
            AmbientRate = ambientRate;
            PrimaryColor = primaryColor;
            SecondaryColor = secondaryColor;
        }

        public int BurstCount { get; }
        public float AmbientRate { get; }
        public Color PrimaryColor { get; }
        public Color SecondaryColor { get; }
    }

    private const string DefaultPresetId = "level_up_arcane";
    private const string StarParticleShaderName = "Hidden/Diceforge/LevelUpStarParticle";
    private const string ParticleMaterialResourcePath = "UI/LevelUp/LevelUpStarParticle";
    private const int EffectLayer = 31;
    private const float PixelsPerUnit = 100f;
    private const float CameraDistance = 10f;

    private Camera _effectCamera;
    private GameObject _runtimeRoot;
    private Transform _ambientAnchor;
    private Transform _burstAnchor;
    private ParticleSystem _burstEffect;
    private ParticleSystem _ambientEffect;
    private Material _particleMaterial;
    private RenderTexture _renderTexture;
    private Image _fxLayerImage;
    private VisualElement _anchorElement;
    private RewardPopupEffectPreset _activePreset;
    private Vector2Int _renderTextureSize;
    private bool _missingShaderLogged;

    public void BeginPresentation(string presetId, Image fxLayerImage, VisualElement anchorElement)
    {
        StopActivePresentation();

        if (fxLayerImage == null)
        {
            Debug.LogError("[RewardPopupEffectsBridge] Missing FX layer image for reward popup particles.", this);
            return;
        }

        _fxLayerImage = fxLayerImage;
        _anchorElement = anchorElement;
        _activePreset = ResolvePreset(presetId);
        _particleMaterial = CreateParticleMaterial();
        if (_particleMaterial == null)
        {
            StopActivePresentation();
            return;
        }

        _runtimeRoot = new GameObject("RewardPopupEffectsRuntime");
        _runtimeRoot.hideFlags = HideFlags.DontSave;
        _runtimeRoot.transform.SetParent(transform, false);
        SetLayerRecursively(_runtimeRoot, EffectLayer);

        _ambientAnchor = CreateChildAnchor(_runtimeRoot.transform, "RewardPopupAmbientAnchor");
        _burstAnchor = CreateChildAnchor(_runtimeRoot.transform, "RewardPopupBurstAnchor");

        _ambientEffect = CreateAmbientEffect(_ambientAnchor, _activePreset, _particleMaterial);
        _burstEffect = CreateBurstEffect(_burstAnchor, _activePreset, _particleMaterial);
        _effectCamera = CreateEffectCamera(_runtimeRoot.transform);

        UpdateEffectLayout(true);

        if (_ambientEffect != null)
            _ambientEffect.Play(true);
        if (_burstEffect != null)
            _burstEffect.Play(true);
    }

    public void StopActivePresentation()
    {
        _anchorElement = null;

        if (_burstEffect != null)
            _burstEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_ambientEffect != null)
            _ambientEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (_effectCamera != null)
            _effectCamera.targetTexture = null;

        if (_fxLayerImage != null)
            _fxLayerImage.image = null;

        if (_renderTexture != null)
        {
            _renderTexture.Release();
            Destroy(_renderTexture);
        }

        if (_particleMaterial != null)
            Destroy(_particleMaterial);

        if (_runtimeRoot != null)
            Destroy(_runtimeRoot);

        _effectCamera = null;
        _runtimeRoot = null;
        _ambientAnchor = null;
        _burstAnchor = null;
        _burstEffect = null;
        _ambientEffect = null;
        _particleMaterial = null;
        _renderTexture = null;
        _fxLayerImage = null;
        _renderTextureSize = Vector2Int.zero;
    }

    private void LateUpdate()
    {
        if (_effectCamera == null || _runtimeRoot == null || _fxLayerImage == null)
            return;

        if (_fxLayerImage.panel == null)
        {
            StopActivePresentation();
            return;
        }

        if (!UpdateEffectLayout(false))
            return;

        _effectCamera.Render();
    }

    private void OnDestroy()
    {
        StopActivePresentation();
    }

    private bool UpdateEffectLayout(bool forceResize)
    {
        Rect fxRect = _fxLayerImage != null ? _fxLayerImage.worldBound : default;
        if (fxRect.width < 1f || fxRect.height < 1f)
            return false;

        Vector2Int textureSize = GetRenderTextureSize(fxRect);
        if (forceResize || textureSize != _renderTextureSize)
            CreateOrResizeRenderTexture(textureSize);

        if (_effectCamera == null || _renderTexture == null)
            return false;

        _effectCamera.transform.localPosition = new Vector3(0f, 0f, -CameraDistance);
        _effectCamera.transform.localRotation = Quaternion.identity;
        _effectCamera.orthographic = true;
        _effectCamera.orthographicSize = textureSize.y / (PixelsPerUnit * 2f);
        _effectCamera.nearClipPlane = 0.1f;
        _effectCamera.farClipPlane = CameraDistance + 10f;

        if (_ambientAnchor != null)
            _ambientAnchor.localPosition = Vector3.zero;

        if (_burstAnchor != null)
        {
            Rect anchorRect = ResolveAnchorRect(fxRect);
            Vector2 anchorOffset = anchorRect.center - fxRect.center;
            _burstAnchor.localPosition = new Vector3(anchorOffset.x / PixelsPerUnit, -anchorOffset.y / PixelsPerUnit, 0f);

            float anchorScale = Mathf.Clamp(Mathf.Max(anchorRect.width, anchorRect.height) / 180f, 0.92f, 1.3f);
            _burstAnchor.localScale = Vector3.one * anchorScale;
        }

        UpdateAmbientBounds(fxRect);
        return true;
    }

    private void UpdateAmbientBounds(Rect fxRect)
    {
        if (_ambientEffect == null)
            return;

        var shape = _ambientEffect.shape;
        float width = Mathf.Clamp((fxRect.width / PixelsPerUnit) * 0.78f, 5.2f, 8.4f);
        float height = Mathf.Clamp((fxRect.height / PixelsPerUnit) * 0.64f, 4.2f, 6.3f);
        shape.scale = new Vector3(width, height, 0.1f);
    }

    private void CreateOrResizeRenderTexture(Vector2Int textureSize)
    {
        if (_renderTexture != null)
        {
            if (_renderTextureSize == textureSize)
                return;

            _renderTexture.Release();
            Destroy(_renderTexture);
        }

        var descriptor = new RenderTextureDescriptor(textureSize.x, textureSize.y, RenderTextureFormat.ARGB32, 24)
        {
            msaaSamples = 1,
            useMipMap = false,
            autoGenerateMips = false,
            sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear
        };

        _renderTexture = new RenderTexture(descriptor)
        {
            name = "RewardPopupEffectsRT",
            hideFlags = HideFlags.DontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        _renderTexture.Create();
        _renderTextureSize = textureSize;

        if (_effectCamera != null)
            _effectCamera.targetTexture = _renderTexture;

        if (_fxLayerImage != null)
        {
            _fxLayerImage.scaleMode = ScaleMode.StretchToFill;
            _fxLayerImage.image = _renderTexture;
        }
    }

    private static Vector2Int GetRenderTextureSize(Rect fxRect)
    {
        return new Vector2Int(
            Mathf.Max(32, Mathf.RoundToInt(fxRect.width)),
            Mathf.Max(32, Mathf.RoundToInt(fxRect.height)));
    }

    private Rect ResolveAnchorRect(Rect fxRect)
    {
        Rect anchorRect = _anchorElement != null ? _anchorElement.worldBound : default;
        if (anchorRect.width < 1f || anchorRect.height < 1f)
            anchorRect = new Rect(fxRect.center.x - 1f, fxRect.center.y - 1f, 2f, 2f);

        Vector2 clampedCenter = new Vector2(
            Mathf.Clamp(anchorRect.center.x, fxRect.xMin, fxRect.xMax),
            Mathf.Clamp(anchorRect.center.y, fxRect.yMin, fxRect.yMax));

        return new Rect(
            clampedCenter.x - (anchorRect.width * 0.5f),
            clampedCenter.y - (anchorRect.height * 0.5f),
            anchorRect.width,
            anchorRect.height);
    }

    private Material CreateParticleMaterial()
    {
        Material templateMaterial = Resources.Load<Material>(ParticleMaterialResourcePath);
        if (templateMaterial != null)
        {
            return new Material(templateMaterial)
            {
                name = "RewardPopupStarParticleMaterial",
                hideFlags = HideFlags.DontSave
            };
        }

        Shader shader = Shader.Find(StarParticleShaderName);
        if (shader == null)
        {
            if (!_missingShaderLogged)
            {
                Debug.LogError($"[RewardPopupEffectsBridge] Missing particle shader '{StarParticleShaderName}'.", this);
                _missingShaderLogged = true;
            }

            return null;
        }

        return new Material(shader)
        {
            name = "RewardPopupStarParticleMaterial",
            hideFlags = HideFlags.DontSave
        };
    }

    private static Camera CreateEffectCamera(Transform parent)
    {
        var cameraObject = new GameObject("RewardPopupEffectsCamera");
        cameraObject.hideFlags = HideFlags.DontSave;
        cameraObject.transform.SetParent(parent, false);

        var camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.cullingMask = 1 << EffectLayer;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.useOcclusionCulling = false;

        AttachUniversalCameraDataIfAvailable(cameraObject);
        return camera;
    }

    private static Transform CreateChildAnchor(Transform parent, string objectName)
    {
        var child = new GameObject(objectName);
        child.hideFlags = HideFlags.DontSave;
        child.transform.SetParent(parent, false);
        SetLayerRecursively(child, EffectLayer);
        return child.transform;
    }

    private static RewardPopupEffectPreset ResolvePreset(string presetId)
    {
        if (string.IsNullOrWhiteSpace(presetId) || presetId == DefaultPresetId)
        {
            return new RewardPopupEffectPreset(
                78,
                15f,
                new Color(1f, 0.84f, 0.36f, 1f),
                new Color(0.74f, 0.92f, 1f, 0.95f));
        }

        return new RewardPopupEffectPreset(
            60,
            12f,
            new Color(1f, 0.82f, 0.44f, 1f),
            new Color(0.76f, 0.93f, 1f, 0.9f));
    }

    private static ParticleSystem CreateBurstEffect(Transform parent, RewardPopupEffectPreset preset, Material material)
    {
        var burstObject = new GameObject("RewardPopupBurst");
        burstObject.hideFlags = HideFlags.DontSave;
        burstObject.transform.SetParent(parent, false);
        SetLayerRecursively(burstObject, EffectLayer);

        var particleSystem = burstObject.AddComponent<ParticleSystem>();
        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = particleSystem.main;
        main.duration = 1.55f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.05f, 1.85f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.45f, 6.15f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.24f, 0.42f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(preset.PrimaryColor, preset.SecondaryColor);
        main.maxParticles = 260;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.useUnscaledTime = true;

        var emission = particleSystem.emission;
        emission.enabled = true;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)(preset.BurstCount + 42)),
            new ParticleSystem.Burst(0.18f, 26),
            new ParticleSystem.Burst(0.42f, 18)
        });

        var shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.42f;
        shape.radiusThickness = 0.15f;

        var sizeOverLifetime = particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.48f, 0.34f, 1.6f));

        var colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(preset.PrimaryColor, 0f),
                new GradientColorKey(preset.SecondaryColor, 0.45f),
                new GradientColorKey(new Color(1f, 0.97f, 0.88f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.14f),
                new GradientAlphaKey(0.8f, 0.68f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        var noise = particleSystem.noise;
        noise.enabled = true;
        noise.strength = 0.24f;
        noise.frequency = 0.72f;
        noise.scrollSpeed = 0.34f;

        var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortMode = ParticleSystemSortMode.YoungestInFront;
        renderer.minParticleSize = 0.01f;
        renderer.maxParticleSize = 0.36f;

        return particleSystem;
    }

    private static ParticleSystem CreateAmbientEffect(Transform parent, RewardPopupEffectPreset preset, Material material)
    {
        var ambientObject = new GameObject("RewardPopupAmbient");
        ambientObject.hideFlags = HideFlags.DontSave;
        ambientObject.transform.SetParent(parent, false);
        SetLayerRecursively(ambientObject, EffectLayer);

        var particleSystem = ambientObject.AddComponent<ParticleSystem>();
        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = particleSystem.main;
        main.duration = 6.8f;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3.4f, 7.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.06f, 0.24f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.11f, 0.22f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(preset.SecondaryColor, new Color(1f, 0.94f, 0.68f, 0.85f));
        main.maxParticles = 340;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.useUnscaledTime = true;

        var emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = preset.AmbientRate * 2.25f;

        var shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(6.6f, 5.1f, 0.1f);

        var velocityOverLifetime = particleSystem.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(preset.SecondaryColor, 0f),
                new GradientColorKey(preset.PrimaryColor, 0.55f),
                new GradientColorKey(new Color(1f, 0.98f, 0.92f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.42f, 0.16f),
                new GradientAlphaKey(0.36f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        var noise = particleSystem.noise;
        noise.enabled = true;
        noise.strength = 0.09f;
        noise.frequency = 0.44f;
        noise.scrollSpeed = 0.14f;

        var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortMode = ParticleSystemSortMode.OldestInFront;
        renderer.minParticleSize = 0.008f;
        renderer.maxParticleSize = 0.3f;

        return particleSystem;
    }

    private static void AttachUniversalCameraDataIfAvailable(GameObject cameraObject)
    {
        Type additionalCameraDataType = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (additionalCameraDataType == null || cameraObject.GetComponent(additionalCameraDataType) != null)
            return;

        cameraObject.AddComponent(additionalCameraDataType);
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null)
            return;

        target.layer = layer;
        for (int i = 0; i < target.transform.childCount; i++)
            SetLayerRecursively(target.transform.GetChild(i).gameObject, layer);
    }
}
