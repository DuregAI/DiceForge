#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Diceforge.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Diceforge.Editor
{
    [InitializeOnLoad, DefaultExecutionOrder(10000)]
    public sealed class CastPolishMovie : MonoBehaviour
    {
        private const string Pending = "Diceforge.CastPolishMovie.Pending";
        private const string Output = "C:/Backforge/Tripo3D/work/cast_v05/movie-frames";
        private Camera camera;
        private RenderTexture target;
        private Texture2D frameImage;
        private readonly List<GameObject> models = new List<GameObject>();
        private readonly List<SkinnedMeshRenderer> skins = new List<SkinnedMeshRenderer>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<MeshRenderer> displays = new List<MeshRenderer>();
        private int frame, oldRate;
        private float oldScale;
        private bool configured, cleared;
        private bool oldBackground, oldFog;

        static CastPolishMovie()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
                SessionState.SetBool(Pending, false);
                var root = new GameObject("Temporary cast movie") { hideFlags = HideFlags.HideAndDontSave };
                root.AddComponent<CastPolishMovie>();
            };
        }

        public static void Record()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Finish the current play session before recording.");
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        private IEnumerator Start()
        {
            yield return null; yield return null;
            oldRate = Time.captureFramerate; oldScale = Time.timeScale;
            oldBackground = Application.runInBackground; Application.runInBackground = true;
            oldFog = RenderSettings.fog; RenderSettings.fog = false;
            Time.captureFramerate = 24; Time.timeScale = 1;
            Directory.CreateDirectory(Output);
            transform.position = new Vector3(0,0,10000);
            target = new RenderTexture(1500,600,24,RenderTextureFormat.ARGB32); target.Create();
            frameImage = new Texture2D(1500,600,TextureFormat.RGBA32,false);
            camera = new GameObject("Cast movie camera").AddComponent<Camera>(); camera.transform.SetParent(transform,false);
            camera.enabled = false; camera.transform.localPosition = new Vector3(0,.72f,5);
            camera.transform.localRotation = Quaternion.Euler(0,180,0);
            camera.orthographic = true; camera.orthographicSize = .98f; camera.cullingMask = 1<<30;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.16f,.20f,.18f);
            camera.targetTexture = target; CharacterAppearance.ConfigurePortraitCamera(camera);
            CharacterAppearance.CreatePortraitLights(transform);
            string[] names = { "Tish", "Luma", "Bum", "Scratchy" };
            // Camera faces -Z, so descending X places the cast in reading order.
            float[] positions = { 1.65f,.60f,-.55f,-1.70f };
            for (int i=0;i<names.Length;i++)
            {
                var model = Instantiate(Resources.Load<GameObject>("Characters/"+names[i]+"Dialogue"),transform);
                model.transform.localPosition = Vector3.right*positions[i];
                foreach(var item in model.GetComponentsInChildren<Transform>()) item.gameObject.layer=30;
                model.GetComponent<CharacterAppearance>().Apply(true);
                model.GetComponentInChildren<Animator>().updateMode=AnimatorUpdateMode.Normal;
                var performance=model.GetComponent<CharacterDialoguePerformance>();
                performance.BeginLine(false);
                model.GetComponentInChildren<Animator>().SetInteger("DialogueMode",0);
                var skin=model.GetComponentInChildren<SkinnedMeshRenderer>();
                var mesh=new Mesh();var itemObject=new GameObject("CPU movie pose");itemObject.layer=30;itemObject.transform.SetParent(skin.transform,false);
                itemObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                var display=itemObject.AddComponent<MeshRenderer>();display.sharedMaterials=skin.sharedMaterials;
                display.renderingLayerMask=CharacterAppearance.PortraitRenderingLayer;
                display.lightProbeUsage=LightProbeUsage.CustomProvided;display.reflectionProbeUsage=ReflectionProbeUsage.Off;
                var properties=new MaterialPropertyBlock();skin.GetPropertyBlock(properties);display.SetPropertyBlock(properties);
                skin.enabled=false;
                models.Add(model);skins.Add(skin);meshes.Add(mesh);displays.Add(display);
            }
            configured=true;
        }

        private void LateUpdate()
        {
            if (!configured) return;
            if(frame==24 || frame==168)
                foreach(var model in models) model.GetComponent<CharacterDialoguePerformance>().BeginLine(true);
            if(frame==72)foreach(var model in models)model.GetComponent<CharacterDialoguePerformance>().PlayGesture("Greet");
            if(frame==144 || frame==216)
                foreach(var model in models)model.GetComponent<CharacterDialoguePerformance>().BeginLine(false);
            if(frame==168)foreach(var model in models)model.GetComponent<CharacterDialoguePerformance>().PlayGesture("Doubt");
            for(int i=0;i<skins.Count;i++)
            {
                skins[i].BakeMesh(meshes[i],true);meshes[i].RecalculateBounds();displays[i].localBounds=meshes[i].bounds;
            }
            var previous=RenderTexture.active;
            camera.Render();RenderTexture.active=target;
            frameImage.ReadPixels(new Rect(0,0,1500,600),0,0);frameImage.Apply();
            File.WriteAllBytes(Output+"/"+frame.ToString("0000")+".png",frameImage.EncodeToPNG());
            RenderTexture.active=previous;
            EditorApplication.QueuePlayerLoopUpdate();
            if(++frame<264)return;
            File.WriteAllText(Output+"/complete.json","{\"frames\":264,\"fps\":24,\"seconds\":11,\"source\":\"Unity runtime: character profiles, facial motion and dialogue transitions\"}");
            Cleanup();
            Destroy(gameObject);
            EditorApplication.delayCall += ()=>EditorApplication.isPlaying=false;
        }

        private void Cleanup()
        {
            if(cleared)return;cleared=true;configured=false;
            Time.captureFramerate=oldRate;Time.timeScale=oldScale;
            Application.runInBackground=oldBackground;RenderSettings.fog=oldFog;
            if(camera!=null)camera.targetTexture=null;
            if(target!=null){target.Release();Destroy(target);}
            if(frameImage!=null)Destroy(frameImage);
            foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);
        }
        private void OnDestroy(){if(target!=null && !cleared)Cleanup();}
    }
}
#endif

