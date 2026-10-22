using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Diceforge.View
{
    /// <summary>One owner captures the shared lighting before a board or standalone art stage changes it.</summary>
    internal sealed class DioramaLightingState
    {
        private readonly Scene scene = SceneManager.GetActiveScene();
        private readonly bool fog = RenderSettings.fog;
        private readonly FogMode fogMode = RenderSettings.fogMode;
        private readonly Color fogColor = RenderSettings.fogColor;
        private readonly float fogStart = RenderSettings.fogStartDistance;
        private readonly float fogEnd = RenderSettings.fogEndDistance;
        private readonly AmbientMode ambientMode = RenderSettings.ambientMode;
        private readonly Color ambientSky = RenderSettings.ambientSkyColor;
        private readonly Color ambientEquator = RenderSettings.ambientEquatorColor;
        private readonly Color ambientGround = RenderSettings.ambientGroundColor;
        private readonly SphericalHarmonicsL2 ambientProbe = RenderSettings.ambientProbe;
        private readonly DirectionalState[] lights;

        public DioramaLightingState()
        {
            var found = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            var states = new System.Collections.Generic.List<DirectionalState>();
            foreach (var light in found)
                if (light.type == LightType.Directional && light.gameObject.layer != 30)
                    states.Add(new DirectionalState(light));
            lights = states.ToArray();
        }

        public void Restore()
        {
            // A scene transition may already have installed the next scene's RenderSettings.
            if (scene.IsValid() && scene.isLoaded && SceneManager.GetActiveScene() == scene)
            {
                RenderSettings.fog = fog; RenderSettings.fogMode = fogMode; RenderSettings.fogColor = fogColor;
                RenderSettings.fogStartDistance = fogStart; RenderSettings.fogEndDistance = fogEnd;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientSkyColor = ambientSky; RenderSettings.ambientEquatorColor = ambientEquator;
                RenderSettings.ambientGroundColor = ambientGround; RenderSettings.ambientProbe = ambientProbe;
            }
            foreach (var light in lights) light.Restore();
        }

        private sealed class DirectionalState
        {
            private readonly Light light;
            private readonly Quaternion rotation;
            private readonly Color color;
            private readonly float intensity, strength, bias, normalBias;
            private readonly LightShadows shadows;
            private readonly LightBakingOutput bakingOutput;
#if UNITY_EDITOR
            private readonly LightmapBakeType bakeType;
#endif
            public DirectionalState(Light light)
            {
                this.light = light; rotation = light.transform.rotation; color = light.color;
                intensity = light.intensity; shadows = light.shadows; strength = light.shadowStrength;
                bias = light.shadowBias; normalBias = light.shadowNormalBias; bakingOutput = light.bakingOutput;
#if UNITY_EDITOR
                bakeType = light.lightmapBakeType;
#endif
            }

            public void Restore()
            {
                if (light == null) return;
                light.transform.rotation = rotation; light.color = color; light.intensity = intensity;
                light.shadows = shadows; light.shadowStrength = strength;
                light.shadowBias = bias; light.shadowNormalBias = normalBias;
#if UNITY_EDITOR
                light.lightmapBakeType = bakeType;
#endif
                light.bakingOutput = bakingOutput;
            }
        }
    }
}
