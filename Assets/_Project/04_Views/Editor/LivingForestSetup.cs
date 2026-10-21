using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace Diceforge.View.Editor
{
    public static class LivingForestSetup
    {
        public const string Folder="Assets/_Project/07_Art/LivingForest/";
        private const string Board="Assets/_Project/05_Gameplay_Data/Battle/Diorama/FirstTrail8.prefab";
        private const string Original="Assets/_Project/07_Art/FirstTrail/Materials/Polished/";
        private const string Hero="Assets/_Project/05_Gameplay_Data/Battle/WoodlandHero/";
        private static readonly Dictionary<string,Material> materials=new();

        [MenuItem("Diceforge/Woodland/Build living forest")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before rebuilding the forest.");
            Directory.CreateDirectory(Folder+"Materials");Directory.CreateDirectory(Folder+"Prefabs");
            AssetDatabase.Refresh();
            BuildMaterials();
            BuildPrefabs();
            var settings=Asset<LivingForestProfile>(Folder+"LivingForestProfile.asset");
            var root=PrefabUtility.LoadPrefabContents(Board);
            try
            {
                var board=root.GetComponent<DioramaBoard>();
                string geometry=EditorJsonUtility.ToJson(board.layout);
                var cells=root.GetComponentsInChildren<DioramaCell>(true);
                var anchors=cells.Select(c=>c.transform.localPosition).ToArray();
                var old=root.transform.Find("Living forest");
                if(old!=null)Object.DestroyImmediate(old.gameObject);
                var environment=new GameObject("Living forest");environment.transform.SetParent(root.transform,false);
                var art=board.landscapeRoot.transform.Cast<Transform>().First(t=>t.name=="FirstTrailPolished");
                foreach(var renderer in art.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.enabled=true;
                    string name=renderer.name;
                    if(name.StartsWith("Rounded pine bough") || name.StartsWith("Reference pine trunk") ||
                       name.StartsWith("Pine root") || name.StartsWith("Distant moss bank") ||
                       name.StartsWith("Underwater pebble") || name.StartsWith("Submerged sandy lake shelf") ||
                       name.StartsWith("Water creek flowing") || name.StartsWith("Creek outlet cascade") || name.StartsWith("Waterfall landing foam") ||
                       name.StartsWith("Distant shoreline rock") || name.StartsWith("Foreground soft") ||
                       name.StartsWith("Exit post") || name.StartsWith("Exit lintel") || name.StartsWith("Exit pennant") ||
                       name.StartsWith("Pennant pole") || name.StartsWith("Pole brass cap"))
                        renderer.gameObject.SetActive(false);
                    else
                    {
                        var array=renderer.sharedMaterials;
                        for(int i=0;i<array.Length;i++)
                        {
                            string source=array[i].name.Split('.')[0];
                            if(source=="TrialStone0" || source=="TrialStone1")array[i]=materials["ForestStone"];
                            else if(source=="TrailGround")array[i]=materials["ForestGround"];
                            else if(source=="ShoreSand")array[i]=materials["ForestSand"];
                            else if(source.StartsWith("IslandLimestone"))array[i]=materials["ForestShore"];
                            else if(source=="TrailWater")array[i]=materials["ForestRiver"];
                            else if(source=="TrailLake")array[i]=materials["ForestLake"];
                            else if(source=="TrailSplash")array[i]=materials["ForestSplash"];
                        }
                        renderer.sharedMaterials=array;
                    }
                }
                // Source animations target the replaced pennant and old water shader.
                var previousLife=root.GetComponent<FirstTrailLife>();if(previousLife!=null)previousLife.enabled=false;
                Add(environment.transform,"Oak_Tripo_A",new Vector3(-3.05f,.025f,1.2f),.87f,18);
                Add(environment.transform,"Fir_Tripo_A",new Vector3(-1.55f,.025f,1.65f),.68f,15);
                Add(environment.transform,"Fir_Tripo_A",new Vector3(-.60f,.025f,1.88f),.47f,68);
                Add(environment.transform,"Fir_Tripo_A",new Vector3(3.02f,.025f,-.38f),.45f,-40);
                Add(environment.transform,"BackdropBank_A",new Vector3(0,-.35f,5f),1f,0);
                Add(environment.transform,"CreekBed_A",Vector3.zero,1f,0);
                Add(environment.transform,"CreekPebbles_A",Vector3.zero,1f,0);
                Add(environment.transform,"ShorePebbles_A",Vector3.zero,1f,0);
                Add(environment.transform,"AsymmetricShelf_A",Vector3.zero,1f,0);
                var river=Add(environment.transform,"RiverSurface_A",Vector3.zero,1f,0);
                var spray=Add(environment.transform,"WaterImpactSpray_A",Vector3.zero,1f,0);
                foreach(var renderer in spray.GetComponentsInChildren<Renderer>())
                {
                    renderer.shadowCastingMode=ShadowCastingMode.Off;
                }
                Add(environment.transform,"RiverRocks_A",Vector3.zero,1f,0);
                // Unequal foreground shoals break the old perfectly oval waterline.
                Add(environment.transform,"Boulder_C",new Vector3(-3.6f,-.84f,-1.55f),1.55f,37);
                Add(environment.transform,"RockGarden_Tripo_A",new Vector3(-3.72f,-.53f,-1.75f),.78f,117);
                Add(environment.transform,"Boulder_A",new Vector3(3.7f,-.88f,-1.55f),.9f,79);
                var frame=environment.AddComponent<LivingForestForeground>();
                frame.leftBranch=Add(environment.transform,"ForegroundBranch_A",Vector3.zero,1f,0).transform;
                frame.rightBranch=Add(environment.transform,"ForegroundBranch_B",Vector3.zero,1f,0).transform;
                foreach(var branch in new[]{frame.leftBranch,frame.rightBranch})
                    foreach(var renderer in branch.GetComponentsInChildren<Renderer>())
                    {
                        renderer.gameObject.layer=LivingForestForeground.ForegroundLayer;
                        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                    }
                for(int i=0;i<24;i++)
                {
                    float x=-7.5f+(i%12)*1.38f+Mathf.Sin(i*7.3f)*.32f;
                    float z=(i<12?3.9f:5.8f)+Mathf.Sin(i*3.7f)*.50f;
                    if(x>2.5f && x<5f)z+=1f;
                    var tree=Add(environment.transform,i%5==0?"Oak_Tripo_A":"Fir_Tripo_A",new Vector3(x,BackgroundHeight(x,z),z),.78f+(i%3)*.14f,i*37);
                    foreach(var r in tree.GetComponentsInChildren<Renderer>())r.shadowCastingMode=ShadowCastingMode.Off;
                }
                for(int i=0;i<18;i++)
                {
                    float x=-7.5f+i*.88f,z=2f-.35f*Mathf.Sin(x*.8f)-.18f*Mathf.Cos(x*2)+.18f;
                    Add(environment.transform,i%3==0?"RockGarden_Tripo_A":"Shrub_A",new Vector3(x,BackgroundHeight(x,z)-.06f,z),.60f+(i%4)*.10f,i*53);
                }
                for(int i=0;i<36;i++)
                {
                    float x=-7.6f+(i%18)*.89f+Mathf.Sin(i*5.7f)*.18f;
                    float z=2.6f+(i/18)*.72f+Mathf.Sin(i*3.1f)*.18f;
                    float scale=x>2.9f && x<4.6f?.70f:1.10f+(i%4)*.18f;
                    Add(environment.transform,i%8==0?"RockGarden_Tripo_A":i%3==0?"Fern_A":"Shrub_A",
                        new Vector3(x,BackgroundHeight(x,z),z),scale,i*47);
                }
                var bushes=new[]{new Vector3(-2.85f,.03f,-.20f),new Vector3(-2.0f,.03f,1.12f),new Vector3(-.7f,.03f,-1.65f),
                    new Vector3(.50f,.03f,1.95f),new Vector3(2.7f,.03f,-1.3f),new Vector3(3.28f,.03f,.50f)};
                for(int i=0;i<bushes.Length;i++)Add(environment.transform,i%2==0?"CanopyShrub_A":"CanopyShrub_B",bushes[i],1f,i*53);
                var bankPlants=new[]{new Vector3(1.3f,.01f,-1.5f),new Vector3(1.0f,.01f,-.95f),new Vector3(1.84f,.01f,-.45f),
                    new Vector3(2.50f,.01f,.15f),new Vector3(2.5f,.01f,1.75f)};
                for(int i=0;i<bankPlants.Length;i++)Add(environment.transform,"Reeds_A",bankPlants[i],.72f+(i%2)*.14f,i*64);
                for(int i=0;i<7;i++)
                {
                    float x=-2.6f+i*.8f,z=-1.85f+Mathf.Sin(i*1.4f)*.15f;
                    Add(environment.transform,i%2==0?"Fern_A":"Flowers_A",new Vector3(x,.035f,z),.85f,i*73);
                }
                Add(environment.transform,"Boulder_A",new Vector3(-2.3f,.01f,1.45f),.85f,18);
                Add(environment.transform,"Boulder_C",new Vector3(.15f,.01f,-1.67f),.7f,43);
                Add(environment.transform,"Boulder_B",new Vector3(2.75f,-.04f,-1.50f),1.1f,95);
                Add(environment.transform,"RockGarden_Tripo_A",new Vector3(-2.6f,.015f,1.55f),.75f,18);
                Add(environment.transform,"RockGarden_Tripo_A",new Vector3(-.12f,.015f,1.75f),.70f,-32);
                Add(environment.transform,"RockGarden_Tripo_A",new Vector3(.25f,.015f,-1.78f),.70f,66);
                Add(environment.transform,"RockGarden_Tripo_A",new Vector3(2.8f,-.04f,-1.4f),.58f,-23);
                AddGroundCover(environment.transform);
                // A real contact surface sits exactly beneath the existing exit anchor.
                Vector3 exit=board.CellPosition(7)+(board.CellPosition(7)-board.CellPosition(6)).normalized*.85f;
                var support=Add(environment.transform,"Boulder_C",exit+Vector3.down*.85f,1.5f,36);
                support.name="Exit support boulder";
                Add(environment.transform,"ExitStone_A",exit,1f,0);
                var pole=Add(environment.transform,"ExitPost_A",exit+new Vector3(.25f,-.12f,.39f),1f,0);
                var flag=Add(environment.transform,"Flag_A",pole.transform.localPosition+new Vector3(-.08f,1.29f,0),1f,0);
                var lamp=Add(environment.transform,"Lantern_A",pole.transform.localPosition+new Vector3(-.55f,1.31f,0),1f,0);
                var point=new GameObject("Warm lantern light");point.transform.SetParent(lamp.transform,false);point.transform.localPosition=new Vector3(0,-.22f,0);
                var light=point.AddComponent<Light>();light.type=LightType.Point;light.range=2.2f;light.shadows=LightShadows.None;
                var motion=environment.AddComponent<LivingForestMotion>();motion.profile=settings;motion.lantern=lamp.transform;motion.lanternLight=light;
                motion.impactSpray=spray.GetComponentInChildren<Renderer>();
                var mirrors=environment.AddComponent<ForestWaterReflections>();mirrors.profile=settings;
                var water=art.GetComponentsInChildren<Renderer>(true).Where(r=>r.sharedMaterial!=null &&
                    r.name.StartsWith("Lake surround")).ToArray();
                mirrors.planes=water.Select(r=>new ForestWaterReflections.Plane{surface=r}).Concat(new[]{
                    new ForestWaterReflections.Plane{surface=river.GetComponentInChildren<Renderer>(),useAuthoredHeight=true,height=.045f}}).ToArray();
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                    if(renderer.sharedMaterial!=null && (renderer.sharedMaterial.shader.name=="Diceforge/Living Forest Water" ||
                        renderer.sharedMaterial.shader.name=="Diceforge/Living Forest Splash"))renderer.gameObject.layer=4;
                var stage=root.GetComponent<WoodlandHeroStage>();stage.grade=BuildGrade();stage.sunIntensity=2.1f;stage.fillIntensity=.65f;
                stage.sunEuler=new Vector3(48,150,0);stage.fillEuler=new Vector3(35,-25,0);
                ConfigurePipeline(board.desktopPipeline,true);ConfigurePipeline(board.mobilePipeline,false);
                ConfigurePipeline(stage.pipelineOverride,true);
                FirstTrailPresentationSetup.BindVisuals(board);
                LivingForestStaticMeshes.Build(art,environment.transform,board);
                for(int i=0;i<cells.Length;i++)if(cells[i].transform.localPosition!=anchors[i])throw new InvalidOperationException("A landing moved.");
                if(EditorJsonUtility.ToJson(board.layout)!=geometry)throw new InvalidOperationException("Gameplay geometry changed.");
                PrefabUtility.SaveAsPrefabAsset(root,Board);
                Debug.Log("Living forest applied: modular kit, Tripo oak, animated flora, lantern, cloth and reflected/refracted water. Eight landings preserved.");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
        }

        private static float BackgroundHeight(float x,float z)
        {
            float localZ=-(z-5f);
            return -.35f+.25f+.30f*Mathf.Sin(x*.4f+localZ*.3f)+.13f*Mathf.Cos(x*.8f-localZ*.6f)
                +.8f*Mathf.Exp(-((x+4)*(x+4)+(localZ-1)*(localZ-1))/7)
                +.65f*Mathf.Exp(-((x-4)*(x-4)+localZ*localZ)/5);
        }

        private static void AddGroundCover(Transform parent)
        {
            // Clusters frame the path; the tile tops and the open creek remain clear.
            var centers=new[]{new Vector2(-2.5f,.7f),new Vector2(-1.6f,1.3f),new Vector2(-.3f,1.6f),
                new Vector2(-1.4f,-1.6f),new Vector2(-.2f,-1.75f),new Vector2(2.6f,-1.2f),new Vector2(3.25f,.5f)};
            for(int c=0;c<centers.Length;c++)for(int j=0;j<15;j++)
            {
                float a=j*2.39996f,r=.12f+.055f*Mathf.Sqrt(j);
                var p=centers[c]+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r;
                Add(parent,j%7==0?"Fern_A":"GrassTuft_A",new Vector3(p.x,.035f,p.y),.65f+(j%4)*.13f,j*53+c*19);
            }
            for(int i=0;i<7;i++)
            {
                if(i==6)continue; // Keep the waterfall lip free of hanging ivy.
                Add(parent,"Ivy_A",new Vector3(-2.5f+i*.76f,-.03f,-1.97f+Mathf.Sin(i*.8f)*.22f),.6f+(i%3)*.15f,0);
            }
        }

        private static T Asset<T>(string path) where T:ScriptableObject
        {
            var asset=AssetDatabase.LoadAssetAtPath<T>(path);if(asset!=null)return asset;
            asset=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(asset,path);return asset;
        }
        private static GameObject Add(Transform parent,string name,Vector3 position,float scale,float yaw)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"Prefabs/"+name+".prefab")
                ??throw new InvalidOperationException("Missing forest model "+name);
            var root=(GameObject)PrefabUtility.InstantiatePrefab(source,parent);
            root.name=name;root.transform.localPosition=position;root.transform.localScale=Vector3.one*scale;root.transform.localRotation=Quaternion.Euler(0,yaw,0);
            foreach(var renderer in root.GetComponentsInChildren<Renderer>())
            {
                var array=renderer.sharedMaterials;
                for(int i=0;i<array.Length;i++)
                {
                    string key=array[i].name.Split('.')[0];
                    if(materials.TryGetValue(key,out var material))array[i]=material;
                    else throw new InvalidOperationException("Unmapped forest material "+key);
                }
                renderer.sharedMaterials=array;renderer.shadowCastingMode=ShadowCastingMode.On;
                renderer.lightProbeUsage=LightProbeUsage.BlendProbes;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            }
            return root;
        }
        private static void BuildPrefabs()
        {
            foreach(string file in Directory.GetFiles(Folder+"Models","*.fbx"))
            {
                string name=Path.GetFileNameWithoutExtension(file);
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(file.Replace('\\','/'));
                // Keep the FBX axis conversion on a child. Placement rotates only this
                // wrapper, otherwise a yaw assignment turns a Z-up tree onto its side.
                var root=new GameObject(name);
                try
                {
                    PrefabUtility.InstantiatePrefab(model,root.transform);
                    foreach(var renderer in root.GetComponentsInChildren<Renderer>())
                        renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>name=="BackdropBank_A"?materials[m.name=="ForestStone"?"ForestShore":"ForestBackdrop"]:
                            name.StartsWith("Boulder_")?materials["ForestBoulder"]:materials[m.name.Split('.')[0]]).ToArray();
                    PrefabUtility.SaveAsPrefabAsset(root,Folder+"Prefabs/"+name+".prefab");
                }
                finally{Object.DestroyImmediate(root);}
            }
        }
        private static Material Material(string name,string shader,Color color,string source=null)
        {
            string path=Folder+"Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(material,path);}
            if(source!=null){var original=AssetDatabase.LoadAssetAtPath<Material>(source);if(original!=null)material.CopyPropertiesFromMaterial(original);}
            material.shader=Shader.Find(shader);material.SetColor("_BaseColor",color);
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.16f);
            material.enableInstancing=true;
            EditorUtility.SetDirty(material);materials[name]=material;return material;
        }
        private static void BuildMaterials()
        {
            materials.Clear();const string lit="Universal Render Pipeline/Lit",leaf="Diceforge/Living Forest Foliage";
            Material("ForestBark",lit,new Color(.76f,.67f,.54f),Hero+"HeroBark.mat");
            Material("ForestWood",lit,new Color(.83f,.74f,.59f),Hero+"HeroWood.mat");
            Stone("ForestStone",new Color(.79f,.72f,.56f),.10f,5.5f);
            Stone("ForestBoulder",new Color(.57f,.55f,.44f),.62f,4.8f);
            Stone("ForestShore",new Color(.67f,.63f,.49f),.32f,3.4f);
            Stone("ForestWetStone",new Color(.47f,.43f,.34f),0,14f);
            Material("ForestGround",lit,Color.white,Original+"TrailGround.mat");
            Material("ForestSand",lit,new Color(.58f,.60f,.44f),Original+"ShoreSand.mat");
            Stone("ForestBackdrop",new Color(.37f,.40f,.20f),.88f,3.5f);
            var metal=Material("ForestMetal",lit,new Color(.29f,.18f,.075f));metal.SetFloat("_Metallic",.75f);metal.SetFloat("_Smoothness",.32f);
            var glass=Material("ForestGlass",lit,new Color(1f,.61f,.22f,.28f));glass.SetFloat("_Surface",1);glass.SetFloat("_ZWrite",0);
            glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.renderQueue=3000;glass.SetFloat("_Smoothness",.8f);
            var flame=Material("ForestFlame",lit,new Color(1f,.78f,.33f));flame.EnableKeyword("_EMISSION");flame.SetColor("_EmissionColor",new Color(2.8f,1.5f,.35f));
            var green=new Dictionary<string,Color>{{"ForestLeaf",new Color(.42f,.56f,.20f)},{"ForestNeedle",new Color(.28f,.43f,.20f)},
                {"ForestReed",new Color(.52f,.60f,.28f)},{"ForestSeed",new Color(.31f,.17f,.07f)},
                {"ForestCloth",new Color(.73f,.24f,.09f)},{"ForestPetal",new Color(.96f,.91f,.73f)},{"ForestPollen",new Color(.94f,.61f,.12f)}};
            foreach(var pair in green)
            {
                var material=Material(pair.Key,leaf,pair.Value);material.SetFloat("_WindStrength",pair.Key=="ForestCloth"?.065f:.035f);
                material.SetFloat("_Translucency",pair.Key=="ForestCloth"?.08f:.22f);
                material.SetFloat("_Emblem",pair.Key=="ForestCloth"?1:0);
            }
            var oakTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"Textures/OakColor.jpg");
            var oakBark=Material("OakBark",lit,new Color(.88f,.80f,.69f));oakBark.SetTexture("_BaseMap",oakTexture);
            var oakLeaf=Material("OakLeaves",leaf,new Color(.72f,.79f,.56f));oakLeaf.SetTexture("_BaseMap",oakTexture);
            oakLeaf.SetFloat("_WindStrength",.055f);oakLeaf.SetFloat("_Translucency",.16f);oakLeaf.SetFloat("_Variation",.12f);
            var firTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"Textures/FirColor.jpg");
            var firBody=Material("FirBody",lit,new Color(.75f,.69f,.57f));firBody.SetTexture("_BaseMap",firTexture);
            var firLeaf=Material("FirLeaves",leaf,new Color(.54f,.66f,.47f));firLeaf.SetTexture("_BaseMap",firTexture);
            firLeaf.SetFloat("_WindStrength",.045f);firLeaf.SetFloat("_Translucency",.16f);firLeaf.SetFloat("_Variation",.18f);
            firLeaf.SetFloat("_Saturation",.72f);
            var gardenTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"Textures/GardenColor.jpg");
            var gardenBody=Material("GardenBody",lit,new Color(.85f,.83f,.70f));gardenBody.SetTexture("_BaseMap",gardenTexture);
            var gardenLeaf=Material("GardenLeaves",leaf,new Color(.82f,.85f,.69f));gardenLeaf.SetTexture("_BaseMap",gardenTexture);
            gardenLeaf.SetFloat("_WindStrength",0);gardenLeaf.SetFloat("_Saturation",.60f);gardenLeaf.SetFloat("_Translucency",.08f);
            var splash=Material("ForestSplash","Diceforge/Living Forest Splash",Color.white);
            splash.SetFloat("_RingStrength",.40f);splash.SetFloat("_Opacity",.42f);
            var spray=Material("ForestSpray","Diceforge/Living Forest Splash",Color.white);
            spray.SetFloat("_Spray",1);spray.SetFloat("_Opacity",.72f);
            spray.SetColor("_FoamColor",new Color(.88f,.95f,.92f));
            foreach(string name in new[]{"ForestRiver","ForestLake"})
            {
                var material=Material(name,"Diceforge/Living Forest Water",Color.white);
                material.SetColor("_ShallowColor",new Color(.56f,.79f,.67f));material.SetColor("_DeepColor",new Color(.045f,.31f,.34f));
                if(name=="ForestRiver")
                {
                    material.SetColor("_ShallowColor",new Color(.66f,.86f,.82f));
                    material.SetColor("_DeepColor",new Color(.025f,.35f,.40f));
                }
                material.SetColor("_FoamColor",new Color(.86f,.93f,.88f));
                material.SetFloat("_Density",name=="ForestRiver"?2.8f:1.05f);material.SetFloat("_WaveStrength",name=="ForestRiver"?.16f:.085f);
                material.SetFloat("_ReflectionStrength",name=="ForestRiver"?.48f:.72f);
                material.SetFloat("_FlowSpeed",name=="ForestRiver"?.82f:.22f);material.SetFloat("_River",name=="ForestRiver"?1:0);
                // The lake writes no depth: equal transparent queues can cover the fall.
                material.renderQueue=name=="ForestRiver"?2981:2980;
            }
            BuildGroundTexture();
        }
        private static void Stone(string name,Color color,float moss,float scale)
        {
            var material=Material(name,"Diceforge/Weathered Forest Stone",color);
            material.SetColor("_MossColor",new Color(.24f,.33f,.085f));
            material.SetFloat("_MossAmount",moss);material.SetFloat("_Scale",scale);
            material.SetFloat("_Relief",.16f);
        }
        private static void BuildGroundTexture()
        {
            const int size=1024;var texture=new Texture2D(size,size,TextureFormat.RGBA32,true);var pixels=new Color[size*size];
            Vector2[] path={new(-2.85f,-1.05f),new(-1.96f,-1.02f),new(-1.08f,-.80f),new(-.29f,-.36f),new(.37f,.24f),new(.88f,.98f),new(1.8f,1.36f),new(2.84f,1.38f)};
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=x/(float)(size-1),v=y/(float)(size-1);var p=new Vector2((u-.5f)*7.5f,(v-.5f)*4.5f);
                float distance=100;
                for(int i=0;i<7;i++){Vector2 segment=path[i+1]-path[i];float t=Mathf.Clamp01(Vector2.Dot(p-path[i],segment)/segment.sqrMagnitude);distance=Mathf.Min(distance,Vector2.Distance(p,path[i]+segment*t));}
                float broad=Mathf.PerlinNoise(u*6+17,v*6+31),grain=Mathf.PerlinNoise(u*190+7,v*190+9);
                float moss=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.32f,.9f,distance+(broad-.5f)*.4f))*.80f;
                var earth=Color.Lerp(new Color(.44f,.32f,.18f),new Color(.65f,.51f,.32f),broad);
                var green=Color.Lerp(new Color(.28f,.37f,.13f),new Color(.48f,.51f,.24f),broad);
                pixels[y*size+x]=Color.Lerp(earth,green,moss)*(.93f+grain*.13f);pixels[y*size+x].a=1;
            }
            texture.SetPixels(pixels);texture.Apply();string pathName=Folder+"Textures/ForestGroundColor.png";
            File.WriteAllBytes(pathName,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(pathName);
            var importer=(TextureImporter)AssetImporter.GetAtPath(pathName);importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=true;importer.maxTextureSize=1024;importer.SaveAndReimport();
            materials["ForestGround"].SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(pathName));
        }
        private static VolumeProfile BuildGrade()
        {
            var grade=Asset<VolumeProfile>(Folder+"ForestGrade.asset");
            var tone=grade.TryGet<Tonemapping>(out var t)?t:grade.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.ACES);
            var color=grade.TryGet<ColorAdjustments>(out var c)?c:grade.Add<ColorAdjustments>(true);color.postExposure.Override(.30f);color.contrast.Override(7);color.saturation.Override(-4);
            var bloom=grade.TryGet<Bloom>(out var b)?b:grade.Add<Bloom>(true);bloom.intensity.Override(.10f);bloom.threshold.Override(1.2f);bloom.scatter.Override(.55f);
            // Explicit Off overrides the older lower-priority scene grade as well.
            var depth=grade.TryGet<DepthOfField>(out var d)?d:grade.Add<DepthOfField>(true);depth.mode.Override(DepthOfFieldMode.Off);
            foreach(var component in grade.components)if(string.IsNullOrEmpty(AssetDatabase.GetAssetPath(component)))AssetDatabase.AddObjectToAsset(component,grade);
            EditorUtility.SetDirty(grade);return grade;
        }
        private static void ConfigurePipeline(UniversalRenderPipelineAsset pipeline,bool ambientOcclusion)
        {
            if(pipeline==null)return;
            pipeline.supportsCameraDepthTexture=true;pipeline.supportsCameraOpaqueTexture=true;pipeline.supportsHDR=true;
            var serialized=new SerializedObject(pipeline);var list=serialized.FindProperty("m_RendererDataList");
            var renderer=(ScriptableRendererData)list.GetArrayElementAtIndex(0).objectReferenceValue;
            if(ambientOcclusion && renderer is UniversalRendererData)
            {
                var feature=renderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault();
                if(feature==null){feature=ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();feature.name="Forest contact ambient occlusion";AssetDatabase.AddObjectToAsset(feature,renderer);renderer.rendererFeatures.Add(feature);}
                var so=new SerializedObject(feature);var settings=so.FindProperty("m_Settings");
                settings.FindPropertyRelative("Source").enumValueIndex=0;settings.FindPropertyRelative("AfterOpaque").boolValue=true;
                settings.FindPropertyRelative("Intensity").floatValue=.70f;settings.FindPropertyRelative("Radius").floatValue=.16f;
                settings.FindPropertyRelative("Downsample").boolValue=true;
                so.ApplyModifiedPropertiesWithoutUndo();feature.SetActive(true);renderer.SetDirty();EditorUtility.SetDirty(feature);EditorUtility.SetDirty(renderer);
            }
            EditorUtility.SetDirty(pipeline);
        }
    }
}
