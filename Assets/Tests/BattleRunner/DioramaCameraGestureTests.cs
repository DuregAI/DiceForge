using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Diceforge.Tests.BattleTermination
{
    public sealed class DioramaCameraGestureTests
    {
        private GameObject cameraObject, controllerObject;
        private Camera camera;
        private Component controller;
        private readonly Quaternion homeRotation = Quaternion.Euler(50f, 0f, 0f);
        private Vector3 homePosition;

        [SetUp]
        public void SetUp()
        {
            cameraObject = new GameObject("Camera gesture test");
            camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            controllerObject = new GameObject("Camera gesture owner test");
            controller = controllerObject.AddComponent(Runtime.Type("View.DioramaCameraController"));
            homePosition = homeRotation * (Vector3.up * .7f - Vector3.forward * 25f);
            Runtime.Call(controller, "SetHomePose", camera, Vector3.zero, homePosition, homeRotation, 5f);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(controllerObject);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void TapIsReleasedOnceWhileDragPinchAndInterfaceStartsCannotMoveAHero()
        {
            Vector2 point = new Vector2(100f, 100f);
            Begin(point, true);
            Runtime.Call(controller, "FinishPointer", point + Vector2.one * 2f, true);
            Assert.That(ConsumeTap(out Vector2 released), Is.True);
            Assert.That(released, Is.EqualTo(point + Vector2.one * 2f));
            Assert.That(ConsumeTap(out _), Is.False, "A board release has one consumer.");

            Begin(point, true);
            Runtime.Call(controller, "MovePointer", point + Vector2.right * 100f);
            Runtime.Call(controller, "MovePointer", point); // Returning to the start is still a drag.
            Runtime.Call(controller, "FinishPointer", point, true);
            Assert.That(ConsumeTap(out _), Is.False);

            Begin(point, true);
            Runtime.Call(controller, "BeginPinch", 100f);
            Runtime.Call(controller, "UpdatePinch", 170f);
            Runtime.Call(controller, "FinishPointer", point, true);
            Assert.That(ConsumeTap(out _), Is.False);

            Begin(point, false);
            Runtime.Call(controller, "FinishPointer", point, true);
            Assert.That(ConsumeTap(out _), Is.False, "Pressing a UI control cannot become a board tap.");
            Begin(point, true);
            Runtime.Call(controller, "FinishPointer", point, false);
            Assert.That(ConsumeTap(out _), Is.False, "Releasing over UI cannot become a board tap.");
        }

        [Test]
        public void OrbitAndPinchAreBoundedAndReleaseReturnsToTheOriginalFit()
        {
            Begin(Vector2.zero, true);
            Runtime.Call(controller, "MovePointer", new Vector2(100000f, -100000f));
            Assert.That((float)Runtime.Field(controller, "targetYaw"), Is.EqualTo(-28f));
            Assert.That((float)Runtime.Field(controller, "targetPitch"), Is.EqualTo(-6f));
            Runtime.Call(controller, "BeginPinch", 100f);
            Runtime.Call(controller, "UpdatePinch", 10000f);
            Assert.That((float)Runtime.Field(controller, "targetZoom"), Is.EqualTo(1.18f).Within(.00001f));
            Runtime.Call(controller, "UpdatePinch", .1f);
            Assert.That((float)Runtime.Field(controller, "targetZoom"), Is.EqualTo(1f));
            Runtime.Set(controller, "currentYaw", -28f);
            Runtime.Set(controller, "currentPitch", -6f);
            Runtime.Set(controller, "currentZoom", 1.18f);
            Runtime.Call(controller, "FinishPointer", Vector2.zero, true);
            for (int i = 0; i < 100; i++) Runtime.Call(controller, "StepMotion", .02f, false, false, 1f);
            Assert.That(Vector3.Distance(camera.transform.position, homePosition), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(camera.transform.rotation, homeRotation), Is.LessThan(.001f));
            Assert.That(camera.orthographicSize, Is.EqualTo(5f).Within(.0001f));
            Assert.That(ConsumeTap(out _), Is.False);
        }

        [Test]
        public void ZoomKeepsTheIslandAtItsHudViewportAnchorAndOrientationResetCancelsTheGesture()
        {
            Vector3 anchorBefore = camera.WorldToViewportPoint(Vector3.zero);
            Runtime.Set(controller, "currentYaw", 28f);
            Runtime.Set(controller, "currentPitch", 6f);
            Runtime.Set(controller, "currentZoom", 1.18f);
            Runtime.Call(controller, "ApplyPose", 0f, 0f);
            Vector3 anchorAfter = camera.WorldToViewportPoint(Vector3.zero);
            Assert.That(anchorAfter.x, Is.EqualTo(anchorBefore.x).Within(.00001f));
            Assert.That(anchorAfter.y, Is.EqualTo(anchorBefore.y).Within(.00001f));
            Assert.That(camera.orthographicSize, Is.EqualTo(5f / 1.18f).Within(.0001f));

            Begin(Vector2.zero, true);
            Runtime.Call(controller, "MovePointer", Vector2.right * 100f);
            Vector3 nextPivot = new Vector3(1f, 0f, 2f);
            Vector3 nextPosition = nextPivot + homeRotation * -Vector3.forward * 25f;
            Runtime.Call(controller, "SetHomePose", camera, nextPivot, nextPosition, homeRotation, 6f);
            Assert.That(Runtime.Get(controller, "IsGestureActive"), Is.False);
            Assert.That(camera.transform.position, Is.EqualTo(nextPosition));
            Assert.That(camera.orthographicSize, Is.EqualTo(6f));
            Assert.That(ConsumeTap(out _), Is.False);
        }

        [Test]
        public void MaximumOrbitAndZoomKeepAllPlayableEdgesInsideTheHudViewport()
        {
            camera.aspect = 1.8f;
            var viewport = new Rect(.1f, .25f, .8f, .45f);
            var playable = new[]
            {
                new Vector3(-6.5f, 0f, -1.5f), new Vector3(-6.5f, 0f, 1.5f),
                new Vector3(6.5f, 0f, -1.5f), new Vector3(7f, 0f, 2f)
            };
            Runtime.Call(controller, "SetGameplayViewport", viewport, playable);
            Runtime.Call(controller, "ApplyPose", 0f, 0f);
            Assert.That(camera.orthographicSize, Is.EqualTo(5f).Within(.0001f), "The original fit stays intact.");
            Runtime.Set(controller, "currentYaw", 28f);
            Runtime.Set(controller, "currentPitch", 6f);
            Runtime.Set(controller, "currentZoom", 1.18f);
            Runtime.Call(controller, "ApplyPose", 0f, 0f);
            Assert.That((float)Runtime.Get(controller, "EffectiveZoom"), Is.LessThan(1.18f));
            foreach (Vector3 edge in playable)
            {
                Vector3 projected = camera.WorldToViewportPoint(edge);
                Assert.That(projected.x, Is.InRange(viewport.xMin - .00001f, viewport.xMax + .00001f));
                Assert.That(projected.y, Is.InRange(viewport.yMin - .00001f, viewport.yMax + .00001f));
            }
            Runtime.Set(controller, "currentYaw", 0f);
            Runtime.Set(controller, "currentPitch", 0f);
            Runtime.Set(controller, "currentZoom", 1f);
            Runtime.Call(controller, "ApplyPose", 0f, 0f);
            Assert.That(camera.orthographicSize, Is.EqualTo(5f).Within(.0001f));
        }

        [Test]
        public void ReducedMotionDisablesIdleCameraSway()
        {
            Runtime.Call(controller, "StepMotion", .02f, false, true, 4f);
            Assert.That(Quaternion.Angle(camera.transform.rotation, homeRotation), Is.GreaterThan(.05f));
            Runtime.Call(controller, "StepMotion", .02f, true, true, 4f);
            Assert.That(Quaternion.Angle(camera.transform.rotation, homeRotation), Is.LessThan(.001f));
            Assert.That(camera.transform.position, Is.EqualTo(homePosition));
        }

        private void Begin(Vector2 point, bool allowed) => Runtime.Call(controller, "BeginPointer", point, allowed, false, -1);

        private bool ConsumeTap(out Vector2 position)
        {
            object[] arguments = { Vector2.zero };
            MethodInfo consume = controller.GetType().GetMethod("TryConsumeTap", BindingFlags.Public | BindingFlags.Instance);
            bool consumed = (bool)consume.Invoke(controller, arguments);
            position = (Vector2)arguments[0];
            return consumed;
        }
    }
}
