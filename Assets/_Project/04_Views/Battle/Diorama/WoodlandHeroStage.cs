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
        public Vector3 sunEuler = new Vector3(48, -38, 0);
        public Vector3 fillEuler = new Vector3(35, 140, 0);
        public Color sunColor = new Color(1, .91f, .76f);
        public Color fillColor = new Color(.60f, .77f, 1);
        [Range(0, 1)] public float sunShadowStrength = 1f;
        [Min(0)] public float sunShadowBias = .035f;
        [Min(0)] public float sunShadowNormalBias = .15f;
        public Color ambientSkyColor = new Color(.57f, .66f, .74f);
        public Color ambientEquatorColor = new Color(.43f, .47f, .34f);
        public Color ambientGroundColor = new Color(.23f, .18f, .12f);
        public Color ambientProbeColor = new Color(.36f, .41f, .43f);
        public bool fogEnabled = true;
        public Color fogColor = new Color(.30f, .38f, .29f);
        public float fogStartDistance = 27f;
        public float fogEndDistance = 43f;
        public bool perspectiveShowcase;
        public UniversalRenderPipelineAsset pipelineOverride;
        private RenderPipelineAsset previousPipeline;
        private bool ownsPipeline;
        private bool initialized;
        private Material waterInstance;
        private Camera view;
        private DioramaBoard gameplayBoard;
        private Volume stageVolume;
        private bool gameplayGradePrepared;
        private int width, height;
        private DioramaLightingState previousLighting;
        private LightmapData[] previousLightmaps;
        private GameObject fillObject;
        private void Start()
        {
            initialized=true;
            previousPipeline=QualitySettings.renderPipeline;
            gameplayBoard=GetComponent<DioramaBoard>();
            // The board owns the gameplay pipeline and restores the menu's one.
            // Capturing its override here creates a second, conflicting owner
            // whose OnDestroy can put the forest pipeline back after the board.
            ownsPipeline=gameplayBoard==null && pipelineOverride!=null;
            if(ownsPipeline)QualitySettings.renderPipeline=pipelineOverride;
            if (gameplayBoard != null) gameplayBoard.CaptureLightingState();
            else
            {
                previousLighting = new DioramaLightingState();
                previousLightmaps = LightmapSettings.lightmaps;
                LightmapSettings.lightmaps = System.Array.Empty<LightmapData>();
            }
            RenderSettings.fog=fogEnabled;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=fogColor;
            RenderSettings.fogStartDistance=fogStartDistance;RenderSettings.fogEndDistance=fogEndDistance;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=ambientSkyColor;
            RenderSettings.ambientEquatorColor=ambientEquatorColor;
            RenderSettings.ambientGroundColor=ambientGroundColor;
            var ambient=new SphericalHarmonicsL2();ambient.AddAmbientLight(ambientProbeColor);RenderSettings.ambientProbe=ambient;
            foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if(light.type==LightType.Directional && light.gameObject.layer!=30)
                {
                    light.transform.rotation=Quaternion.Euler(sunEuler);
                    light.color=sunColor;light.intensity=sunIntensity;
#if UNITY_EDITOR
                    light.lightmapBakeType=LightmapBakeType.Realtime;
#endif
                    light.bakingOutput=default;
                    light.shadows=LightShadows.Soft;light.shadowBias=sunShadowBias;light.shadowNormalBias=sunShadowNormalBias;
                    light.shadowStrength=sunShadowStrength;
                }
            var fill=new GameObject("Cool woodland fill");fill.transform.SetParent(transform,false);
            fillObject=fill;
            fill.transform.rotation=Quaternion.Euler(fillEuler);var bounce=fill.AddComponent<Light>();
            bounce.type=LightType.Directional;bounce.color=fillColor;bounce.intensity=fillIntensity;bounce.shadows=LightShadows.None;
            stageVolume=gameObject.AddComponent<Volume>();stageVolume.isGlobal=true;stageVolume.priority=30;stageVolume.sharedProfile=grade;
            if(gameplayBoard==null)gameplayBoard=FindAnyObjectByType<DioramaBoard>();
            view=Camera.main;
            if(view!=null){view.GetUniversalAdditionalCameraData().renderPostProcessing=true;view.backgroundColor=fogColor;}
            if(water!=null)
            {
                waterInstance=new Material(water);
                foreach(var renderer in GetComponentsInChildren<Renderer>())if(renderer.sharedMaterial==water)renderer.sharedMaterial=waterInstance;
            }
            Frame();
        }
        private void LateUpdate()
        {
            if(width!=Screen.width || height!=Screen.height || (!gameplayGradePrepared && gameplayBoard != null && gameplayBoard.GetComponent<DioramaCameraController>()?.HasHomePose == true))Frame();
            if(waterInstance!=null && !DioramaBoard.ReducedMotion)
                waterInstance.SetTextureOffset("_BumpMap",new Vector2(Time.time*.019f,Time.time*.011f));
        }
        private void Frame()
        {
            width=Screen.width;height=Screen.height;
            if(view==null)return;
            var gameplayCamera = FindAnyObjectByType<DioramaCameraController>();
            if(gameplayCamera != null && gameplayCamera.HasHomePose)
            {
                if (!gameplayGradePrepared && stageVolume != null && stageVolume.sharedProfile != null)
                {
                    if (stageVolume.profile.TryGet<DepthOfField>(out var depth)) depth.mode.Override(DepthOfFieldMode.Off);
                    gameplayGradePrepared = true;
                }
                return;
            }
            view.transform.rotation=Quaternion.Euler(47,0,0);
            view.orthographicSize=Mathf.Max(cameraSize,cameraSize*1.19f/Mathf.Max(.3f,view.aspect));
            view.orthographic=!perspectiveShowcase;
            if(perspectiveShowcase)view.fieldOfView=2*Mathf.Atan(view.orthographicSize/25)*Mathf.Rad2Deg;
            view.transform.position=new Vector3(0,.2f,0)-view.transform.up*.25f-view.transform.forward*25;
        }
        private void OnDestroy()
        {
            if(waterInstance!=null)Destroy(waterInstance);
            if(stageVolume != null && stageVolume.HasInstantiatedProfile())
            {
                var instance = stageVolume.profile;
                foreach (var component in instance.components) if (component != null) Destroy(component);
                Destroy(instance);
            }
            if(!initialized)return;
            if(fillObject!=null){fillObject.SetActive(false);Destroy(fillObject);}
            if(stageVolume!=null){stageVolume.enabled=false;Destroy(stageVolume);}
            if(ownsPipeline)QualitySettings.renderPipeline=previousPipeline;
            previousLighting?.Restore();
            if(previousLightmaps!=null)LightmapSettings.lightmaps=previousLightmaps;
        }
    }
}
