using System;
using UnityEngine;

namespace Diceforge.View
{
    /// <summary>Cast facial accents; bone animation and board movement keep their own owners.</summary>
    public sealed class TishFaceMotion : MonoBehaviour
    {
        public CharacterPresentationProfile profile;
        private SkinnedMeshRenderer skin;
        private Animator animator;
        private System.Random timing;
        private int blink = -1, left = -1, right = -1, brows = -1, mouth = -1, smile = -1, concern = -1;
        private float nextBlink, blinkTime = -1, nextLook, gaze, gazeTarget, browWeight;
        private float faceTime, mouthWeight, smileWeight, concernWeight;
        private bool hasDialogueMode;
        private static readonly int Mode = Animator.StringToHash("DialogueMode");
        private static readonly int Greet = Animator.StringToHash("Dialogue_Greet");
        private static readonly int Laugh = Animator.StringToHash("Dialogue_Laugh");
        private static readonly int Talk = Animator.StringToHash("Dialogue_Talk");
        private static readonly int Doubt = Animator.StringToHash("Dialogue_Doubt");
        private static readonly int Agree = Animator.StringToHash("Dialogue_Agree");

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            if (animator != null)
                foreach (var parameter in animator.parameters) if (parameter.nameHash == Mode) hasDialogueMode = true;
            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = renderer.sharedMesh;
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    var name = mesh.GetBlendShapeName(i);
                    if (name == "Blink" || name.EndsWith(".Blink", StringComparison.Ordinal)) { skin = renderer; blink = i; }
                }
            }
            if (skin == null) { enabled = false; return; }
            left = FindShape("LookLeft"); right = FindShape("LookRight"); brows = FindShape("BrowRaise");
            mouth = FindShape("MouthOpen"); smile = FindShape("Smile"); concern = FindShape("BrowConcern");
            // Facial timing must not consume the gameplay random stream.
            timing = new System.Random(GetEntityId().GetHashCode());
            nextBlink = 1.5f + (float)timing.NextDouble() * 2;
            nextLook = 2;
        }

        private int FindShape(string name)
        {
            for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++)
            {
                string candidate = skin.sharedMesh.GetBlendShapeName(i);
                if (candidate == name || candidate.EndsWith("." + name, StringComparison.Ordinal)) return i;
            }
            return -1;
        }

        private void LateUpdate()
        {
            if (skin == null) return;
            if (DioramaBoard.ReducedMotion) { ResetFace(); blinkTime = -1; nextBlink = 2; return; }
            float dt = animator != null && animator.updateMode == AnimatorUpdateMode.UnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            faceTime += dt;
            nextBlink -= dt;
            if (blinkTime < 0 && nextBlink <= 0) blinkTime = 0;
            float weight = 0;
            if (blinkTime >= 0)
            {
                blinkTime += dt;
                if (blinkTime < .07f) weight = Mathf.SmoothStep(0, 100, blinkTime / .07f);
                else if (blinkTime < .105f) weight = 100;
                else if (blinkTime < .215f) weight = Mathf.SmoothStep(100, 0, (blinkTime - .105f) / .11f);
                else
                {
                    blinkTime = -1;
                    float minimum = profile != null ? profile.blinkMin : 3.1f;
                    float maximum = profile != null ? profile.blinkMax : 5.6f;
                    nextBlink = minimum + (float)timing.NextDouble() * (maximum - minimum);
                }
            }
            skin.SetBlendShapeWeight(blink, weight);
            nextLook -= dt;
            if (nextLook <= 0) { gazeTarget = ((float)timing.NextDouble() * 2 - 1) * (profile != null ? profile.gazeAmount : 45); nextLook = 2 + (float)timing.NextDouble() * 3; }
            gaze = Mathf.MoveTowards(gaze, gazeTarget, dt * (profile != null ? profile.gazeSpeed : 85));
            Set(left, Mathf.Max(0, gaze)); Set(right, Mathf.Max(0, -gaze));
            float expressive = 0, talking = 0, laughing = 0, doubtful = 0, agreeing = 0;
            if (animator != null)
            {
                var state = animator.GetCurrentAnimatorStateInfo(0);
                var next = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : state;
                float blend = animator.IsInTransition(0) ? Mathf.Clamp01(animator.GetAnimatorTransitionInfo(0).normalizedTime) : 0;
                expressive = StateWeight(state, next, blend, Greet) + StateWeight(state, next, blend, Laugh);
                talking = StateWeight(state, next, blend, Talk);
                laughing = StateWeight(state, next, blend, Laugh);
                doubtful = StateWeight(state, next, blend, Doubt);
                agreeing = StateWeight(state, next, blend, Agree);
                if (hasDialogueMode) talking = animator.GetInteger(Mode) == 2 ? 1 - laughing : 0;
            }
            float expression = profile != null ? profile.expressionAmount : 65;
            browWeight = Mathf.MoveTowards(browWeight, expressive * expression, dt * 120);
            Set(brows, browWeight);
            // Stylized speech accents, independent of audio; these are not phoneme lip sync.
            float syllable = Mathf.Max(0, Mathf.Sin(faceTime * (profile != null ? profile.speechFrequency : 15.5f))) * (.65f + .35f * Mathf.Sin(faceTime * 5.3f));
            float mouthTarget = talking * syllable * (profile != null ? profile.speechAmount : 62) + laughing * (35 + syllable * 35);
            mouthWeight = Mathf.MoveTowards(mouthWeight, mouthTarget, dt * 420);
            smileWeight = Mathf.MoveTowards(smileWeight, Mathf.Max(expressive, agreeing) * expression + talking * 18, dt * 110);
            concernWeight = Mathf.MoveTowards(concernWeight, doubtful * 75, dt * 110);
            Set(mouth, mouthWeight); Set(smile, smileWeight); Set(concern, concernWeight);
        }

        private static float StateWeight(AnimatorStateInfo current, AnimatorStateInfo next, float blend, int hash)
            => (current.shortNameHash == hash ? 1 - blend : 0) + (next.shortNameHash == hash ? blend : 0);

        private void Set(int index, float value) { if (index >= 0) skin.SetBlendShapeWeight(index, value); }
        private void ResetFace()
        {
            Set(blink, 0); Set(left, 0); Set(right, 0); Set(brows, 0);
            Set(mouth, 0); Set(smile, 0); Set(concern, 0);
            gaze = browWeight = mouthWeight = smileWeight = concernWeight = 0;
        }
        private void OnDisable()
        {
            if (skin != null) ResetFace();
            blinkTime = -1; nextBlink = 1.5f; nextLook = 1; gazeTarget = 0;
        }
    }
}
