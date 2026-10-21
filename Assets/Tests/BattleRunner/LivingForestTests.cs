using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Diceforge.Tests.BattleTermination
{
    public sealed class LivingForestTests
    {
        private const string Art = "Assets/_Project/07_Art/LivingForest/";
        private const string Board = "Assets/_Project/05_Gameplay_Data/Battle/Diorama/FirstTrail8.prefab";

        [Test]
        public void LumaCrownHasOutwardFacingSurfacesUnderBackfaceCulling()
        {
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/07_Art/DemoRCCharacters/Prefabs/Luma.prefab"));
            bool oldBackfaces=Physics.queriesHitBackfaces;
            try
            {
                var skin=root.GetComponentInChildren<SkinnedMeshRenderer>();
                var probe=new GameObject("Crown winding probe");probe.transform.SetParent(skin.transform,false);
                var collider=probe.AddComponent<MeshCollider>();collider.sharedMesh=skin.sharedMesh;
                Physics.queriesHitBackfaces=false;Physics.SyncTransforms();
                Vector3 head=skin.bones.First(b=>b.name=="mixamorig:Head").position;
                // Nine views into the original crown must hit hair above the head
                // bone. Inward outer polygons previously let these rays through.
                foreach(float x in new[]{-.09f,0,.09f})foreach(float z in new[]{-.09f,0,.09f})
                {
                    Assert.That(collider.Raycast(new Ray(head+new Vector3(x,1,z),Vector3.down),out var hit,3),Is.True);
                    Assert.That(hit.point.y,Is.GreaterThan(head.y+.18f),"Crown hole at "+x+","+z);
                }
                Assert.That(skin.sharedMesh.blendShapeCount,Is.EqualTo(7),"Facial shapes are retained");
                Assert.That(skin.sharedMaterials.All(m=>m.GetFloat("_Cull")==2),Is.True,"Repair does not hide bad winding by disabling culling");
            }
            finally{Physics.queriesHitBackfaces=oldBackfaces;Object.DestroyImmediate(root);}
        }

        [TestCase(16f/9f)]
        [TestCase(9f/16f)]
        public void ForegroundStaysInTheCornersWithoutChangingTheCamera(float aspect)
        {
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Board));
            var cameraRoot=new GameObject("Foreground framing check");
            try
            {
                var camera=cameraRoot.AddComponent<Camera>();camera.orthographic=true;camera.aspect=aspect;
                camera.orthographicSize=2.3f;camera.transform.rotation=Quaternion.Euler(39,0,0);
                camera.transform.position=new Vector3(1,.3f,.5f)-camera.transform.forward*25;
                var frame=root.GetComponentInChildren(Runtime.Type("View.LivingForestForeground"));
                Runtime.Call(frame,"Frame",camera);
                foreach(string field in new[]{"leftBranch","rightBranch"})
                {
                    var branch=(Transform)Runtime.Field(frame,field);Vector3 position=branch.position;
                    Runtime.Call(frame,"Frame",camera);
                    Assert.That(branch.position,Is.EqualTo(position),"No repeated framing drift");
                    foreach(var filter in branch.GetComponentsInChildren<MeshFilter>())foreach(var vertex in filter.sharedMesh.vertices)
                    {
                        var p=camera.WorldToViewportPoint(filter.transform.TransformPoint(vertex));
                        if(field=="leftBranch")Assert.That(p.x,Is.LessThan(.29f));
                        else Assert.That(p.x,Is.GreaterThan(.71f));
                    }
                }
                Assert.That(camera.orthographicSize,Is.EqualTo(2.3f));
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(cameraRoot);}
        }

        [Test]
        public void RiverBedAndExitAssemblyRetainTheCorrectExportedSide()
        {
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Board));
            try
            {
                var forest=root.transform.Find("Living forest");
                var post=forest.Find("ExitPost_A");var flag=forest.Find("Flag_A");var lamp=forest.Find("Lantern_A");
                var arm=post.GetComponentInChildren<Renderer>().bounds;
                Assert.That(lamp.position.x,Is.InRange(arm.min.x,post.position.x));
                Assert.That(flag.GetComponentInChildren<Renderer>().bounds.center.x,Is.LessThan(post.position.x));
                var bed=forest.Find("CreekBed_A").GetComponentInChildren<Renderer>().bounds;
                Assert.That(bed.center.x,Is.GreaterThan(1),"The bed is under the actual river, not mirrored left");
                var river=forest.Find("RiverSurface_A").GetComponentInChildren<MeshFilter>();
                Assert.That(river.sharedMesh.uv.Max(uv=>uv.y),Is.GreaterThan(6),"Continuous metre-based flow UVs reach the lake");
                Assert.That(river.sharedMesh.colors.Max(c=>c.g),Is.GreaterThan(.9f),"Rock wakes are authored");
            }
            finally{Object.DestroyImmediate(root);}
        }

        [Test]
        public void AuthoredTreeAxesAndPinnedWindMasksSurvivePrefabPlacement()
        {
            foreach (string name in new[] { "Oak_Tripo_A", "Fir_Tripo_A" })
            {
                var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Prefabs/" + name + ".prefab"));
                try
                {
                    root.transform.rotation = Quaternion.Euler(0, 73, 0);
                    var renderers = root.GetComponentsInChildren<Renderer>();
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    Assert.That(bounds.min.y, Is.EqualTo(0).Within(.025f), name + " root touches ground");
                    Assert.That(bounds.size.y, Is.GreaterThan(2.5f), name + " stays upright after yaw");
                    var mesh = root.GetComponentInChildren<MeshFilter>().sharedMesh;
                    Assert.That(mesh.colors.Length, Is.EqualTo(mesh.vertexCount));
                    Assert.That(mesh.colors.Min(color => color.r), Is.LessThan(.01f), "Roots are pinned");
                    Assert.That(mesh.colors.Max(color => color.r), Is.GreaterThan(.8f), "Canopy can move");
                }
                finally { Object.DestroyImmediate(root); }
            }
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if(Application.isPlaying)yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ReducedMotionFreezesTheSharedClockAndRestoresLanternWithoutUsingGameplayRandom()
        {
            yield return new EnterPlayMode();
            var type = Runtime.Type("View.DioramaBoard");
            bool originalReduced = (bool)type.GetProperty("ReducedMotion").GetValue(null);
            bool hadPreference = PlayerPrefs.HasKey("WoodlandReducedMotion");
            int preference = PlayerPrefs.GetInt("WoodlandReducedMotion", 0);
            var random = UnityEngine.Random.state;
            var root = new GameObject("Forest motion check"); root.SetActive(false);
            var lamp = new GameObject("Lantern"); lamp.transform.SetParent(root.transform, false);
            Quaternion rest = Quaternion.Euler(3, 18, -2); lamp.transform.localRotation = rest;
            var light = lamp.AddComponent<Light>();
            var profile = ScriptableObject.CreateInstance(Runtime.Type("View.LivingForestProfile"));
            try
            {
                PlayerPrefs.SetInt("WoodlandReducedMotion", 0); type.GetProperty("ReducedMotion").SetValue(null, false);
                var motion = root.AddComponent(Runtime.Type("View.LivingForestMotion"));
                Runtime.Set(motion, "profile", profile); Runtime.Set(motion, "lantern", lamp.transform); Runtime.Set(motion, "lanternLight", light);
                root.SetActive(true);
                float expectedRandom = UnityEngine.Random.value; UnityEngine.Random.state = random;
                Runtime.Call(motion, "Apply", 1f);
                Assert.That(Quaternion.Angle(lamp.transform.localRotation, rest), Is.GreaterThan(.1f));
                Assert.That(UnityEngine.Random.value, Is.EqualTo(expectedRandom));
                float clock = (float)Runtime.Get(motion, "AnimationTime");
                type.GetProperty("ReducedMotion").SetValue(null, true);
                Runtime.Call(motion, "Apply", 10f);
                Assert.That((float)Runtime.Get(motion, "AnimationTime"), Is.EqualTo(clock));
                Assert.That(Quaternion.Angle(lamp.transform.localRotation, rest), Is.LessThan(.001f));
                Assert.That(Shader.GetGlobalFloat("_ForestMotion"), Is.Zero);
                float intensity = light.intensity; Runtime.Call(motion, "Apply", 20f);
                Assert.That(light.intensity, Is.EqualTo(intensity));
                root.SetActive(false);
                Assert.That(Quaternion.Angle(lamp.transform.localRotation, rest), Is.LessThan(.001f));
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(profile);
                type.GetProperty("ReducedMotion").SetValue(null, originalReduced);
                if (hadPreference) PlayerPrefs.SetInt("WoodlandReducedMotion", preference); else PlayerPrefs.DeleteKey("WoodlandReducedMotion");
                UnityEngine.Random.state = random;
            }
        }

        [UnityTest]
        public IEnumerator PlanarReflectionBindsRealTexturesRestoresCullingAndReleasesOnDisable()
        {
            yield return new EnterPlayMode();
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Board);
            var root = Object.Instantiate(asset);
            var cameraRoot = new GameObject("Forest reflection check");
            var pipeline = QualitySettings.renderPipeline;
            bool culling = GL.invertCulling;
            try
            {
                QualitySettings.renderPipeline = (RenderPipelineAsset)Runtime.Field(root.GetComponent(Runtime.Type("View.DioramaBoard")), "desktopPipeline");
                var camera = cameraRoot.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true; camera.aspect = 16f / 9;
                camera.orthographicSize = 4; camera.farClipPlane = 100;
                camera.transform.rotation = Quaternion.Euler(50, 0, 0); camera.transform.position = -camera.transform.forward * 25;
                var cameraData=cameraRoot.AddComponent(Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime",true));
                Runtime.Call(cameraData,"SetRenderer",0);
                var mirrors = (Behaviour)root.GetComponentInChildren(Runtime.Type("View.ForestWaterReflections"));
                Runtime.Call(mirrors, "Capture", camera);
                Assert.That(GL.invertCulling, Is.EqualTo(culling));
                Assert.That((int)Runtime.Get(mirrors, "CaptureCount"), Is.GreaterThanOrEqualTo(2));
                var planes = (Array)Runtime.Field(mirrors, "planes");
                var block = new MaterialPropertyBlock();
                var textures = new System.Collections.Generic.List<RenderTexture>();
                foreach (var plane in planes)
                {
                    var surface = (Renderer)Runtime.Field(plane, "surface"); surface.GetPropertyBlock(block);
                    var texture = block.GetTexture("_PlanarReflectionTex") as RenderTexture;
                    Assert.That(texture, Is.Not.Null); Assert.That(texture.IsCreated(), Is.True);
                    Assert.That(block.GetFloat("_HasReflection"), Is.EqualTo(1)); textures.Add(texture);
                }
                mirrors.enabled = false;
                Assert.That(textures.All(texture => texture == null || !texture.IsCreated()), Is.True);
                foreach (var plane in planes)
                {
                    ((Renderer)Runtime.Field(plane, "surface")).GetPropertyBlock(block);
                    Assert.That(block.GetFloat("_HasReflection"), Is.Zero);
                }
                yield return null; // Unity destroys the hidden camera at the end of the frame.
                Assert.That(root.GetComponentsInChildren<Camera>(true).Length, Is.Zero, "Hidden reflection camera is released");
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(cameraRoot);
                QualitySettings.renderPipeline = pipeline; GL.invertCulling = culling;
            }
        }

        [Test]
        public void ForestShaderAssetsHaveNoCompileErrors()
        {
            foreach (string shader in new[] { "Diceforge/Living Forest Foliage", "Diceforge/Living Forest Water", "Diceforge/Weathered Forest Stone", "Diceforge/Living Forest Splash" })
            {
                var asset = Shader.Find(shader); Assert.That(asset, Is.Not.Null, shader);
                Assert.That(ShaderUtil.GetShaderMessages(asset).Any(message => message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error), Is.False, shader);
            }
        }
    }
}
