using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Diceforge.Tests.BattleTermination
{
    public sealed class ForestSceneLifecycleTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if(Application.isPlaying)yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator StandaloneArtStageStillAppliesAndRestoresItsOwnPipeline()
        {
            yield return new EnterPlayMode();
            var previous = QualitySettings.renderPipeline;
            var root = new GameObject("Standalone art stage check");
            try
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/05_Gameplay_Data/Battle/Diorama/FirstTrail8.prefab");
                var profile = Runtime.Field(asset.GetComponent(Runtime.Type("View.WoodlandHeroStage")), "pipelineOverride");
                var stage = root.AddComponent(Runtime.Type("View.WoodlandHeroStage"));
                Runtime.Set(stage, "pipelineOverride", profile);
                yield return null;
                Assert.That(QualitySettings.renderPipeline, Is.SameAs(profile));
                Object.DestroyImmediate(root); root = null;
                Assert.That(QualitySettings.renderPipeline, Is.SameAs(previous));
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                QualitySettings.renderPipeline = previous;
            }
        }

        [UnityTest]
        public IEnumerator ForestRestoresSharedLightingInEitherComponentDestructionOrder()
        {
            yield return new EnterPlayMode();
            var original = System.Activator.CreateInstance(Runtime.Type("View.DioramaLightingState"));
            var lightObject = new GameObject("Shared lighting lifecycle test sun");
            var sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            GameObject root = null;
            try
            {
                for (int order = 0; order < 2; order++)
                {
                    var rotation = Quaternion.Euler(19, 73, 4);
                    var color = new Color(.73f, .81f, .94f);
                    var sky = new Color(.22f, .31f, .47f);
                    var probe = new SphericalHarmonicsL2(); probe.AddAmbientLight(new Color(.12f, .23f, .34f));
                    sun.transform.rotation = rotation; sun.color = color; sun.intensity = .72f;
                    sun.shadows = LightShadows.Hard; sun.shadowStrength = .61f;
                    sun.shadowBias = .17f; sun.shadowNormalBias = .23f;
                    RenderSettings.ambientMode = AmbientMode.Trilight;
                    RenderSettings.ambientSkyColor = sky; RenderSettings.ambientProbe = probe;
                    // Compare the native stored SH coefficients, allowing float round trips.
                    probe = RenderSettings.ambientProbe;
                    RenderSettings.fog = false; RenderSettings.fogStartDistance = 11; RenderSettings.fogEndDistance = 23;
                    root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/_Project/05_Gameplay_Data/Battle/Diorama/FirstTrail8.prefab"));
                    var board = root.GetComponent(Runtime.Type("View.DioramaBoard"));
                    var stage = root.GetComponent(Runtime.Type("View.WoodlandHeroStage"));
                    Runtime.Set(stage, "sunColor", new Color(1, .82f, .61f));
                    Runtime.Set(stage, "sunShadowStrength", .82f);
                    Runtime.Call(board, "Initialize");
                    yield return null;
                    Assert.That(sun.shadowStrength, Is.EqualTo(.82f).Within(.0001f));
                    Assert.That(sun.color, Is.EqualTo(new Color(1, .82f, .61f)));
                    if (order == 0) { Object.DestroyImmediate(board); Object.DestroyImmediate(stage); }
                    else { Object.DestroyImmediate(stage); Object.DestroyImmediate(board); }
                    Assert.That(Quaternion.Angle(sun.transform.rotation, rotation), Is.LessThan(.001f));
                    Assert.That(sun.color, Is.EqualTo(color));
                    Assert.That(sun.intensity, Is.EqualTo(.72f).Within(.0001f));
                    Assert.That(sun.shadows, Is.EqualTo(LightShadows.Hard));
                    Assert.That(sun.shadowStrength, Is.EqualTo(.61f).Within(.0001f));
                    Assert.That(sun.shadowBias, Is.EqualTo(.17f).Within(.0001f));
                    Assert.That(sun.shadowNormalBias, Is.EqualTo(.23f).Within(.0001f));
                    Assert.That(RenderSettings.ambientSkyColor, Is.EqualTo(sky));
                    Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Trilight));
                    var restoredProbe = RenderSettings.ambientProbe;
                    for (int channel = 0; channel < 3; channel++)
                        for (int coefficient = 0; coefficient < 9; coefficient++)
                            Assert.That(restoredProbe[channel, coefficient], Is.EqualTo(probe[channel, coefficient]).Within(.00001f),
                                "Ambient SH channel " + channel + " coefficient " + coefficient);
                    Assert.That(RenderSettings.fog, Is.False);
                    Assert.That(RenderSettings.fogStartDistance, Is.EqualTo(11));
                    Assert.That(RenderSettings.fogEndDistance, Is.EqualTo(23));
                    Object.DestroyImmediate(root); root = null;
                    yield return null;
                }
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                Object.DestroyImmediate(lightObject);
                Runtime.Call(original, "Restore");
            }
        }

        [UnityTest]
        public IEnumerator LeavingTheForestRestoresTheMenuPipelineRegardlessOfComponentDestructionOrder()
        {
            yield return new EnterPlayMode();
            RenderPipelineAsset previous=QualitySettings.renderPipeline;
            GameObject root=null;
            try
            {
                for(int order=0;order<2;order++)
                {
                    root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/_Project/05_Gameplay_Data/Battle/Diorama/FirstTrail8.prefab"));
                    var board=root.GetComponent(Runtime.Type("View.DioramaBoard"));
                    var stage=root.GetComponent(Runtime.Type("View.WoodlandHeroStage"));
                    Runtime.Call(board,"Initialize");
                    yield return null; // The art stage initializes after the board bootstrap.
                    Assert.That(QualitySettings.renderPipeline,Is.SameAs(Runtime.Field(board,"desktopPipeline")));
                    if(order==0){Object.DestroyImmediate(board);Object.DestroyImmediate(stage);}
                    else{Object.DestroyImmediate(stage);Object.DestroyImmediate(board);}
                    Object.DestroyImmediate(root);root=null;
                    Assert.That(QualitySettings.renderPipeline,Is.SameAs(previous),"Pipeline leaked from component destruction order "+order);
                    yield return null;
                }
            }
            finally
            {
                if(root!=null)Object.DestroyImmediate(root);
                QualitySettings.renderPipeline=previous;
            }
        }
    }
}
