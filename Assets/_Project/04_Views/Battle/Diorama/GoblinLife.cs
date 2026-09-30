using System.Collections;
using UnityEngine;

namespace Diceforge.View
{
    public sealed class GoblinLife : MonoBehaviour
    {
        private Animator animator;
        private BoardLayoutTokenMover mover;
        private bool wasMoving;
        private Coroutine reaction;

        private void OnEnable()
        {
            animator = GetComponentInChildren<Animator>();
            mover = GetComponent<BoardLayoutTokenMover>();
            wasMoving = false;
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.Play("Idle", 0, Random.value);
        }

        private void Update()
        {
            if (animator == null || mover == null)
                return;

            var moving = mover.IsAnimating;
            animator.speed = DioramaBoard.ReducedMotion && !moving ? 0f : 1f;
            if (moving == wasMoving)
                return;

            wasMoving = moving;
            if (moving && reaction != null)
                return;
            if (moving)
                animator.CrossFadeInFixedTime("Walk", .12f);
            else if (DioramaBoard.ReducedMotion)
                animator.Play("Idle", 0, 0f);
            else
                animator.CrossFadeInFixedTime("Idle", .18f, 0, Random.value);
        }

        public void React(string state, float seconds)
        {
            if (animator == null || animator.runtimeAnimatorController == null || DioramaBoard.ReducedMotion)
                return;

            if (reaction != null)
                StopCoroutine(reaction);
            reaction = StartCoroutine(PlayReaction(state, seconds));
        }

        private IEnumerator PlayReaction(string state, float seconds)
        {
            animator.CrossFadeInFixedTime(state, .06f);
            yield return new WaitForSeconds(Mathf.Max(.05f, seconds));
            if (animator != null)
                animator.CrossFadeInFixedTime(mover != null && mover.IsAnimating ? "Walk" : "Idle", .12f);
            reaction = null;
        }

        private void OnDisable()
        {
            if (reaction != null)
                StopCoroutine(reaction);
            reaction = null;
        }
    }
}
