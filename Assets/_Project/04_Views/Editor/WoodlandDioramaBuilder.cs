using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Diceforge.Map;
using Diceforge.MapSystem;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace Diceforge.View.Editor
{
    public static class WoodlandDioramaBuilder
    {
        public const string Art="Assets/_Project/07_Art/WoodlandDiorama";
        public const string Data="Assets/_Project/05_Gameplay_Data/Battle/Diorama";
        private static Material atlas,soil,moss,water,glow;
        private static UniversalRenderPipelineAsset desktop,mobile;
        [MenuItem("Diceforge/Woodland/Build assets and layouts")]
        public static void Build()
        {
            Directory.CreateDirectory(Data); AssetDatabase.Refresh();
            foreach(string file in Directory.GetFiles(Art,"*.fbx"))
            {
                if(file.Contains("Goblin"))continue;
                var model=(ModelImporter)AssetImporter.GetAtPath(file.Replace('\\','/'));
                if(!model.generateSecondaryUV){model.generateSecondaryUV=true;model.SaveAndReimport();}
            }
            atlas=Material("Atlas",Color.white);atlas.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/WoodlandPalette.png");
            soil=Material("Earth",new Color(.30f,.18f,.085f));moss=Material("Meadow",new Color(.38f,.41f,.15f));
            water=Material("Water",new Color(.12f,.43f,.40f));water.SetFloat("_Smoothness",.68f);
            glow=Material("Selection",new Color(1,.75f,.22f));glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",new Color(.65f,.33f,.04f));
            desktop=Pipeline("Desktop","Assets/Settings/PC_RPAsset.asset");mobile=Pipeline("Mobile","Assets/Settings/Mobile_RPAsset.asset");
            var textureImporter=(TextureImporter)AssetImporter.GetAtPath(Art+"/WoodlandPalette.png");
            textureImporter.mipmapEnabled=true;textureImporter.textureCompression=TextureImporterCompression.Compressed;textureImporter.SaveAndReimport();
            GameObject red=BuildGoblin("Red"),blue=BuildGoblin("Blue");
            foreach(string guid in AssetDatabase.FindAssets("t:BoardLayout",new[]{"Assets/_Project/05_Gameplay_Data/Battle"}))
            {
                var logical=AssetDatabase.LoadAssetAtPath<BoardLayout>(AssetDatabase.GUIDToAssetPath(guid));
                if(logical.cells==null || logical.cells.Count==0)continue;
                BuildBoard(logical);
            }
            foreach(string guid in AssetDatabase.FindAssets("t:BattleMapConfig"))
            {
                var map=AssetDatabase.LoadAssetAtPath<BattleMapConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if(IsEarlyCampaignMap(map))continue;
                if(map.boardLayout==null || map.mapTheme==null)continue;
                string path=Data+"/Theme_"+map.name+".asset";
                var theme=AssetDatabase.LoadAssetAtPath<MapTheme>(path);
                if(theme==null){theme=UnityEngine.Object.Instantiate(map.mapTheme);AssetDatabase.CreateAsset(theme,path);}
                theme.presentation=MapTheme.Presentation.Diorama;
                theme.dioramaPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Data+"/"+map.boardLayout.name+".prefab");
                theme.unitPrefab=red;theme.teamBUnitPrefab=blue;EditorUtility.SetDirty(theme);
            }
            string panelPath="Assets/_Project/Resources/WoodlandPanel.asset";
            var panel=AssetDatabase.LoadAssetAtPath<PanelSettings>(panelPath);
            if(panel==null)
            {
                panel=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Project/03_UI/MainMenu/MainMenuPanelSettings.asset"));
                AssetDatabase.CreateAsset(panel,panelPath);
            }
            panel.scaleMode=PanelScaleMode.ScaleWithScreenSize;panel.referenceResolution=new Vector2Int(1280,720);
            panel.screenMatchMode=PanelScreenMatchMode.MatchWidthOrHeight;panel.match=1;EditorUtility.SetDirty(panel);
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Debug.Log("Woodland diorama assets built; use Enable for current maps after validation.");
        }
        [MenuItem("Diceforge/Woodland/Enable for current maps")]
        public static void Enable()
        {
            foreach(string guid in AssetDatabase.FindAssets("t:BattleMapConfig"))
            {
                var map=AssetDatabase.LoadAssetAtPath<BattleMapConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if(IsEarlyCampaignMap(map))continue;
                var theme=AssetDatabase.LoadAssetAtPath<MapTheme>(Data+"/Theme_"+map.name+".asset");
                if(theme==null || theme.dioramaPrefab==null)throw new InvalidOperationException("Diorama missing for "+map.name);
                map.mapTheme=theme;EditorUtility.SetDirty(map);
            }
            AssetDatabase.SaveAssets();
        }
        private static bool IsEarlyCampaignMap(BattleMapConfig map)
        {
            if(map==null || !map.name.StartsWith("Map_Level_",StringComparison.Ordinal))return false;
            return int.TryParse(map.name.Substring("Map_Level_".Length),out int level) && level>=1 && level<=6;
        }
        private static Material Material(string name,Color color)
        {
            string path=Data+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.color=color;m.SetFloat("_Smoothness",.12f);EditorUtility.SetDirty(m);return m;
        }
        private static UniversalRenderPipelineAsset Pipeline(string name,string source)
        {
            string path=Data+"/"+name+"Pipeline.asset";
            if(!File.Exists(path))AssetDatabase.CopyAsset(source,path);
            var p=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            p.msaaSampleCount=4;p.shadowDistance=45;p.renderScale=1;
            EditorUtility.SetDirty(p);return p;
        }
        private static GameObject BuildGoblin(string team)
        {
            string path=Art+"/Goblin_"+team+".fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.animationType=ModelImporterAnimationType.Generic;
            importer.importAnimation=true;importer.materialImportMode=ModelImporterMaterialImportMode.None;
            var animations=importer.defaultClipAnimations;
            foreach(var clip in animations)clip.loopTime=clip.name.Contains("Idle")||clip.name.Contains("Walk")||clip.name.Contains("Victory");
            importer.clipAnimations=animations;importer.SaveAndReimport();
            string controllerPath=Data+"/Goblin_"+team+".controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if(controller==null)
            {
                controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var clips=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
                foreach(string name in new[]{"Idle","IdleReact","Selected","Anticipate","Walk","Land","Hit","Return","Victory"})
                {
                    var clip=clips.FirstOrDefault(c=>c.name.EndsWith("|"+name) || c.name==name || c.name.EndsWith("|"+name+".001"));
                    if(clip==null)clip=clips.FirstOrDefault(c=>c.name.Contains(name));
                    var state=controller.layers[0].stateMachine.AddState(name);state.motion=clip;
                    if(name=="Idle")controller.layers[0].stateMachine.defaultState=state;
                }
            }
            var root=new GameObject("Goblin_"+team);
            var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),root.transform);
            model.name="Model";
            foreach(var importedLod in model.GetComponentsInChildren<LODGroup>())UnityEngine.Object.DestroyImmediate(importedLod);
            foreach(var r in model.GetComponentsInChildren<Renderer>())r.sharedMaterials=Enumerable.Repeat(atlas,r.sharedMaterials.Length).ToArray();
            var animator=model.GetComponent<Animator>();
            if(animator==null)animator=model.AddComponent<Animator>();
            animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
            var renderers=model.GetComponentsInChildren<SkinnedMeshRenderer>();
            if(renderers.Length>=2)
            {
                var lod=root.AddComponent<LODGroup>();
                var high=renderers.Where(r=>r.name.Contains("LOD0")).Cast<Renderer>().ToArray();
                var low=renderers.Where(r=>r.name.Contains("LOD1")).Cast<Renderer>().ToArray();
                lod.SetLODs(new[]{new LOD(.12f,high),new LOD(.015f,low)});lod.RecalculateBounds();
            }
            root.AddComponent<BoardLayoutTokenMover>();
            root.AddComponent<GoblinLife>();
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Data+"/Goblin_"+team+".prefab");UnityEngine.Object.DestroyImmediate(root);return prefab;
        }
        private static void BuildBoard(BoardLayout logical)
        {
            int n=logical.cells.Count;
            string path=Data+"/"+logical.name+".asset";
            var layout=AssetDatabase.LoadAssetAtPath<DioramaLayout>(path);
            if(layout==null){layout=ScriptableObject.CreateInstance<DioramaLayout>();AssetDatabase.CreateAsset(layout,path);}
            layout.cellIds=logical.cells.Select(c=>c.cellId).ToArray();
            float x=Mathf.Max(2.35f,n*.19f),z=Mathf.Max(1.7f,n*.12f);
            float px=Mathf.Max(1.65f,n*.085f),pz=Mathf.Max(2.4f,n*.21f);
            layout.landscape=Positions(n,x,z);layout.portrait=Positions(n,px,pz);
            layout.landscapeSize=new Vector3((x+1.0f)*2,1,(z+1.0f)*2);
            layout.portraitSize=new Vector3((px+1.0f)*2,1,(pz+1.0f)*2);EditorUtility.SetDirty(layout);
            var root=new GameObject(logical.name+"_Diorama");var board=root.AddComponent<DioramaBoard>();board.layout=layout;
            board.landscapeLighting=AssetDatabase.LoadAssetAtPath<DioramaLighting>(Data+"/Lighting/Cells"+n+"Landscape.asset");
            board.portraitLighting=AssetDatabase.LoadAssetAtPath<DioramaLighting>(Data+"/Lighting/Cells"+n+"Portrait.asset");
            board.desktopPipeline=desktop;board.mobilePipeline=mobile;
            board.landscapeRoot=Composition(root.transform,layout.landscape,layout.cellIds,x,z,"Landscape",logical.name);
            board.portraitRoot=Composition(root.transform,layout.portrait,layout.cellIds,px,pz,"Portrait",logical.name);
            board.portraitRoot.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root,Data+"/"+logical.name+".prefab");UnityEngine.Object.DestroyImmediate(root);
        }
        private static Vector3[] Positions(int count,float x,float z)
        {
            var samples=new Vector3[721];var cumulative=new float[721];
            for(int i=0;i<=720;i++)
            {
                float a=(float)i/720*Mathf.PI*2;
                samples[i]=new Vector3(-Mathf.Sin(a)*x,.23f,-Mathf.Cos(a)*z);
                if(i>0)cumulative[i]=cumulative[i-1]+Vector3.Distance(samples[i-1],samples[i]);
            }
            var result=new Vector3[count];
            for(int c=0;c<count;c++)
            {
                float distance=cumulative[720]*c/count;int j=1;while(j<720&&cumulative[j]<distance)j++;
                result[c]=Vector3.Lerp(samples[j-1],samples[j],Mathf.InverseLerp(cumulative[j-1],cumulative[j],distance));
            }
            return result;
        }
        private static GameObject Composition(Transform parent,Vector3[] positions,int[] ids,float x,float z,string name,string key)
        {
            var root=new GameObject(name);root.transform.SetParent(parent,false);
            var decor=new GameObject("HandPlacedGrove");decor.transform.SetParent(root.transform,false);
            var island=Place("Island",root.transform,Vector3.zero,1,0);
            island.transform.localScale=new Vector3(x+.85f,1,z+.85f);
            for(int i=0;i<positions.Length;i++)
            {
                var cell=new GameObject("Cell_"+ids[i]);cell.transform.SetParent(root.transform,false);cell.transform.localPosition=positions[i]-Vector3.up*.23f;
                var tile=Place("Tile_"+(i%4),cell.transform,Vector3.zero,1,0);
                tile.transform.localRotation=Quaternion.Euler(0,Mathf.Atan2(positions[(i+1)%positions.Length].x-positions[i].x,positions[(i+1)%positions.Length].z-positions[i].z)*Mathf.Rad2Deg+90,0);
                var box=cell.AddComponent<BoxCollider>();box.center=new Vector3(0,.15f,0);box.size=new Vector3(1,.35f,1);
                var marker=cell.AddComponent<DioramaCell>();marker.cellId=ids[i];
                var h=Primitive(PrimitiveType.Cylinder,"Available move",cell.transform,new Vector3(0,.225f,0),new Vector3(.93f,.009f,.93f),glow);
                marker.highlight=h.GetComponent<Renderer>();marker.highlight.enabled=false;
            }
            // Asymmetric central grove keeps the near edge open for characters.
            Place("Pine_2",decor.transform,new Vector3(.65f,0,z*.40f),.92f,15);
            Place("Pine_0",decor.transform,new Vector3(-.24f,0,z*.58f),.70f,-15);
            Place("Pine_1",decor.transform,new Vector3(1.10f,0,z*.09f),.58f,30);
            Place("Bridge",decor.transform,new Vector3(-.65f,.03f,.08f),.83f,70);
            var pond=Primitive(PrimitiveType.Sphere,"Quiet pond",root.transform,new Vector3(-.60f,.075f,.15f),new Vector3(1.6f,.045f,1.35f),water);
            pond.AddComponent<WoodlandWater>();
            Place("Crate",decor.transform,new Vector3(-x*.30f,0,z*.48f),.75f,18);
            Place("Stump",decor.transform,new Vector3(x*.35f,0,-z*.42f),.75f,0);
            for(int k=0;k<8;k++)
            {
                float a=k*Mathf.PI*.25f;
                Place("Rock_"+(k%5),decor.transform,new Vector3(-.6f+Mathf.Cos(a)*.88f,.02f,.15f+Mathf.Sin(a)*.69f),.44f,k*43);
            }
            var rng=new System.Random(231+positions.Length);
            for(int i=0;i<positions.Length;i++)
            {
                Vector3 p=positions[i];p.y=0;
                Vector3 outward=new Vector3(p.x/(x*x),0,p.z/(z*z)).normalized;
                Place("Foliage_"+(i%6),decor.transform,p+outward*.68f,.8f+(float)rng.NextDouble()*.5f,i*57);
                Place("Foliage_"+((i+2)%6),decor.transform,p-outward*.70f,.7f,i*41);
                Place("Foliage_"+((i+1)%3),decor.transform,p+outward*.82f+Vector3.right*.25f,1.05f,i*73);
                if(i%3==0)Place("Rock_"+(i%5),decor.transform,p+outward*.75f,.75f,i*37);
                if(i%4==1)Place("Fence",decor.transform,p+outward*.82f,.65f,Mathf.Atan2(outward.x,outward.z)*Mathf.Rad2Deg);
                if(i%5==2 && p.z>0)Place("Pine_"+(i%3),decor.transform,p+outward*.87f,.6f,i*21);
            }
            Place("Flag",root.transform,positions[0]+new Vector3(.62f,-.23f,0),.70f,0).AddComponent<WoodlandBreeze>();
            Place("Sign",decor.transform,positions[positions.Length/2]+new Vector3(-.6f,-.23f,.1f),.8f,180);
            // Combine static atlas decoration into one mesh; keep gameplay colliders and water separate.
            var meshes=decor.GetComponentsInChildren<MeshFilter>();
            var combines=meshes.Select(m=>new CombineInstance {mesh=m.sharedMesh,transform=root.transform.worldToLocalMatrix*m.transform.localToWorldMatrix}).ToArray();
            var combined=new Mesh {indexFormat=IndexFormat.UInt32};combined.CombineMeshes(combines,true,true);
            // Preserve each source's packed secondary UVs in a separate grid slot.
            // Re-unwrapping the entire grove creates thousands of tiny charts and is unnecessarily expensive.
            int columns=Mathf.CeilToInt(Mathf.Sqrt(meshes.Length));
            var secondary=new Vector2[combined.vertexCount];int vertex=0;
            for(int m=0;m<meshes.Length;m++)
            {
                var source=meshes[m].sharedMesh;var uv=source.uv2;
                if(uv.Length!=source.vertexCount)uv=source.uv;
                for(int v=0;v<source.vertexCount;v++)
                {
                    Vector2 point=v<uv.Length?uv[v]:Vector2.one*.5f;
                    secondary[vertex++]=(new Vector2(m%columns,m/columns)+Vector2.one*.035f+point*.93f)/columns;
                }
            }
            combined.uv2=secondary;
            string meshPath=Data+"/"+key+"_"+name+"_Grove.asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(existing!=null){EditorUtility.CopySerialized(combined,existing);UnityEngine.Object.DestroyImmediate(combined);combined=existing;}
            else AssetDatabase.CreateAsset(combined,meshPath);
            UnityEngine.Object.DestroyImmediate(decor);
            var grove=new GameObject("Grove");grove.transform.SetParent(root.transform,false);grove.AddComponent<MeshFilter>().sharedMesh=combined;grove.AddComponent<MeshRenderer>().sharedMaterial=atlas;
            return root;
        }
        private static GameObject Place(string asset,Transform parent,Vector3 position,float scale,float angle)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/"+asset+".fbx");
            if(prefab==null)throw new InvalidOperationException("Missing Blender asset "+asset);
            var go=new GameObject(asset);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,angle,0);go.transform.localScale=Vector3.one*scale;
            UnityEngine.Object.Instantiate(prefab,go.transform);
            foreach(var renderer in go.GetComponentsInChildren<Renderer>())renderer.sharedMaterials=Enumerable.Repeat(atlas,renderer.sharedMaterials.Length).ToArray();
            return go;
        }
        private static GameObject Primitive(PrimitiveType type,string name,Transform parent,Vector3 pos,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
    }
}
