using System.Collections;
using System.Linq;
using Diceforge.TokenPlacement;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Diceforge.Tests.TokenPlacement
{
    public partial class StonesTokensViewTests
    {
        [UnityTest]
        public IEnumerator DioramaCapsVisibleModelsWithoutLosingLogicalStones()
        {
            foreach(int count in new[]{1,2,3,4,15})
            {
                var assignments=Enumerable.Range(0,count).Select(i=>TokenPlacementResolverTests.Cell(0,i,1)).ToArray();
                CreateFixture(assignments,true);
                Assert.That(Tokens().Count(t=>(bool)Get(t,"assigned")),Is.EqualTo(count));
                Assert.That(Tokens().Count(t=>((GameObject)Get(t,"root")).activeSelf),Is.EqualTo(System.Math.Min(3,count)));
                CollectionAssert.AreEquivalent(assignments,Tokens().Select(t=>(TokenAssignment)Get(t,"placement")).ToArray());
                DestroyFixture();yield return null;
            }
        }
        [UnityTest]
        public IEnumerator DioramaMovesHiddenIdentityAndRefreshPreservesItsDestination()
        {
            var initial=Enumerable.Range(0,5).Select(i=>TokenPlacementResolverTests.Cell(0,i,1)).ToArray();
            CreateFixture(initial,true);
            var expected=initial.ToArray();expected[4]=expected[4].At(TokenLocation.Cell,3);
            object moved=Tokens().Single(t=>(string)Get(t,"stoneId")=="A-4");
            Assert.That(((GameObject)Get(moved,"root")).activeSelf,Is.False);
            var state=State(expected);
            Call(_view,"HandleMoveApplied",Record(new TokenMove(0,TokenLocation.Cell,1,TokenLocation.Cell,3),2),state,true,"StoneA_04");
            Assert.That(((GameObject)Get(moved,"root")).activeSelf,Is.True);
            Assert.That((int)Property(Get(moved,"mover"),"CurrentCellId"),Is.EqualTo(1),"Hidden model must start at the source, not teleport to the destination.");
            yield return WaitForMovement(_view);yield return null;
            Call(_view,"RefreshGeometry",state);
            CollectionAssert.AreEquivalent(expected,Tokens().Select(t=>(TokenAssignment)Get(t,"placement")).ToArray());
            Assert.That((int)Property(Get(moved,"mover"),"CurrentCellId"),Is.EqualTo(3));
        }
        [UnityTest]
        public IEnumerator DioramaSpecialMovesConvergeAndRestartCancelsAnimation()
        {
            foreach(var scenario in TokenPlacementResolverTests.Scenarios())
            {
                CreateFixture(scenario.Before,true);
                var expected=State(scenario.Expected);
                Call(_view,"HandleMoveApplied",Record(scenario.Move,scenario.Pip),expected,true,RootName(scenario.Preferred));
                yield return WaitForMovement(_view);yield return null;
                CollectionAssert.AreEquivalent(scenario.Expected,Tokens().Select(t=>(TokenAssignment)Get(t,"placement")).ToArray(),scenario.Name);
                foreach(var group in Tokens().Where(t=>((TokenAssignment)Get(t,"placement")).Location!=TokenLocation.BorneOff).GroupBy(t=>{var a=(TokenAssignment)Get(t,"placement");return (a.Player,a.Location,a.Cell);}))
                    Assert.That(group.Count(t=>((GameObject)Get(t,"root")).activeSelf),Is.EqualTo(System.Math.Min(3,group.Count())),scenario.Name);
                Call(_view,"RefreshGeometry",expected);
                Assert.That((bool)Property(_view,"IsAnimating"),Is.False);
                DestroyFixture();yield return null;
            }
        }
        [Test]
        public void DioramaLightingIsSavedForEveryComposition()
        {
            foreach(string guid in AssetDatabase.FindAssets("t:DioramaLayout"))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace(".asset",".prefab"));
                Assert.That(prefab,Is.Not.Null,path);
                var board=prefab.GetComponent("DioramaBoard");
                foreach(string orientation in new[]{"landscape","portrait"})
                {
                    var profile=(Object)Get(board,orientation+"Lighting");
                    Assert.That(profile,Is.Not.Null,path+" "+orientation);
                    var serialized=new SerializedObject(profile);
                    Assert.That(serialized.FindProperty("sunOutputJson").stringValue,Is.Not.Empty,profile.name);
                    Assert.That(Get(profile,"probes"),Is.Not.Null,profile.name);
                    var colors=(Texture2D[])Get(profile,"colors");
                    Assert.That(colors.Length,Is.GreaterThan(0),profile.name);
                    Assert.That(colors.All(color=>color!=null),Is.True,profile.name);
                    var root=(GameObject)Get(board,orientation+"Root");
                    foreach(var binding in (System.Array)Get(profile,"bindings"))
                    {
                        var target=root.transform.Find((string)Get(binding,"path"));
                        Assert.That(target,Is.Not.Null,profile.name);
                        Assert.That(target.GetComponent<Renderer>(),Is.Not.Null,profile.name);
                        Assert.That((int)Get(binding,"index"),Is.InRange(0,colors.Length-1),profile.name);
                    }
                }
            }
        }
        [Test]
        public void DioramaLayoutsCoverBothOrientationsAndKeepLegacyThemes()
        {
            string[] layouts=AssetDatabase.FindAssets("t:DioramaLayout");Assert.That(layouts.Length,Is.GreaterThanOrEqualTo(9));
            foreach(string guid in layouts)
            {
                var layout=AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                var ids=(int[])Get(layout,"cellIds");var landscape=(Vector3[])Get(layout,"landscape");var portrait=(Vector3[])Get(layout,"portrait");
                Assert.That(ids.Distinct().Count(),Is.EqualTo(ids.Length));Assert.That(landscape.Length,Is.EqualTo(ids.Length));Assert.That(portrait.Length,Is.EqualTo(ids.Length));
                for(int i=0;i<ids.Length;i++){Assert.That(float.IsNaN(landscape[i].x),Is.False);Assert.That(float.IsNaN(portrait[i].z),Is.False);}
            }
        }
    }
}
