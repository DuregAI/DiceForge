using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Diceforge.View
{
    public sealed class ForestWaterReflections : MonoBehaviour
    {
        [Serializable] public sealed class Plane
        {
            public Renderer surface;
            public bool useAuthoredHeight;
            public float height;
            [NonSerialized] internal RenderTexture texture;
        }
        public LivingForestProfile profile;
        public Plane[] planes = Array.Empty<Plane>();
        private Camera reflectionCamera;
        private Camera source;
        private MaterialPropertyBlock properties;
        private float nextFrame;
        private Vector3 lastPosition;
        private Quaternion lastRotation;
        private float lastSize;
        public int CaptureCount { get; private set; }
        private static readonly int Texture = Shader.PropertyToID("_PlanarReflectionTex");
        private static readonly int Ready = Shader.PropertyToID("_HasReflection");

        private void LateUpdate()
        {
            source=Camera.main;
            if(source==null || profile==null)return;
            bool cameraMoved=(source.transform.position-lastPosition).sqrMagnitude>.00001f ||
                Quaternion.Angle(source.transform.rotation,lastRotation)>.05f || Mathf.Abs(source.orthographicSize-lastSize)>.001f;
            if(!cameraMoved && Time.unscaledTime<nextFrame)return;
            Capture(source);
            nextFrame=Time.unscaledTime+1f/Mathf.Max(1,Application.isMobilePlatform?profile.reflectionFramesPerSecond/2:profile.reflectionFramesPerSecond);
        }

        public void Capture(Camera view)
        {
            if(view==null || profile==null || planes==null || planes.Length==0)return;
            source=view;
            if(properties==null)properties=new MaterialPropertyBlock();
            if(reflectionCamera==null)
            {
                var go=new GameObject("Forest water reflection camera"){hideFlags=HideFlags.HideAndDontSave};
                go.transform.SetParent(transform,false);
                reflectionCamera=go.AddComponent<Camera>();reflectionCamera.enabled=false;
            }
            int width=Mathf.Clamp(Application.isMobilePlatform?profile.mobileReflectionWidth:profile.reflectionWidth,128,1024);
            int height=Mathf.Max(64,Mathf.RoundToInt(width/Mathf.Max(.3f,view.aspect)));
            reflectionCamera.CopyFrom(view);
            reflectionCamera.enabled=false;
            reflectionCamera.cullingMask=view.cullingMask & ~((1<<4)|(1<<5)|(1<<30)|(1<<LivingForestForeground.ForegroundLayer));
            reflectionCamera.useOcclusionCulling=false;
            var data=reflectionCamera.GetUniversalAdditionalCameraData();
            data.SetRenderer(1);data.renderPostProcessing=false;data.renderShadows=false;
            data.requiresDepthTexture=false;data.requiresColorTexture=false;
            data.volumeLayerMask=0;
            bool oldInvert=GL.invertCulling;
            var previousActive=RenderTexture.active;
            try
            {
                foreach(var plane in planes)
                {
                    if(plane==null || plane.surface==null || !plane.surface.gameObject.activeInHierarchy)continue;
                    if(plane.texture==null || plane.texture.width!=width || plane.texture.height!=height)
                    {
                        Release(plane);
                        var format=SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)?RenderTextureFormat.ARGBHalf:RenderTextureFormat.ARGB32;
                        plane.texture=new RenderTexture(width,height,16,format)
                            {name="Forest reflection "+plane.surface.name,hideFlags=HideFlags.HideAndDontSave};
                        plane.texture.Create();
                    }
                    float y=plane.useAuthoredHeight?plane.height:plane.surface.bounds.center.y;
                    Matrix4x4 mirror=Matrix4x4.Scale(new Vector3(1,-1,1));mirror.m13=2*y;
                    Vector3 position=view.transform.position;position.y=2*y-position.y;
                    reflectionCamera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(
                        mirror.MultiplyVector(view.transform.forward),mirror.MultiplyVector(view.transform.up)));
                    reflectionCamera.worldToCameraMatrix=view.worldToCameraMatrix*mirror;
                    var matrix=reflectionCamera.worldToCameraMatrix;
                    Vector3 planePoint=matrix.MultiplyPoint(new Vector3(0,y+.025f,0));
                    Vector3 planeNormal=matrix.MultiplyVector(Vector3.up).normalized;
                    reflectionCamera.projectionMatrix=view.CalculateObliqueMatrix(new Vector4(
                        planeNormal.x,planeNormal.y,planeNormal.z,-Vector3.Dot(planePoint,planeNormal)));
                    reflectionCamera.targetTexture=plane.texture;
                    GL.invertCulling=!oldInvert;
                    var request=new UniversalRenderPipeline.SingleCameraRequest{destination=plane.texture};
                    RenderPipeline.SubmitRenderRequest(reflectionCamera,request);
                    plane.surface.GetPropertyBlock(properties);
                    properties.SetTexture(Texture,plane.texture);properties.SetFloat(Ready,1);
                    plane.surface.SetPropertyBlock(properties);
                    CaptureCount++;
                }
            }
            finally
            {
                GL.invertCulling=oldInvert;
                RenderTexture.active=previousActive;
                reflectionCamera.targetTexture=null;
            }
            lastPosition=view.transform.position;lastRotation=view.transform.rotation;lastSize=view.orthographicSize;
        }

        private static void Release(Plane plane)
        {
            if(plane.texture==null)return;
            plane.texture.Release();
            if(Application.isPlaying)Destroy(plane.texture);else DestroyImmediate(plane.texture);
            plane.texture=null;
        }
        private void OnDisable() => ReleaseResources();

        // Editor art reviews invoke Capture without entering Play Mode. They own
        // this cleanup explicitly because runtime lifecycle messages do not run there.
        public void ReleaseResources()
        {
            foreach(var plane in planes ?? Array.Empty<Plane>())
            {
                if(plane==null)continue;
                if(plane.surface!=null)
                {
                    var block=new MaterialPropertyBlock();plane.surface.GetPropertyBlock(block);
                    block.SetFloat(Ready,0);block.SetTexture(Texture,Texture2D.blackTexture);plane.surface.SetPropertyBlock(block);
                }
                Release(plane);
            }
            if(reflectionCamera!=null)
            {
                if(Application.isPlaying)Destroy(reflectionCamera.gameObject);else DestroyImmediate(reflectionCamera.gameObject);
                reflectionCamera=null;
            }
        }
    }
}
