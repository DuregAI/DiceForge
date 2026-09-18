using NUnit.Framework;
using UnityEngine;

namespace Diceforge.Tests.Progression
{
    public sealed class WoodlandTravelTests
    {
        [TestCase(true, true, "Chapter1", "C1_01", "C1_02", false, "C1_02")]
        [TestCase(false, true, "Chapter1", "C1_01", "C1_02", false, null)]
        [TestCase(true, false, "Chapter1", "C1_01", "C1_02", false, null)]
        [TestCase(true, true, "OtherChapter", "C1_01", "C1_02", false, null)]
        [TestCase(true, true, "Chapter1", "C1_01", "C1_03", false, null)]
        [TestCase(true, true, "Chapter1", "C1_01", "C1_02", true, null)]
        [TestCase(true, true, "Chapter1", "C1_06", "", false, null)]
        public void TravelRequiresSavedVictoryAndAnAvailableDirectSuccessor(bool won, bool saved, string chapter, string from, string to, bool destinationComplete, string expected)
        {
            var map = R.Static("Map.MapDefinitionSO", "LoadChapter", "Chapter1");
            var state = R.New("Map.MapRunState");
            R.Set(state, "currentNodeId", to);
            if (saved) R.Call(state, "MarkCompleted", from);
            R.Call(state, "Unlock", to);
            if (destinationComplete) R.Call(state, "MarkCompleted", to);
            Assert.That(R.Static("Map.WoodlandTravelRules", "GetDestination", map, state, chapter, from, won), Is.EqualTo(expected));
        }

        [Test]
        public void AllFiveRoutesPreserveEndpointsAndBridgeRouteUsesBridgeDeck()
        {
            var routes = Resources.Load("Map/Chapter1_WoodlandRoutes", R.Type("Map.WoodlandRoutesSO"));
            Assert.That(routes, Is.Not.Null);
            for (int i = 1; i <= 5; i++)
            {
                var start = new Vector2(200, 500);
                var end = new Vector2(600, 400);
                var path = (Vector2[])R.Call(routes, "BuildPath", "C1_" + i.ToString("D2"), "C1_" + (i + 1).ToString("D2"), start, end);
                Assert.That(path, Is.Not.Null);
                Assert.That(path[0], Is.EqualTo(start));
                Assert.That(path[path.Length - 1], Is.EqualTo(end));
                if (i == 1) Assert.That(System.Array.Exists(path, p => new Rect(340, 480, 100, 55).Contains(p)), Is.True);
            }
            Assert.That(R.Call(routes, "BuildPath", "C1_06", "C1_07", Vector2.zero, Vector2.one), Is.Null);
        }
    }
}
