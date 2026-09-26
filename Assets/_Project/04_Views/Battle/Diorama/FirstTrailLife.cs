using UnityEngine;

namespace Diceforge.View
{
    public sealed class FirstTrailLife : MonoBehaviour
    {
        public Transform pennant;
        public Renderer creek;
        private Quaternion clothRest;
        private MaterialPropertyBlock properties;
        private bool reducedMotion;
        private bool initialized;
        private void Start()
        {
            initialized=true;reducedMotion=PlayerPrefs.GetInt("WoodlandReducedMotion",0)==1;
            if(pennant!=null)clothRest=pennant.localRotation;
            properties=new MaterialPropertyBlock();
            if(creek!=null)
            {
                creek.GetPropertyBlock(properties);
                properties.SetFloat("_Still",reducedMotion?1:0);
                creek.SetPropertyBlock(properties);
            }
            foreach(var renderer in GetComponentsInChildren<Renderer>())
            {
                if(renderer.sharedMaterial==null || (renderer.sharedMaterial.shader.name!="Diceforge/First Trail Lake" && renderer.sharedMaterial.shader.name!="Diceforge/Stylized Water Surface" && renderer.sharedMaterial.shader.name!="Diceforge/Waterfall Splash"))continue;
                renderer.GetPropertyBlock(properties);
                properties.SetFloat("_Still",reducedMotion || DioramaBoard.ReducedMotion?1:0);
                renderer.SetPropertyBlock(properties);
            }
        }
        private void Update()
        {
            if(pennant==null || DioramaBoard.ReducedMotion || reducedMotion)return;
            pennant.localRotation=clothRest*Quaternion.Euler(0,Mathf.Sin(Time.time*1.25f)*4,Mathf.Sin(Time.time*.83f)*1.5f);
        }
        private void OnDisable(){if(initialized && pennant!=null)pennant.localRotation=clothRest;}
    }
}
