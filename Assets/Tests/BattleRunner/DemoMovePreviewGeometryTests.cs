using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Diceforge.Tests.BattleTermination
{
    public sealed class DemoMovePreviewGeometryTests
    {
        [Test]
        public void SmoothRoutePreservesEveryStoneAndDoesNotOvershootSharpTurns()
        {
            var points = new[]
            {
                new Vector3(0, 0, 0), new Vector3(1, .3f, 0), new Vector3(1, .4f, 1),
                new Vector3(.2f, .2f, 1), new Vector3(.2f, 0, 2)
            };
            var output = new List<Vector3>();
            Sample(points, output);
            Assert.That(output[0], Is.EqualTo(points[0]));
            Assert.That(output[output.Count - 1], Is.EqualTo(points[points.Length - 1]));
            int segment = 0;
            for (int i = 1; i < output.Count; i++)
            {
                Vector3 point = output[i], start = points[segment], end = points[segment + 1];
                Assert.That(point.x, Is.InRange(Mathf.Min(start.x, end.x) - .00001f, Mathf.Max(start.x, end.x) + .00001f));
                Assert.That(point.y, Is.InRange(Mathf.Min(start.y, end.y) - .00001f, Mathf.Max(start.y, end.y) + .00001f));
                Assert.That(point.z, Is.InRange(Mathf.Min(start.z, end.z) - .00001f, Mathf.Max(start.z, end.z) + .00001f));
                if (Vector3.Distance(point, end) < .00001f && segment < points.Length - 2) segment++;
            }
            Assert.That(segment, Is.EqualTo(points.Length - 2), "Every intermediate logical stone must remain on the route.");
        }

        [Test]
        public void RepeatedAndVerticalPointsStayFiniteAndReducedMotionKeepsTheSameRoute()
        {
            var points = new[] { Vector3.zero, Vector3.zero, Vector3.up, Vector3.up, new Vector3(1, 1, 1) };
            var normal = new List<Vector3>();
            var reduced = new List<Vector3>();
            var property = Runtime.Type("View.DioramaBoard").GetProperty("ReducedMotion", Runtime.Members);
            bool saved = (bool)property.GetValue(null);
            try
            {
                property.SetValue(null, false);
                Sample(points, normal);
                property.SetValue(null, true);
                Sample(points, reduced);
                Assert.That(reduced, Is.EqualTo(normal), "Accessibility changes animation, never the information shown by the route.");
                Assert.That(normal[0], Is.EqualTo(points[0]));
                Assert.That(normal[normal.Count - 1], Is.EqualTo(points[points.Length - 1]));
                foreach (Vector3 point in normal)
                    Assert.That(float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsNaN(point.z) ||
                        float.IsInfinity(point.x) || float.IsInfinity(point.y) || float.IsInfinity(point.z), Is.False);
                Sample(new[] { Vector3.zero, Vector3.zero }, reduced);
                Assert.That(reduced, Is.EqualTo(new[] { Vector3.zero }));
            }
            finally { property.SetValue(null, saved); }
        }

        private static void Sample(IReadOnlyList<Vector3> points, List<Vector3> output) =>
            Runtime.Type("View.DioramaMovePreview").GetMethod("SampleSmoothPath", Runtime.Members)
                .Invoke(null, new object[] { points, output });
    }
}
