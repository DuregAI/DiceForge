using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Diceforge.MapSystem;

namespace Diceforge.View.Editor
{
    public static class WoodlandHeroBuilder
    {
        private const string Art="Assets/_Project/07_Art/WoodlandHero";
        private const string Data="Assets/_Project/05_Gameplay_Data/Battle/WoodlandHero";
        [MenuItem("Diceforge/Woodland/Build hero level")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Build outside Play Mode.");
            Directory.CreateDirectory(Data);AssetDatabase.Refresh();
            var importer=(ModelImporter)AssetImporter.GetAtPath(Art+"/WoodlandHero.fbx");
            importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            importer.importNormals=ModelImporterNormals.Import;importer.SaveAndReimport();
            var mats=new Dictionary<string,Material>();
            mats["HeroEarth"]=Surface("HeroEarth",new Color(.43f,.31f,.16f),0,.85f);
            mats["HeroMoss"]=Surface("HeroMoss",new Color(.37f,.46f,.14f),0,.9f);
            mats["HeroRock"]=Surface("HeroRock",new Color(.48f,.49f,.41f),0,.8f);
            mats["HeroStone"]=Surface("HeroStone",new Color(.89f,.84f,.68f),0,.75f);
            mats["HeroPine"]=Surface("HeroPine",new Color(.24f,.36f,.20f),0,.87f);
            mats["HeroTips"]=Surface("HeroTips",new Color(.32f,.42f,.24f),0,.85f);
            mats["HeroBark"]=Surface("HeroBark",new Color(.37f,.21f,.09f),1,.85f);
            mats["HeroWood"]=Surface("HeroWood",new Color(.66f,.41f,.20f),1,.7f);
            mats["HeroWater"]=Surface("HeroWater",new Color(.10f,.49f,.44f),2,.22f);
            mats["HeroFoam"]=Surface("HeroFoam",new Color(.72f,.90f,.82f),2,.42f);
            mats["HeroGold"]=Surface("HeroGold",new Color(.92f,.70f,.30f),0,.5f);
            var atlas=AssetDatabase.LoadAssetAtPath<Material>(WoodlandDioramaBuilder.Data+"/Atlas.mat");
            var root=new GameObject("Woodland Hero — fifteen stones");
            var board=root.AddComponent<DioramaBoard>();board.landscapeOnly=true;
            var stage=root.AddComponent<WoodlandHeroStage>();
            var landscape=new GameObject("Landscape");landscape.transform.SetParent(root.transform,false);
            var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/WoodlandHero.fbx"),landscape.transform);
            model.transform.localRotation=Quaternion.Euler(0,180,0)*model.transform.localRotation;
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m!=null&&mats.TryGetValue(m.name.Split('.')[0],out var replacement)?replacement:atlas).ToArray();
                renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            }
            var layout=Asset<DioramaLayout>("Hero15.asset");layout.cellIds=Enumerable.Range(0,15).ToArray();layout.landscape=new Vector3[15];
            var glow=AssetDatabase.LoadAssetAtPath<Material>(WoodlandDioramaBuilder.Data+"/Selection.mat");
            for(int i=0;i<15;i++)
            {
                var tile=model.GetComponentsInChildren<Transform>().Single(t=>t.name.Split('.')[0]=="Tile_"+i.ToString("00"));
                var renderer=tile.GetComponent<Renderer>();var center=renderer.bounds.center;center.y=renderer.bounds.max.y+.015f;
                layout.landscape[i]=root.transform.InverseTransformPoint(center);
                var cell=new GameObject("Cell_"+i);cell.transform.SetParent(landscape.transform,false);cell.transform.position=center;
                var collider=cell.AddComponent<BoxCollider>();collider.center=Vector3.down*.12f;collider.size=new Vector3(1.13f,.32f,1.02f);
                var marker=cell.AddComponent<DioramaCell>();marker.cellId=i;
                var highlight=GameObject.CreatePrimitive(PrimitiveType.Cylinder);highlight.name="Available move";highlight.transform.SetParent(cell.transform,false);highlight.transform.localScale=new Vector3(1.06f,.005f,1.06f);
                UnityEngine.Object.DestroyImmediate(highlight.GetComponent<Collider>());marker.highlight=highlight.GetComponent<Renderer>();marker.highlight.sharedMaterial=glow;marker.highlight.enabled=false;
            }
            layout.portrait=(Vector3[])layout.landscape.Clone();layout.landscapeSize=new Vector3(9.1f,2,6.6f);layout.portraitSize=layout.landscapeSize;EditorUtility.SetDirty(layout);
            board.layout=layout;board.landscapeRoot=landscape;
            board.portraitRoot=new GameObject("Portrait deferred");board.portraitRoot.transform.SetParent(root.transform,false);board.portraitRoot.SetActive(false);
            string pipelinePath=Data+"/HeroPipeline.asset";
            if(!File.Exists(pipelinePath))AssetDatabase.CopyAsset("Assets/Settings/PC_RPAsset.asset",pipelinePath);
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            pipeline.shadowDistance=60;pipeline.msaaSampleCount=4;pipeline.supportsHDR=true;EditorUtility.SetDirty(pipeline);
            board.desktopPipeline=pipeline;board.mobilePipeline=pipeline;
            var grade=Asset<VolumeProfile>("HeroGrade.asset");
            grade.components.RemoveAll(component=>component==null);
            if(!grade.TryGet<ColorAdjustments>(out var colors))colors=grade.Add<ColorAdjustments>();
            colors.postExposure.Override(.28f);colors.contrast.Override(9);colors.saturation.Override(5);
            if(!grade.TryGet<Tonemapping>(out var tone))tone=grade.Add<Tonemapping>();tone.mode.Override(TonemappingMode.ACES);
            if(!grade.TryGet<Bloom>(out var bloom))bloom=grade.Add<Bloom>();bloom.intensity.Override(.13f);bloom.threshold.Override(1.1f);
            if(!grade.TryGet<Vignette>(out var vignette))vignette=grade.Add<Vignette>();vignette.intensity.Override(.19f);vignette.smoothness.Override(.6f);
            if(!grade.TryGet<DepthOfField>(out var depth))depth=grade.Add<DepthOfField>();
            depth.mode.Override(DepthOfFieldMode.Gaussian);depth.gaussianStart.Override(30);depth.gaussianEnd.Override(39);depth.gaussianMaxRadius.Override(1.2f);
            var backdrop=new GameObject("Distant woodland");backdrop.transform.SetParent(root.transform,false);
            var distantMat=AssetDatabase.LoadAssetAtPath<Material>(Data+"/DistantLeaves.mat");
            if(distantMat==null){distantMat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(distantMat,Data+"/DistantLeaves.mat");}
            distantMat.shader=Shader.Find("Universal Render Pipeline/Unlit");distantMat.mainTexture=null;
            distantMat.color=new Color(.28f,.37f,.28f);EditorUtility.SetDirty(distantMat);
            for(int row=0;row<2;row++)for(int i=0;i<11;i++)
            {
                var pivot=new GameObject("Distant cedar");pivot.transform.SetParent(backdrop.transform,false);
                pivot.transform.localPosition=new Vector3((i-5)*2.0f+row*.6f,-3.6f,8.5f+row*3.5f);
                pivot.transform.localScale=Vector3.one*(1.65f+Mathf.Sin(i*2.3f+row)*.30f);
                var tree=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(WoodlandDioramaBuilder.Art+"/Pine_"+(i%3)+".fbx"),pivot.transform);
                foreach(var r in tree.GetComponentsInChildren<Renderer>()){r.sharedMaterials=r.sharedMaterials.Select(m=>distantMat).ToArray();r.shadowCastingMode=ShadowCastingMode.Off;}
            }
            foreach(var component in grade.components)
            {
                if(!AssetDatabase.Contains(component))AssetDatabase.AddObjectToAsset(component,grade);
                EditorUtility.SetDirty(component);
            }
            EditorUtility.SetDirty(grade);stage.grade=grade;stage.water=mats["HeroWater"];
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Data+"/WoodlandHero.prefab");UnityEngine.Object.DestroyImmediate(root);
            var theme=AssetDatabase.LoadAssetAtPath<MapTheme>(Data+"/HeroTheme.asset");
            if(theme==null){theme=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<MapTheme>(WoodlandDioramaBuilder.Data+"/Theme_Map_Level_05.asset"));AssetDatabase.CreateAsset(theme,Data+"/HeroTheme.asset");}
            theme.dioramaPrefab=prefab;theme.presentation=MapTheme.Presentation.Diorama;EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();Debug.Log("Woodland hero art prefab rebuilt. Campaign levels 01-06 continue to use FirstTrail8.");
        }
        private static T Asset<T>(string name) where T:ScriptableObject
        {
            var asset=AssetDatabase.LoadAssetAtPath<T>(Data+"/"+name);if(asset!=null)return asset;
            asset=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(asset,Data+"/"+name);return asset;
        }
        private static Material Surface(string name,Color color,int grain,float roughness)
        {
            string path=Data+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            string texturePath=Data+"/"+name+"_Color.png";
            const int size=256;var pixels=new Color[size*size];var normals=new Color[size*size];
            Func<float,float,float> height=(x,y)=>
            {
                float broad=Mathf.PerlinNoise(x*5+13,y*5+7),fine=Mathf.PerlinNoise(x*65,y*65);
                return grain==1?.5f+.18f*Mathf.Sin((x+Mathf.Sin(y*9)*.014f)*185)+fine*.11f:grain==2?Mathf.PerlinNoise(x*9+Mathf.Sin(y*12)*.18f,y*11):broad*.64f+fine*.36f;
            };
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=(float)x/size,v=(float)y/size,h=height(u,v);float shade=grain==1?.80f+h*.29f:.90f+h*.17f;
                pixels[y*size+x]=new Color(color.r*shade,color.g*shade,color.b*shade,1);
                var n=new Vector3((h-height(u+.0039f,v))*.8f,(h-height(u,v+.0039f))*.8f,1).normalized;
                normals[y*size+x]=new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f,1);
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(texturePath,texture.EncodeToPNG());
            texture.SetPixels(normals);texture.Apply();string normalPath=Data+"/"+name+"_Normal.png";File.WriteAllBytes(normalPath,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath);AssetDatabase.ImportAsset(normalPath);
            var normalImporter=(TextureImporter)AssetImporter.GetAtPath(normalPath);normalImporter.textureType=TextureImporterType.NormalMap;normalImporter.SaveAndReimport();
            material.color=Color.white;material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));material.EnableKeyword("_NORMALMAP");material.SetFloat("_BumpScale",grain==2?1.8f:.65f);material.SetFloat("_Smoothness",1-roughness);
            if(grain==2){material.SetFloat("_Cull",0);material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",color*.12f);}
            EditorUtility.SetDirty(material);return material;
        }
    }
}
