using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Diceforge.Tests.BattleTermination
{
    public sealed class FirstTrailFramingTests
    {
        [TestCase(16f / 9f)]
        [TestCase(9f / 16f)]
        public void MidTrailOpeningHidesOldSurfacesAndFitsAllRemainingLandings(float aspect)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/05_Gameplay_Data/Battle/Diorama/FirstTrail8.prefab");
            var root = Object.Instantiate(asset);
            var cameraRoot = new GameObject("First trail framing check");
            try
            {
                var camera = cameraRoot.AddComponent<Camera>();
                camera.orthographic = true;
                camera.aspect = aspect;
                var board = root.GetComponent(Runtime.Type("View.DioramaBoard"));
                var cells = root.GetComponentsInChildren(Runtime.Type("View.DioramaCell"), true);
                var typedCells = System.Array.CreateInstance(Runtime.Type("View.DioramaCell"), cells.Length);
                for (int i = 0; i < cells.Length; i++) typedCells.SetValue(cells[i], i);
                Runtime.Set(board, "cameraView", camera);
                Runtime.Set(board, "cells", typedCells);
                var viewport = new Rect(.05f, .28f, .90f, .62f);
                Runtime.Call(board, "ConfigureTrail", true, 0, 1f);
                Runtime.Call(board, "SetDemoGameplayViewport", viewport);
                float fullSize = camera.orthographicSize;
                Vector3 anchor = (Vector3)Runtime.Call(board, "CellPosition", 4);
                Runtime.Call(board, "ConfigureTrail", true, 4, 1.15f);
                Assert.That(camera.orthographicSize, Is.LessThan(fullSize));
                Assert.That(fullSize / camera.orthographicSize, Is.LessThanOrEqualTo(1.1501f));
                foreach (Component cell in cells)
                {
                    int id = (int)Runtime.Field(cell, "cellId");
                    Assert.That(cell.gameObject.activeSelf, Is.EqualTo(id >= 4), "Cell " + id);
                    var visuals = (GameObject[])Runtime.Field(cell, "presentationObjects");
                    Assert.That(visuals.Length, Is.GreaterThan(0));
                    foreach (var visual in visuals)
                        Assert.That(visual.activeSelf, Is.EqualTo(id >= 4), visual.name);
                    if (id < 4) continue;
                    Vector3 position = (Vector3)Runtime.Call(board, "CellPosition", id);
                    foreach (Vector3 point in new[] { position - new Vector3(.43f, 0, .43f),
                        position + new Vector3(.43f, 0, .43f), position + Vector3.up * 1.1f })
                        AssertInside(camera.WorldToViewportPoint(point), viewport);
                }
                AssertInside(camera.WorldToViewportPoint((Vector3)Runtime.Call(board, "ExitPosition", 0)), viewport);
                Assert.That((Vector3)Runtime.Call(board, "CellPosition", 4), Is.EqualTo(anchor), "Framing cannot move a landing.");
                Runtime.Call(board, "ConfigureTrail", true, 0, 1f);
                Assert.That(camera.orthographicSize, Is.EqualTo(fullSize).Within(.0001f));
                foreach (Component cell in cells)
                    Assert.That(cell.gameObject.activeSelf, Is.True, "Later levels retain the complete route.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraRoot);
            }
        }

        private static void AssertInside(Vector3 point, Rect viewport)
        {
            Assert.That(point.x, Is.InRange(viewport.xMin - .001f, viewport.xMax + .001f));
            Assert.That(point.y, Is.InRange(viewport.yMin - .001f, viewport.yMax + .001f));
        }
    }
}
