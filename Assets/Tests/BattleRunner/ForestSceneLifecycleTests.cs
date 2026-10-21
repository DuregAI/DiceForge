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
