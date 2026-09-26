using System.IO;
using UnityEditor;
using UnityEngine;

namespace Diceforge.View.Editor
{
    internal static class FirstTrailArtMaterials
    {
        internal const string Folder="Assets/_Project/07_Art/FirstTrail/Materials/";
        private const string Hero="Assets/_Project/05_Gameplay_Data/Battle/WoodlandHero/";
        private static readonly Vector2[] Path={new(-2.85f,-1.05f),new(-1.96f,-1.02f),new(-1.08f,-.80f),new(-.29f,-.36f),new(.37f,.24f),new(.88f,.98f),new(1.8f,1.36f),new(2.84f,1.38f)};
        internal static void Build()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var ground=Copy("TrailGround","HeroEarth");
            const int size=768;var pixels=new Color[size*size];var normals=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=(float)x/(size-1),v=(float)y/(size-1);var p=new Vector2((u-.5f)*7.5f,(v-.5f)*4.5f);
                float distance=100;
                for(int j=0;j<Path.Length-1;j++)
                {
                    Vector2 ab=Path[j+1]-Path[j];float t=Mathf.Clamp01(Vector2.Dot(p-Path[j],ab)/ab.sqrMagnitude);
                    distance=Mathf.Min(distance,Vector2.Distance(p,Path[j]+ab*t));
                }
                float camp=new Vector2((p.x+2.7f)*.8f,(p.y-.15f)*1.1f).magnitude;
                distance=Mathf.Min(distance,Mathf.Max(0,camp-.35f));
                float broad=Mathf.PerlinNoise(u*12+23,v*10+41),fine=Mathf.PerlinNoise(u*190,v*170);
                float moss=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.34f,.86f,distance+(broad-.5f)*.35f));
                moss*=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,.58f,broad));
                Color dirt=Color.Lerp(new Color(.34f,.245f,.14f),new Color(.50f,.375f,.23f),broad);
                Color grass=Color.Lerp(new Color(.26f,.32f,.105f),new Color(.43f,.47f,.20f),broad);
                pixels[y*size+x]=Color.Lerp(dirt,grass,moss)*( .90f+fine*.19f);pixels[y*size+x].a=1;
                float dx=(fine-Mathf.PerlinNoise((u+.0013f)*190,v*170))*.45f;
                float dy=(fine-Mathf.PerlinNoise(u*190,(v+.0013f)*170))*.45f;
                var n=new Vector3(dx,dy,1).normalized;normals[y*size+x]=new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f,1);
            }
            SaveTexture("TrailGroundColor",size,pixels,false);SaveTexture("TrailGroundNormal",size,normals,true);
            ground.color=Color.white;ground.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"TrailGroundColor.png");
            ground.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"TrailGroundNormal.png"));ground.SetFloat("_BumpScale",.7f);ground.EnableKeyword("_NORMALMAP");ground.SetFloat("_Smoothness",.08f);EditorUtility.SetDirty(ground);
            var wood=Copy("TrailWood","HeroWood");wood.color=new Color(.85f,.93f,1);wood.SetFloat("_BumpScale",.35f);EditorUtility.SetDirty(wood);
            BuildShoreTexture();
            SetShoreMaterial("ShoreStone","HeroStone",new Color(.94f,.97f,1),.14f);
            SetShoreMaterial("ShoreStoneLight","HeroStone",new Color(1.06f,1.04f,1),.14f);
            SetShoreMaterial("ShoreStoneCool","HeroStone",new Color(.89f,.96f,1.03f),.12f);
            SetShoreMaterial("ShoreMoss","HeroMoss",new Color(.78f,.86f,.65f),.05f);
            SetShoreMaterial("ShoreLeaf","HeroPine",new Color(1.35f,1.28f,.72f),.28f);
            SetShoreMaterial("ShoreFoam","HeroStone",new Color(.46f,.78f,.73f),.38f);
            SetShoreMaterial("ShoreSand","HeroStone",new Color(.72f,.74f,.63f),.05f);
            var splash=AssetDatabase.LoadAssetAtPath<Material>(Folder+"TrailSplash.mat");
            if(splash==null){splash=new Material(Shader.Find("Diceforge/Waterfall Splash"));AssetDatabase.CreateAsset(splash,Folder+"TrailSplash.mat");}
            var needles=Copy("TrailNeedles","HeroPine");needles.color=new Color(.94f,1,.73f);needles.SetFloat("_Smoothness",.22f);EditorUtility.SetDirty(needles);
            var lake=AssetDatabase.LoadAssetAtPath<Material>(Folder+"TrailLake.mat");
            if(lake==null){lake=new Material(Shader.Find("Diceforge/First Trail Lake"));AssetDatabase.CreateAsset(lake,Folder+"TrailLake.mat");}
            lake.shader=Shader.Find("Diceforge/Stylized Water Surface");
            lake.SetColor("_DeepColor",new Color(.02f,.20f,.34f));lake.SetColor("_ShallowColor",new Color(.065f,.56f,.72f));
            lake.SetColor("_FoamColor",new Color(.76f,.93f,.84f));lake.SetFloat("_DepthRange",1.4f);lake.SetFloat("_River",0);EditorUtility.SetDirty(lake);
            var canvas=Copy("TrailCanvas","HeroStone");canvas.color=new Color(.80f,.60f,.37f);canvas.SetFloat("_Cull",0);canvas.SetFloat("_Smoothness",.05f);EditorUtility.SetDirty(canvas);
            var cloth=Copy("TrailCloth","HeroStone");cloth.color=new Color(.68f,.22f,.10f);cloth.SetFloat("_Cull",0);EditorUtility.SetDirty(cloth);
            for(int i=0;i<3;i++)
            {
                var stone=Copy("TrailStone"+i,"HeroStone");stone.color=Color.Lerp(new Color(.91f,.93f,.90f),new Color(1,.97f,.90f),i*.5f);stone.SetFloat("_Smoothness",.12f);EditorUtility.SetDirty(stone);
            }
            for(int i=0;i<2;i++)
            {
                var sample=Copy("TrialStone"+i,"HeroStone");
                sample.color=i==0?new Color(.82f,.81f,.75f):new Color(.88f,.85f,.77f);
                sample.SetFloat("_Smoothness",.07f);EditorUtility.SetDirty(sample);
            }
            var water=AssetDatabase.LoadAssetAtPath<Material>(Folder+"TrailWater.mat");
            if(water==null){water=new Material(Shader.Find("Diceforge/First Trail Creek"));AssetDatabase.CreateAsset(water,Folder+"TrailWater.mat");}
            water.shader=Shader.Find("Diceforge/Stylized Water Surface");
            water.SetColor("_DeepColor",new Color(.025f,.30f,.34f));water.SetColor("_ShallowColor",new Color(.19f,.65f,.57f));
            water.SetFloat("_DepthRange",.12f);water.SetFloat("_River",1);
            water.SetColor("_FoamColor",new Color(.76f,.93f,.84f));EditorUtility.SetDirty(water);AssetDatabase.SaveAssets();
        }
        private static void SetShoreMaterial(string name,string source,Color color,float smoothness)
        {
            var material=Copy(name,source);material.color=color;
            if(name.StartsWith("ShoreStone"))material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"ShoreStoneColor.png");
            material.SetFloat("_Smoothness",smoothness);material.SetFloat("_BumpScale",.35f);EditorUtility.SetDirty(material);
        }
        private static void BuildShoreTexture()
        {
            const int size=256;var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=x/(float)size*5,v=y/(float)size*5;
                int cx=Mathf.FloorToInt(u),cy=Mathf.FloorToInt(v);float first=100,second=100;
                for(int j=-1;j<=1;j++)for(int i=-1;i<=1;i++)
                {
                    int gx=cx+i,gy=cy+j;int wx=(gx%5+5)%5,wy=(gy%5+5)%5;
                    float hx=Mathf.Repeat(Mathf.Sin(wx*127.1f+wy*311.7f)*43758.5f,1);
                    float hy=Mathf.Repeat(Mathf.Sin(wx*269.5f+wy*183.3f)*43758.5f,1);
                    float d=new Vector2(gx+.2f+hx*.6f-u,gy+.2f+hy*.6f-v).sqrMagnitude;
                    if(d<first){second=first;first=d;}else if(d<second)second=d;
                }
                float crack=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.005f,.045f,second-first));
                float grain=Mathf.PerlinNoise(u*19,v*19),broad=Mathf.PerlinNoise(u*2+7,v*2+11);
                Color stone=Color.Lerp(new Color(.42f,.43f,.42f),new Color(.67f,.64f,.58f),broad);
                pixels[y*size+x]=Color.Lerp(stone,new Color(.30f,.31f,.30f),crack*.6f)*(.92f+grain*.16f);pixels[y*size+x].a=1;
            }
            SaveTexture("ShoreStoneColor",size,pixels,false);
            var importer=(TextureImporter)AssetImporter.GetAtPath(Folder+"ShoreStoneColor.png");importer.wrapMode=TextureWrapMode.Repeat;importer.SaveAndReimport();
        }
        private static Material Copy(string name,string source)
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+name+".mat");
            if(material==null){material=new Material(AssetDatabase.LoadAssetAtPath<Material>(Hero+source+".mat"));AssetDatabase.CreateAsset(material,Folder+name+".mat");}
            return material;
        }
        private static void SaveTexture(string name,int size,Color[] pixels,bool normal)
        {
            string path=Folder+name+".png";var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.wrapMode=TextureWrapMode.Clamp;
            if(normal)importer.textureType=TextureImporterType.NormalMap;importer.SaveAndReimport();
        }
    }
}
