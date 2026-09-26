using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Diceforge.View.Editor
{
    public static class WoodlandLightingBaker
    {
        private const string Folder=WoodlandDioramaBuilder.Data+"/Lighting";
        private static Queue<(DioramaLayout layout,bool portrait)> queue;
        private static (DioramaLayout layout,bool portrait) current;
        private static GameObject composition;
        private static Light sun;
        private static string originalScene;
        private static RenderPipelineAsset originalPipeline;
        private static string Progress=>"docs/Art/WoodlandDiorama/Validation/lighting.txt";
        [MenuItem("Diceforge/Woodland/Bake both orientations")]
        public static void Start()
        {
            if(EditorApplication.isPlaying || Lightmapping.isRunning)throw new InvalidOperationException("Stop play mode and any active bake first.");
            if(UnityEngine.SceneManagement.SceneManager.sceneCount!=1)throw new InvalidOperationException("Bake from a single saved scene to preserve the editor setup.");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save or discard current scene changes before baking.");
            if(string.IsNullOrEmpty(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path))throw new InvalidOperationException("Save the current scene before baking.");
            Directory.CreateDirectory(Folder);Directory.CreateDirectory(Path.GetDirectoryName(Progress));
            originalScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;originalPipeline=QualitySettings.renderPipeline;
            QualitySettings.renderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(WoodlandDioramaBuilder.Data+"/DesktopPipeline.asset");
            queue=new Queue<(DioramaLayout,bool)>();
            foreach(var layout in AssetDatabase.FindAssets("t:DioramaLayout").Select(g=>AssetDatabase.LoadAssetAtPath<DioramaLayout>(AssetDatabase.GUIDToAssetPath(g))).GroupBy(l=>l.cellIds.Length).Select(g=>g.First()))
            {queue.Enqueue((layout,false));queue.Enqueue((layout,true));}
            File.WriteAllText(Progress,"Queued "+queue.Count+" lighting compositions\n");
            Lightmapping.bakeCompleted+=Completed;NextSafely();
        }
        private static void RestoreEditor()
        {
            Lightmapping.bakeCompleted-=Completed;
            EditorApplication.delayCall-=NextSafely;
            QualitySettings.renderPipeline=originalPipeline;
            if(!string.IsNullOrEmpty(originalScene))EditorSceneManager.OpenScene(originalScene);
        }
        private static void NextSafely()
        {
            try { Next(); }
            catch(Exception exception)
            {
                File.AppendAllText(Progress,"FAILED "+exception.Message+"\n");
                RestoreEditor();Debug.LogException(exception);
            }
        }
        private static string Key => "Cells"+current.layout.cellIds.Length+(current.portrait?"Portrait":"Landscape");
        private static void Next()
        {
            if(queue.Count==0)
            {
                RestoreEditor();
                File.AppendAllText(Progress,"COMPLETE\n");return;
            }
            current=queue.Dequeue();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(WoodlandDioramaBuilder.Data+"/"+current.layout.name+".prefab");
            var root=UnityEngine.Object.Instantiate(prefab);var board=root.GetComponent<DioramaBoard>();
            board.landscapeRoot.SetActive(!current.portrait);board.portraitRoot.SetActive(current.portrait);
            composition=current.portrait?board.portraitRoot:board.landscapeRoot;
            foreach(var renderer in composition.GetComponentsInChildren<MeshRenderer>())
                if(renderer.name!="Available move" && renderer.name!="Quiet pond" && renderer.enabled)
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,StaticEditorFlags.ContributeGI);
            sun=new GameObject("Warm sun").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(48,-35,0);
            sun.color=new Color(1,.89f,.69f);sun.intensity=1.65f;sun.shadows=LightShadows.Soft;sun.lightmapBakeType=LightmapBakeType.Mixed;
            var camera=new GameObject("Bake camera").AddComponent<Camera>();camera.transform.position=new Vector3(0,12,-10);camera.transform.LookAt(Vector3.zero);
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.60f,.68f,.60f);RenderSettings.ambientEquatorColor=new Color(.37f,.40f,.28f);RenderSettings.ambientGroundColor=new Color(.19f,.14f,.085f);
            var group=new GameObject("Character probes").AddComponent<LightProbeGroup>();
            var poses=current.portrait?current.layout.portrait:current.layout.landscape;
            group.probePositions=poses.SelectMany(p=>new[]{p+Vector3.up*.25f,p+Vector3.up*1.3f}).Concat(new[]{new Vector3(0,.5f,0),new Vector3(0,1.5f,0)}).ToArray();
            var settings=new LightingSettings {bakedGI=true,realtimeGI=false,lightmapResolution=12,lightmapMaxSize=1024,directSampleCount=16,indirectSampleCount=48,environmentSampleCount=32,maxBounces=2,ao=true,aoMaxDistance=.7f,aoExponentIndirect=1.1f,aoExponentDirect=.5f,directionalityMode=LightmapsMode.NonDirectional,mixedBakeMode=MixedLightingMode.Subtractive};
            settings.lightmapper=LightingSettings.Lightmapper.ProgressiveCPU;
            string settingsPath=Folder+"/"+Key+"Settings.lighting";
            var existing=AssetDatabase.LoadAssetAtPath<LightingSettings>(settingsPath);
            if(existing==null)AssetDatabase.CreateAsset(settings,settingsPath);else{EditorUtility.CopySerialized(settings,existing);UnityEngine.Object.DestroyImmediate(settings);settings=existing;}
            Lightmapping.lightingSettings=settings;
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Folder+"/"+Key+".unity");
            File.AppendAllText(Progress,"BAKING "+Key+"\n");
            if(!Lightmapping.BakeAsync())throw new InvalidOperationException("Lightmapper did not start for "+Key);
        }
        private static void Completed()
        {
            var maps=LightmapSettings.lightmaps;
            if(maps.Length==0){File.AppendAllText(Progress,"FAILED no lightmaps for "+Key+"\n");RestoreEditor();return;}
            string path=Folder+"/"+Key+".asset";
            var profile=AssetDatabase.LoadAssetAtPath<DioramaLighting>(path);
            if(profile==null){profile=ScriptableObject.CreateInstance<DioramaLighting>();AssetDatabase.CreateAsset(profile,path);}
            profile.colors=maps.Select(m=>m.lightmapColor).ToArray();profile.directions=maps.Select(m=>m.lightmapDir).ToArray();profile.masks=maps.Select(m=>m.shadowMask).ToArray();profile.probes=LightmapSettings.lightProbes;profile.sunOutput=sun.bakingOutput;
            profile.bindings=composition.GetComponentsInChildren<Renderer>().Where(r=>r.lightmapIndex>=0&&r.lightmapIndex<maps.Length).Select(r=>new DioramaLighting.Binding {path=AnimationUtility.CalculateTransformPath(r.transform,composition.transform),index=r.lightmapIndex,scaleOffset=r.lightmapScaleOffset}).ToArray();EditorUtility.SetDirty(profile);
            EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
            foreach(string guid in AssetDatabase.FindAssets("t:DioramaLayout"))
            {
                var layout=AssetDatabase.LoadAssetAtPath<DioramaLayout>(AssetDatabase.GUIDToAssetPath(guid));if(layout.cellIds.Length!=current.layout.cellIds.Length)continue;
                string prefabPath=WoodlandDioramaBuilder.Data+"/"+layout.name+".prefab";var root=PrefabUtility.LoadPrefabContents(prefabPath);var board=root.GetComponent<DioramaBoard>();
                if(current.portrait)board.portraitLighting=profile;else board.landscapeLighting=profile;
                PrefabUtility.SaveAsPrefabAsset(root,prefabPath);PrefabUtility.UnloadPrefabContents(root);
            }
            File.AppendAllText(Progress,"DONE "+Key+" maps="+maps.Length+" renderers="+profile.bindings.Length+"\n");
            EditorApplication.delayCall+=NextSafely;
        }
    }
}
