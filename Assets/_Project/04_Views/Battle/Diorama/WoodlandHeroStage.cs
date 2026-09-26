using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Diceforge.View
{
    /// <summary>Art direction for the single landscape showcase, independent of gameplay.</summary>
    public sealed class WoodlandHeroStage : MonoBehaviour
    {
        public VolumeProfile grade;
        public Material water;
        public float cameraSize=4.65f;
        public float sunIntensity=2.15f;
        public float fillIntensity=.35f;
        public bool perspectiveShowcase;
        public UniversalRenderPipelineAsset pipelineOverride;
        private RenderPipelineAsset previousPipeline;
        private bool initialized;
        private Material waterInstance;
        private Camera view;
        private int width, height;
        private bool previousFog;
        private FogMode previousFogMode;
        private Color previousFogColor;
        private float previousFogStart,previousFogEnd;
        private void Start()
        {
            initialized=true;
            previousPipeline=QualitySettings.renderPipeline;
            if(pipelineOverride!=null)QualitySettings.renderPipeline=pipelineOverride;
            LightmapSettings.lightmaps=System.Array.Empty<LightmapData>();
            previousFog=RenderSettings.fog;previousFogMode=RenderSettings.fogMode;previousFogColor=RenderSettings.fogColor;
            previousFogStart=RenderSettings.fogStartDistance;previousFogEnd=RenderSettings.fogEndDistance;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=new Color(.30f,.38f,.29f);
            RenderSettings.fogStartDistance=27;RenderSettings.fogEndDistance=43;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.57f,.66f,.74f);
            RenderSettings.ambientEquatorColor=new Color(.43f,.47f,.34f);
            RenderSettings.ambientGroundColor=new Color(.23f,.18f,.12f);
            var ambient=new SphericalHarmonicsL2();ambient.AddAmbientLight(new Color(.36f,.41f,.43f));RenderSettings.ambientProbe=ambient;
            foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if(light.type==LightType.Directional)
                {
                    light.transform.rotation=Quaternion.Euler(48,-38,0);
                    light.color=new Color(1,.91f,.76f);light.intensity=sunIntensity;
#if UNITY_EDITOR
                    light.lightmapBakeType=LightmapBakeType.Realtime;
#endif
                    light.bakingOutput=default;
                    light.shadows=LightShadows.Soft;light.shadowBias=.035f;light.shadowNormalBias=.15f;
                }
            var fill=new GameObject("Cool woodland fill");fill.transform.SetParent(transform,false);
            fill.transform.rotation=Quaternion.Euler(35,140,0);var bounce=fill.AddComponent<Light>();
            bounce.type=LightType.Directional;bounce.color=new Color(.60f,.77f,1);bounce.intensity=fillIntensity;bounce.shadows=LightShadows.None;
            var volume=gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=30;volume.sharedProfile=grade;
            view=Camera.main;
            if(view!=null){view.GetUniversalAdditionalCameraData().renderPostProcessing=true;view.backgroundColor=new Color(.30f,.38f,.29f);}
            if(water!=null)
            {
                waterInstance=new Material(water);
                foreach(var renderer in GetComponentsInChildren<Renderer>())if(renderer.sharedMaterial==water)renderer.sharedMaterial=waterInstance;
            }
            Frame();
        }
        private void LateUpdate()
        {
            if(width!=Screen.width || height!=Screen.height)Frame();
            if(waterInstance!=null && !DioramaBoard.ReducedMotion)
                waterInstance.SetTextureOffset("_BumpMap",new Vector2(Time.time*.019f,Time.time*.011f));
        }
        private void Frame()
        {
            width=Screen.width;height=Screen.height;
            if(view==null)return;
            view.transform.rotation=Quaternion.Euler(47,0,0);
            view.orthographicSize=Mathf.Max(cameraSize,cameraSize*1.19f/Mathf.Max(.3f,view.aspect));
            view.orthographic=!perspectiveShowcase;
            if(perspectiveShowcase)view.fieldOfView=2*Mathf.Atan(view.orthographicSize/25)*Mathf.Rad2Deg;
            view.transform.position=new Vector3(0,.2f,0)-view.transform.up*.25f-view.transform.forward*25;
        }
        private void OnDestroy()
        {
            if(waterInstance!=null)Destroy(waterInstance);
            if(!initialized)return;
            if(pipelineOverride!=null)QualitySettings.renderPipeline=previousPipeline;
            RenderSettings.fog=previousFog;RenderSettings.fogMode=previousFogMode;RenderSettings.fogColor=previousFogColor;
            RenderSettings.fogStartDistance=previousFogStart;RenderSettings.fogEndDistance=previousFogEnd;
        }
    }
}
