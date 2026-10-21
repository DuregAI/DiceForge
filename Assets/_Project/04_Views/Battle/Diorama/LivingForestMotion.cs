using UnityEngine;

namespace Diceforge.View
{
    public sealed class LivingForestMotion : MonoBehaviour
    {
        public LivingForestProfile profile;
        public Transform lantern;
        public Light lanternLight;
        public Renderer impactSpray;
        private Quaternion lanternRest;
        private float clock;
        private bool initialized;
        private static readonly int Wind = Shader.PropertyToID("_ForestWindStrength");
        private static readonly int Motion = Shader.PropertyToID("_ForestMotion");
        private static readonly int Clock = Shader.PropertyToID("_ForestTime");
        public float AnimationTime => clock;
        public bool IsStill => DioramaBoard.ReducedMotion || PlayerPrefs.GetInt("WoodlandReducedMotion",0)==1;

        private void OnEnable()
        {
            if(lantern!=null)lanternRest=lantern.localRotation;
            // Runtime local bounds are not serialized into a prefab. Include the
            // shader-driven flight in local units after the FBX axis/scale conversion.
            if(impactSpray!=null)
            {
                impactSpray.ResetLocalBounds();
                var bounds=impactSpray.localBounds;
                Vector3 size=impactSpray.transform.InverseTransformVector(Vector3.one);
                bounds.Expand(new Vector3(Mathf.Abs(size.x),Mathf.Abs(size.y),Mathf.Abs(size.z)));
                impactSpray.localBounds=bounds;
            }
            initialized=true;
            Apply(0f);
        }
        private void Update() => Apply(Time.deltaTime);
        public void Apply(float deltaTime)
        {
            if(!initialized)
            {
                if(lantern!=null)lanternRest=lantern.localRotation;
                initialized=true;
            }
            bool still=IsStill;
            if(!still)clock+=Mathf.Max(0,deltaTime);
            Shader.SetGlobalFloat(Clock,clock);
            Shader.SetGlobalFloat(Wind,profile!=null?profile.windStrength:1f);
            Shader.SetGlobalFloat(Motion,still?0:1);
            if(lantern!=null)
                lantern.localRotation=lanternRest*(still?Quaternion.identity:Quaternion.Euler(
                    Mathf.Sin(clock*1.13f)*2.3f,0,Mathf.Sin(clock*.83f+.4f)*1.7f));
            if(lanternLight!=null && profile!=null)
            {
                lanternLight.color=profile.lanternColor;
                lanternLight.intensity=profile.lanternIntensity*(still?1f:1f+Mathf.Sin(clock*2.4f)*.025f);
            }
        }
        private void OnDisable()
        {
            if(initialized && lantern!=null)lantern.localRotation=lanternRest;
            if(impactSpray!=null)impactSpray.ResetLocalBounds();
            Shader.SetGlobalFloat(Motion,0);
        }
    }
}
