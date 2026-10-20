using UnityEngine;

namespace Diceforge.View
{
    /// <summary>Owns dialogue gestures; board hopping and bone clips retain their own drivers.</summary>
    public sealed class CharacterDialoguePerformance : MonoBehaviour
    {
        public CharacterPresentationProfile profile;
        private Animator animator;
        private bool speaking, initialized;
        private float gestureRemaining;
        private static readonly int Mode = Animator.StringToHash("DialogueMode");
        private static readonly string[] Gestures = { "Greet", "Agree", "Laugh", "Doubt" };

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            bool hasMode = false;
            if (animator != null)
                foreach (var parameter in animator.parameters) if (parameter.nameHash == Mode) hasMode = true;
            enabled = hasMode;
        }

        public void BeginLine(bool isSpeaking)
        {
            if (!enabled || animator == null) return;
            speaking = isSpeaking;
            ClearTriggers(); gestureRemaining = 0;
            animator.SetInteger(Mode, speaking ? 2 : 1);
            string target = speaking ? "Dialogue_Talk" : "Dialogue_Listen";
            if (!initialized)
            {
                animator.Play(target, 0, 0); animator.Update(0);
                initialized = true;
            }
            else if (!animator.GetCurrentAnimatorStateInfo(0).IsName(target) || animator.IsInTransition(0))
                animator.CrossFadeInFixedTime(target, BlendSeconds);
        }

        public void PlayGesture(string gesture)
        {
            if (!enabled || profile == null || DioramaBoard.ReducedMotion) return;
            float duration = profile.GestureSeconds(gesture);
            if (duration <= 0) return;
            ClearTriggers();
            animator.SetTrigger(gesture);
            gestureRemaining = duration;
        }

        private float BlendSeconds => profile != null ? profile.blendSeconds : .2f;
        private void ClearTriggers() { foreach (string gesture in Gestures) animator.ResetTrigger(gesture); }

        private void LateUpdate()
        {
            if (gestureRemaining <= 0 || animator == null) return;
            float dt = animator.updateMode == AnimatorUpdateMode.UnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            gestureRemaining -= dt;
            if (!DioramaBoard.ReducedMotion && gestureRemaining > 0) return;
            gestureRemaining = 0;
            ClearTriggers();
            animator.CrossFadeInFixedTime(speaking ? "Dialogue_Talk" : "Dialogue_Listen", BlendSeconds);
        }

        private void OnDisable() { gestureRemaining = 0; }
    }
}

