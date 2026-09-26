using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace Diceforge.View.Editor
{
    public static class FirstTrailCompositionBuilder
    {
        private const string Art="Assets/_Project/07_Art/FirstTrail/FirstTrail.fbx";
        private const string Hero="Assets/_Project/05_Gameplay_Data/Battle/WoodlandHero/";
        private const string Output="Assets/_Project/06_Scenes/Art/FirstTrailReference.unity";
        [MenuItem("Diceforge/Woodland/Build first trail composition")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before building.");
            for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
                if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save current scene edits before opening the composition.");
            AssetDatabase.Refresh();
            FirstTrailArtMaterials.Build();
            var importer=(ModelImporter)AssetImporter.GetAtPath(Art);
            importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;
            importer.importNormals=ModelImporterNormals.Import;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;importer.SaveAndReimport();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("First trail — solo journey");
            var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Art),root.transform);
            model.transform.localRotation=Quaternion.Euler(0,180,0)*model.transform.localRotation;
            var atlas=AssetDatabase.LoadAssetAtPath<Material>(WoodlandDioramaBuilder.Data+"/Atlas.mat");
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m==null?atlas:AssetDatabase.LoadAssetAtPath<Material>(FirstTrailArtMaterials.Folder+m.name.Split('.')[0]+".mat")??AssetDatabase.LoadAssetAtPath<Material>(Hero+m.name.Split('.')[0]+".mat")??atlas).ToArray();
                renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            }
            // Keep explicit cell anchors for the upcoming solo gameplay implementation.
            var anchors=new GameObject("Eight route anchors");anchors.transform.SetParent(root.transform,false);
            var routeCells=new Transform[8];
            Vector3 start=Vector3.zero;
            for(int i=0;i<8;i++)
            {
                var tile=model.GetComponentsInChildren<Transform>().Single(t=>t.name.Split('.')[0]=="TrailCell_"+i.ToString("00"));
                var bounds=tile.GetComponent<Renderer>().bounds;
                tile.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(FirstTrailArtMaterials.Folder+"TrialStone"+(i%2)+".mat");
                var anchor=new GameObject("Cell_"+i);anchor.transform.SetParent(anchors.transform,false);
                anchor.transform.position=new Vector3(bounds.center.x,bounds.max.y+.01f,bounds.center.z);
                routeCells[i]=anchor.transform;
                if(i==0)start=anchor.transform.position;
            }
            var friends=new Transform[2];
            for(int i=0;i<2;i++)
            {
                var goblin=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(WoodlandDioramaBuilder.Data+"/Goblin_Red.prefab"),root.transform);
                goblin.name="Friend "+(i+1);goblin.transform.position=start+new Vector3(i==0?-.235f:.235f,0,0);
                goblin.transform.rotation=Quaternion.Euler(0,180+(i==0?-12:12),0);goblin.transform.localScale=Vector3.one*.76f;
                var mover=goblin.GetComponent<BoardLayoutTokenMover>();if(mover!=null)mover.enabled=false;
                friends[i]=goblin.transform;
            }
            var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.nearClipPlane=.1f;camera.farClipPlane=80;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.gameObject.AddComponent<AudioListener>();camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            var sun=new GameObject("Warm sun").AddComponent<Light>();sun.type=LightType.Directional;sun.shadows=LightShadows.Soft;
            var stage=root.AddComponent<WoodlandHeroStage>();stage.cameraSize=3.65f;
            stage.pipelineOverride=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Hero+"HeroPipeline.asset");
            stage.grade=BuildReferenceGrade();stage.water=AssetDatabase.LoadAssetAtPath<Material>(Hero+"HeroWater.mat");
            stage.sunIntensity=2.15f;stage.fillIntensity=.48f;stage.perspectiveShowcase=true;
            var life=root.AddComponent<FirstTrailLife>();
            life.pennant=model.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name.StartsWith("Exit pennant"));
            life.creek=model.GetComponentsInChildren<Renderer>().FirstOrDefault(r=>r.name.StartsWith("Water creek flowing"));
            // Give the edit-time camera the same framing as the runtime art stage.
            camera.transform.rotation=Quaternion.Euler(47,0,0);camera.orthographicSize=stage.cameraSize;
            camera.orthographic=false;camera.fieldOfView=2*Mathf.Atan(stage.cameraSize/25)*Mathf.Rad2Deg;
            camera.transform.position=new Vector3(0,.2f,0)-camera.transform.up*.25f-camera.transform.forward*25;
            var document=new GameObject("Composition captions").AddComponent<UIDocument>();
            document.gameObject.name="First trail HUD";
            document.panelSettings=Resources.Load<PanelSettings>("WoodlandPanel");document.visualTreeAsset=Resources.Load<VisualTreeAsset>("FirstTrailPlayHud");
            var play=root.AddComponent<FirstTrailPlayController>();play.Configure(camera,document,routeCells,friends);
            Directory.CreateDirectory(Path.GetDirectoryName(Output));EditorSceneManager.SaveScene(scene,Output);AssetDatabase.SaveAssets();
            Selection.activeGameObject=root;Debug.Log("First trail reference saved: two friends, eight spaces, solo journey ready.");
        }
        private static VolumeProfile BuildReferenceGrade()
        {
            const string path="Assets/_Project/07_Art/FirstTrail/ReferenceGrade.asset";
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if(profile==null)
            {
                profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,path);
                foreach(var source in AssetDatabase.LoadAssetAtPath<VolumeProfile>(Hero+"HeroGrade.asset").components)
                {
                    if(source==null)continue;
                    var copy=UnityEngine.Object.Instantiate(source);copy.name=source.name;
                    profile.components.Add(copy);AssetDatabase.AddObjectToAsset(copy,profile);
                }
            }
            if(!profile.TryGet<DepthOfField>(out var focus))
            {focus=profile.Add<DepthOfField>();AssetDatabase.AddObjectToAsset(focus,profile);}
            focus.active=true;focus.mode.Override(DepthOfFieldMode.Bokeh);
            focus.focusDistance.Override(25f);focus.focalLength.Override(300f);focus.aperture.Override(1f);
            EditorUtility.SetDirty(focus);EditorUtility.SetDirty(profile);
            return profile;
        }
    }
}
