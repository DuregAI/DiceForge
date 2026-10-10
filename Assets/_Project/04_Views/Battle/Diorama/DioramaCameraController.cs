using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Diceforge.View
{
    /// <summary>Temporary inspection gestures around the DEMO island, with one owner for board taps.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class DioramaCameraController : MonoBehaviour
    {
        private const float MaximumYaw = 28f;
        private const float MaximumPitch = 6f;
        private const float MaximumZoom = 1.18f;
        private Camera boardCamera;
        private BattleDebugController battle;
        private Vector3 homePivot, homePosition;
        private Quaternion homeRotation;
        private float homeSize;
        private bool hasHomePose;
        private bool pointerHeld, pointerAllowed, pointerDragged, pointerIsTouch, pinching;
        private bool suppressUntilRelease, suppressTap;
        private int pointerTouchId, tapFrame = -1;
        private Vector2 pointerStart, pointerPosition, pendingTap;
        private float startYaw, startPitch, pinchStartDistance, pinchStartZoom;
        private float targetYaw, targetPitch, targetZoom = 1f;
        private float currentYaw, currentPitch, currentZoom = 1f;
        private float lastTouchTime = -1f, wheelHoldUntil;
        private Rect gameplayViewport = new Rect(0f, 0f, 1f, 1f);
        private IReadOnlyList<Vector3> playablePoints;
        private float effectiveZoom = 1f;

        public float EffectiveZoom => effectiveZoom;
        public bool HasHomePose => hasHomePose;
        public bool IsGestureActive => pointerHeld || Time.unscaledTime < wheelHoldUntil;

        /// <summary>Call whenever orientation or the HUD changes the board's original camera fit.</summary>
        public void SetHomePose(Camera camera, Vector3 pivot, Vector3 position, Quaternion rotation, float orthographicSize)
        {
            if (camera == null || orthographicSize <= 0f) return;
            bool changed = !hasHomePose || boardCamera != camera || (homePivot - pivot).sqrMagnitude > .000001f
                || (homePosition - position).sqrMagnitude > .000001f || Quaternion.Angle(homeRotation, rotation) > .01f
                || Mathf.Abs(homeSize - orthographicSize) > .0001f;
            camera.orthographic = true;
            boardCamera = camera;
            homePivot = pivot;
            homePosition = position;
            homeRotation = rotation;
            homeSize = orthographicSize;
            hasHomePose = true;
            if (battle == null) battle = FindAnyObjectByType<BattleDebugController>();
            if (!changed) return;
            CancelGesture();
            suppressUntilRelease = AnyPointerHeld();
            ResetOffsets();
            ApplyPose(0f, 0f);
        }

        /// <summary>World-space trail edges and exit bounds, inside the normalized area between HUD and action panel.</summary>
        public void SetGameplayViewport(Rect normalizedViewport, IReadOnlyList<Vector3> points)
        {
            if (normalizedViewport.width <= 0f || normalizedViewport.height <= 0f) return;
            Rect clamped = Rect.MinMaxRect(Mathf.Clamp01(normalizedViewport.xMin), Mathf.Clamp01(normalizedViewport.yMin),
                Mathf.Clamp01(normalizedViewport.xMax), Mathf.Clamp01(normalizedViewport.yMax));
            if (clamped.width <= 0f || clamped.height <= 0f) return;
            gameplayViewport = clamped;
            playablePoints = points;
        }

        /// <summary>A release is a board tap only if this frame's gesture never became a drag or pinch.</summary>
        public bool TryConsumeTap(out Vector2 screenPosition)
        {
            screenPosition = pendingTap;
            if (tapFrame != Time.frameCount) return false;
            tapFrame = -1;
            return true;
        }

        private bool InputBlocked => !hasHomePose || boardCamera == null || DioramaHud.BlocksGameplay
            || battle?.DemoNarrative?.IsHelpVisible == true
            || Diceforge.Transitions.ScreenTransition.IsBusy || Time.timeScale <= 0f
            || (battle != null && (battle.DemoLevel == null || battle.IsMatchEnded));

        private void Update()
        {
            if (InputBlocked)
            {
                CancelGesture();
                suppressUntilRelease |= AnyPointerHeld();
                return;
            }
            if (suppressUntilRelease)
            {
                if (!AnyPointerHeld()) suppressUntilRelease = false;
                return;
            }
            bool touchHandled = ReadTouchInput();
            if (!touchHandled && Time.unscaledTime - lastTouchTime > .15f) ReadMouseInput();
            if (!pointerHeld && Time.unscaledTime >= wheelHoldUntil)
            {
                targetYaw = targetPitch = 0f;
                targetZoom = 1f;
            }
        }

        private void LateUpdate()
        {
            if (!hasHomePose || boardCamera == null) return;
            StepMotion(Time.unscaledDeltaTime, DioramaBoard.ReducedMotion, !InputBlocked, Time.unscaledTime);
        }

        private bool ReadTouchInput()
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen == null) return false;
            TouchControl first = null, second = null, tracked = null, instantTap = null;
            int instantTouchCount = 0;
            foreach (var touch in touchscreen.touches)
            {
                if (pointerIsTouch && touch.touchId.ReadValue() == pointerTouchId) tracked = touch;
                if (touch.press.wasPressedThisFrame && touch.press.wasReleasedThisFrame)
                {
                    instantTap = touch;
                    instantTouchCount++;
                }
                if (!touch.press.isPressed) continue;
                if (first == null) first = touch;
                else if (second == null) second = touch;
            }
            if (first == null)
            {
                if (!pointerHeld || !pointerIsTouch)
                {
                    if (instantTap == null) return false;
                    lastTouchTime = Time.unscaledTime;
                    Vector2 start = instantTap.startPosition.ReadValue();
                    BeginPointer(start, CanStartAt(start), true, instantTap.touchId.ReadValue());
                    suppressTap = instantTouchCount > 1 || instantTap.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled;
                    EndPointer(instantTap.position.ReadValue());
                    return true;
                }
                lastTouchTime = Time.unscaledTime;
                if (tracked == null || tracked.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled)
                    suppressTap = true;
                EndPointer(tracked != null ? tracked.position.ReadValue() : pointerPosition);
                return true;
            }
            lastTouchTime = Time.unscaledTime;
            if (pointerHeld && !pointerIsTouch) CancelGesture();
            if (!pointerHeld)
            {
                Vector2 position = first.position.ReadValue();
                BeginPointer(position, CanStartAt(position), true, first.touchId.ReadValue());
            }
            if (second != null)
            {
                Vector2 a = first.position.ReadValue(), b = second.position.ReadValue();
                // A pinch must start with both fingers on the play field, never on a button.
                suppressTap = true;
                if (!pointerAllowed || (!pinching && (!CanStartAt(a) || !CanStartAt(b))))
                {
                    pointerAllowed = false;
                    return true;
                }
                if (!pinching) BeginPinch(Vector2.Distance(a, b));
                UpdatePinch(Vector2.Distance(a, b));
                pointerPosition = (a + b) * .5f;
                return true;
            }
            if (pinching || first.touchId.ReadValue() != pointerTouchId)
            {
                // Keep the gesture consumed when one finger remains after pinching.
                pinching = false;
                suppressTap = true;
                pointerTouchId = first.touchId.ReadValue();
                pointerStart = pointerPosition = first.position.ReadValue();
                startYaw = targetYaw;
                startPitch = targetPitch;
            }
            MovePointer(first.position.ReadValue());
            return true;
        }

        private void ReadMouseInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 position = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
                BeginPointer(position, CanStartAt(position), false, -1);
            if (pointerHeld && !pointerIsTouch)
            {
                if (mouse.leftButton.isPressed) MovePointer(position);
                else EndPointer(position);
            }
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < .01f || !CanStartAt(position)) return;
            ApplyWheel(scroll);
        }

        private bool CanStartAt(Vector2 position)
        {
            return boardCamera != null && boardCamera.pixelRect.Contains(position) && !DioramaHud.IsOverInterface(position);
        }

        private void BeginPointer(Vector2 position, bool allowed, bool isTouch, int touchId)
        {
            tapFrame = -1;
            pointerHeld = true;
            pointerAllowed = allowed;
            pointerDragged = pinching = suppressTap = false;
            pointerIsTouch = isTouch;
            pointerTouchId = touchId;
            pointerStart = pointerPosition = position;
            startYaw = allowed ? currentYaw : 0f;
            startPitch = allowed ? currentPitch : 0f;
            targetYaw = startYaw;
            targetPitch = startPitch;
            targetZoom = allowed ? currentZoom : 1f;
        }

        private void MovePointer(Vector2 position)
        {
            pointerPosition = position;
            if (!pointerAllowed || pinching) return;
            Vector2 distance = position - pointerStart;
            float threshold = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) * .016f, 10f, 18f);
            if (distance.sqrMagnitude >= threshold * threshold) pointerDragged = true;
            if (!pointerDragged) return;
            float dimension = Mathf.Max(1, Mathf.Max(Screen.width, Screen.height));
            targetYaw = Mathf.Clamp(startYaw - distance.x / dimension * 95f, -MaximumYaw, MaximumYaw);
            targetPitch = Mathf.Clamp(startPitch + distance.y / dimension * 32f, -MaximumPitch, MaximumPitch);
        }

        private void EndPointer(Vector2 position) => FinishPointer(position, CanStartAt(position));

        private void FinishPointer(Vector2 position, bool releaseAllowed)
        {
            MovePointer(position);
            if (pointerAllowed && !pointerDragged && !suppressTap && releaseAllowed)
            {
                pendingTap = position;
                tapFrame = Time.frameCount;
            }
            pointerHeld = pointerAllowed = pinching = false;
            targetYaw = targetPitch = 0f;
            targetZoom = 1f;
            wheelHoldUntil = 0f;
        }

        private void BeginPinch(float distance)
        {
            pinching = suppressTap = true;
            pinchStartDistance = Mathf.Max(1f, distance);
            pinchStartZoom = targetZoom;
        }

        private void UpdatePinch(float distance)
        {
            targetZoom = Mathf.Clamp(pinchStartZoom * distance / pinchStartDistance, 1f, MaximumZoom);
        }

        private void ApplyWheel(float scroll)
        {
            // Input System wheel units differ between platforms; bound each frame's impulse.
            float impulse = Mathf.Clamp(scroll / 120f, -1f, 1f);
            if (Mathf.Abs(impulse) < .05f) impulse = Mathf.Sign(impulse) * .12f;
            targetZoom = Mathf.Clamp(targetZoom + impulse * .055f, 1f, MaximumZoom);
            wheelHoldUntil = Time.unscaledTime + .45f;
        }

        private void StepMotion(float deltaTime, bool reducedMotion, bool allowIdle, float elapsed)
        {
            float speed = reducedMotion ? 24f : IsGestureActive ? 14f : 6.5f;
            float blend = 1f - Mathf.Exp(-Mathf.Min(.1f, Mathf.Max(0f, deltaTime)) * speed);
            currentYaw = Mathf.Lerp(currentYaw, targetYaw, blend);
            currentPitch = Mathf.Lerp(currentPitch, targetPitch, blend);
            currentZoom = Mathf.Lerp(currentZoom, targetZoom, blend);
            if (Mathf.Abs(currentYaw) < .001f) currentYaw = 0f;
            if (Mathf.Abs(currentPitch) < .001f) currentPitch = 0f;
            if (Mathf.Abs(currentZoom - 1f) < .00001f) currentZoom = 1f;
            float idleWeight = allowIdle && !reducedMotion && !IsGestureActive
                ? 1f - Mathf.Clamp01(Mathf.Abs(currentYaw) / 3f + Mathf.Abs(currentPitch) / 2f + (currentZoom - 1f) * 6f) : 0f;
            ApplyPose(Mathf.Sin(elapsed * .43f) * .18f * idleWeight, Mathf.Sin(elapsed * .31f) * .10f * idleWeight);
        }

        private void ApplyPose(float idleYaw, float idlePitch)
        {
            Quaternion orbit = Quaternion.AngleAxis(currentYaw + idleYaw, Vector3.up)
                * Quaternion.AngleAxis(currentPitch + idlePitch, homeRotation * Vector3.right);
            Quaternion rotation = orbit * homeRotation;
            Vector3 offset = homePosition - homePivot;
            Vector3 right = homeRotation * Vector3.right, up = homeRotation * Vector3.up;
            Vector3 lateral = right * Vector3.Dot(offset, right) + up * Vector3.Dot(offset, up);
            effectiveZoom = LimitZoomToPlayableBounds(rotation);
            // Scale lateral framing with orthographic zoom so the island stays in the free HUD viewport.
            boardCamera.transform.SetPositionAndRotation(homePivot + orbit * (offset + lateral * (1f / effectiveZoom - 1f)), rotation);
            boardCamera.orthographicSize = homeSize / effectiveZoom;
        }

        private float LimitZoomToPlayableBounds(Quaternion rotation)
        {
            if (playablePoints == null || playablePoints.Count == 0) return currentZoom;
            Vector3 homeOffset = Quaternion.Inverse(homeRotation) * (homePosition - homePivot);
            float heightSpan = homeSize * 2f;
            float widthSpan = heightSpan * Mathf.Max(.01f, boardCamera.aspect);
            Vector2 anchor = new Vector2(.5f - homeOffset.x / widthSpan, .5f - homeOffset.y / heightSpan);
            Quaternion inverse = Quaternion.Inverse(rotation);
            float availableZoom = currentZoom;
            for (int i = 0; i < playablePoints.Count; i++)
            {
                Vector3 local = inverse * (playablePoints[i] - homePivot);
                if (Mathf.Abs(local.x) > .0001f)
                {
                    float freeWidth = local.x > 0f ? gameplayViewport.xMax - anchor.x : anchor.x - gameplayViewport.xMin;
                    availableZoom = Mathf.Min(availableZoom, Mathf.Max(0f, freeWidth - .004f) * widthSpan / Mathf.Abs(local.x));
                }
                if (Mathf.Abs(local.y) > .0001f)
                {
                    float freeHeight = local.y > 0f ? gameplayViewport.yMax - anchor.y : anchor.y - gameplayViewport.yMin;
                    availableZoom = Mathf.Min(availableZoom, Mathf.Max(0f, freeHeight - .004f) * heightSpan / Mathf.Abs(local.y));
                }
            }
            // Orbit may need a little extra room; the stored home pose and requested zoom stay intact.
            return Mathf.Max(.01f, availableZoom);
        }

        private void CancelGesture()
        {
            pointerHeld = pointerAllowed = pinching = false;
            suppressTap = true;
            tapFrame = -1;
            wheelHoldUntil = 0f;
            targetYaw = targetPitch = 0f;
            targetZoom = 1f;
        }

        private void ResetOffsets()
        {
            currentYaw = currentPitch = targetYaw = targetPitch = 0f;
            currentZoom = targetZoom = 1f;
        }

        private static bool AnyPointerHeld()
        {
            if (Mouse.current?.leftButton.isPressed == true) return true;
            if (Touchscreen.current == null) return false;
            foreach (var touch in Touchscreen.current.touches) if (touch.press.isPressed) return true;
            return false;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            CancelGesture();
            suppressUntilRelease = true;
            ResetOffsets();
            if (hasHomePose && boardCamera != null) ApplyPose(0f, 0f);
        }

        private void OnDisable()
        {
            CancelGesture();
            ResetOffsets();
            if (hasHomePose && boardCamera != null) ApplyPose(0f, 0f);
        }
    }
}
