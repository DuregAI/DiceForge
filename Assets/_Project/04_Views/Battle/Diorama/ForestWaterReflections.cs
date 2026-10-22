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
        private float lastAspect;
        private Matrix4x4 lastProjection;
        private Camera lastCamera;
        private bool hasCapturedView;
        private readonly UnityEngine.Plane[] viewFrustum = new UnityEngine.Plane[6];
        public int CaptureCount { get; private set; }
        private static readonly int Texture = Shader.PropertyToID("_PlanarReflectionTex");
        private static readonly int Ready = Shader.PropertyToID("_HasReflection");
        private static readonly int MaxMip = Shader.PropertyToID("_PlanarReflectionMaxMip");

        private void LateUpdate()
        {
            source=Camera.main;
            if(source==null || profile==null)return;
            bool cameraMoved=ViewChanged(source);
            if(!cameraMoved && Time.unscaledTime<nextFrame)return;
            Capture(source);
            nextFrame=Time.unscaledTime+1f/Mathf.Max(1,Application.isMobilePlatform?profile.reflectionFramesPerSecond/2:profile.reflectionFramesPerSecond);
        }

        private bool ViewChanged(Camera view)
        {
            return !hasCapturedView || lastCamera != view ||
                (view.transform.position-lastPosition).sqrMagnitude>.00001f ||
                Quaternion.Angle(view.transform.rotation,lastRotation)>.05f ||
                Mathf.Abs(view.orthographicSize-lastSize)>.001f ||
                Mathf.Abs(view.aspect-lastAspect)>.0001f || lastProjection != view.projectionMatrix;
        }

        private bool Visible(Plane plane, Camera view)
        {
            if(plane==null || plane.surface==null)return false;
            var surface=plane.surface;
            return surface.enabled && !surface.forceRenderingOff && surface.gameObject.activeInHierarchy &&
                (view.cullingMask & (1<<surface.gameObject.layer))!=0 &&
                GeometryUtility.TestPlanesAABB(viewFrustum,surface.bounds);
        }

        public void Capture(Camera view)
        {
            if(view==null || profile==null || planes==null || planes.Length==0)return;
            source=view;
            GeometryUtility.CalculateFrustumPlanes(view,viewFrustum);
            bool anyVisible=false;
            foreach(var plane in planes)if(Visible(plane,view)){anyVisible=true;break;}
            if(!anyVisible)
            {
                RememberView(view);
                return;
            }
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
                    if(!Visible(plane,view))continue;
                    if(plane.texture==null || !plane.texture.IsCreated() || plane.texture.width!=width || plane.texture.height!=height)
                    {
                        Release(plane);
                        var format=SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)?RenderTextureFormat.ARGBHalf:RenderTextureFormat.ARGB32;
                        plane.texture=new RenderTexture(width,height,16,format)
                            {name="Forest reflection "+plane.surface.name,hideFlags=HideFlags.HideAndDontSave,
                            useMipMap=true,autoGenerateMips=false,filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Clamp};
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
                    // One chain per completed capture, not every main-camera frame.
                    // The water shader uses low mip levels for broad soft reflections.
                    plane.texture.GenerateMips();
                    plane.surface.GetPropertyBlock(properties);
                    properties.SetTexture(Texture,plane.texture);properties.SetFloat(Ready,1);
                    properties.SetFloat(MaxMip,Mathf.Max(0,plane.texture.mipmapCount-1));
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
            RememberView(view);
        }

        private void RememberView(Camera view)
        {
            lastPosition=view.transform.position;lastRotation=view.transform.rotation;lastSize=view.orthographicSize;
            lastAspect=view.aspect;lastProjection=view.projectionMatrix;lastCamera=view;hasCapturedView=true;
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
                    block.SetFloat(Ready,0);block.SetFloat(MaxMip,0);
                    block.SetTexture(Texture,Texture2D.blackTexture);plane.surface.SetPropertyBlock(block);
                }
                Release(plane);
            }
            if(reflectionCamera!=null)
            {
                if(Application.isPlaying)Destroy(reflectionCamera.gameObject);else DestroyImmediate(reflectionCamera.gameObject);
                reflectionCamera=null;
            }
            hasCapturedView=false;lastCamera=null;nextFrame=0;
        }
    }
}
