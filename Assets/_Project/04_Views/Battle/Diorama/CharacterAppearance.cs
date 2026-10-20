using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Diceforge.View
{
    /// <summary>Shared matte body, reflective eyes and stable ambient light for the cast.</summary>
    public sealed class CharacterAppearance : MonoBehaviour
    {
        public const uint PortraitRenderingLayer = 128;
        public const int PortraitRendererIndex = 1;
        public CharacterPresentationProfile profile;
        private static SphericalHarmonicsL2[] BoardAmbient, PortraitAmbient;

        private static SphericalHarmonicsL2[] Ambient(Color color)
        {
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(color);
            return new[] { probe };
        }

        private void Awake() => Apply(false);

        public void Apply(bool portrait)
        {
            if (profile == null) return;
            if (BoardAmbient == null)
            {
                BoardAmbient = Ambient(new Color(.26f, .28f, .30f));
                PortraitAmbient = Ambient(new Color(.30f, .32f, .35f));
            }
            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.sharedMaterials = renderer.sharedMesh.subMeshCount == 2
                    ? new[] { profile.bodyMaterial, profile.eyeMaterial } : new[] { profile.bodyMaterial };
                renderer.renderingLayerMask = portrait ? PortraitRenderingLayer : 1;
                renderer.lightProbeUsage = LightProbeUsage.CustomProvided;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.shadowCastingMode = portrait ? ShadowCastingMode.Off : ShadowCastingMode.On;
                renderer.receiveShadows = !portrait;
                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                properties.CopySHCoefficientArraysFrom(portrait ? PortraitAmbient : BoardAmbient);
                properties.SetVector("unity_ProbesOcclusion", Vector4.one);
                renderer.SetPropertyBlock(properties);
            }
        }

        public static void CreatePortraitLights(Transform stage)
        {
            AddLight(stage, "Portrait Key", new Color(1, .97f, .92f), 1.05f, new Vector3(25, 155, 0));
            AddLight(stage, "Portrait Fill", new Color(.79f, .87f, 1), .28f, new Vector3(18, 215, 0));
            var grade = new GameObject("Portrait color grade"); grade.layer = 30;
            grade.transform.SetParent(stage, false);
            var volume = grade.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 100;
            volume.sharedProfile = Resources.Load<VolumeProfile>("Characters/Presentation/PortraitGrade");
        }

        public static void ConfigurePortraitCamera(Camera camera)
        {
            // Fill the render target before the main camera composites the dialogue UI.
            camera.depth = -50;
            var data = camera.GetUniversalAdditionalCameraData();
            data.SetRenderer(PortraitRendererIndex);
            camera.allowHDR = true;
            data.renderPostProcessing = true;
            data.volumeLayerMask = 1 << 30;
            data.renderShadows = false;
        }

        private static void AddLight(Transform stage, string name, Color color, float intensity, Vector3 angles)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.gameObject.layer = 30;
            light.transform.SetParent(stage, false);
            light.transform.localRotation = Quaternion.Euler(angles);
            light.type = LightType.Directional;
            light.color = color; light.intensity = intensity;
            light.cullingMask = 1 << 30; light.shadows = LightShadows.None;
            light.GetUniversalAdditionalLightData().renderingLayers = PortraitRenderingLayer;
        }
    }
}

