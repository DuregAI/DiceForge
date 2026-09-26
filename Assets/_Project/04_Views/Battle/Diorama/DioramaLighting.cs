using System;
using UnityEngine;

namespace Diceforge.View
{
    public sealed class DioramaLighting : ScriptableObject
    {
        [Serializable] public struct Binding { public string path; public int index; public Vector4 scaleOffset; }
        public Texture2D[] colors;
        public Texture2D[] directions;
        public Texture2D[] masks;
        public LightProbes probes;
        // Unity does not persist LightBakingOutput as a nested asset field.
        // Store its native JSON representation so mixed-light flags survive reloads.
        [SerializeField] private string sunOutputJson;
        public LightBakingOutput sunOutput
        {
            get => string.IsNullOrEmpty(sunOutputJson)
                ? new LightBakingOutput {isBaked=true, lightmapBakeType=LightmapBakeType.Mixed, mixedLightingMode=MixedLightingMode.Subtractive}
                : JsonUtility.FromJson<LightBakingOutput>(sunOutputJson);
            set => sunOutputJson=JsonUtility.ToJson(value);
        }
        public Binding[] bindings;
        public void Apply(GameObject composition)
        {
            var maps=new LightmapData[colors.Length];
            for(int i=0;i<maps.Length;i++)maps[i]=new LightmapData {lightmapColor=colors[i],lightmapDir=directions[i],shadowMask=masks[i]};
            LightmapSettings.lightmapsMode=LightmapsMode.NonDirectional;LightmapSettings.lightmaps=maps;LightmapSettings.lightProbes=probes;
            foreach(var binding in bindings)
            {
                var target=composition.transform.Find(binding.path);
                if(target==null)continue;
                var renderer=target.GetComponent<Renderer>();
                if(renderer!=null){renderer.lightmapIndex=binding.index;renderer.lightmapScaleOffset=binding.scaleOffset;}
            }
            foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))if(light.type==LightType.Directional)
            {
#if UNITY_EDITOR
                light.lightmapBakeType=LightmapBakeType.Mixed;
#endif
                light.bakingOutput=sunOutput;
            }
        }
    }
}
