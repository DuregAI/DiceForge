using UnityEngine;

namespace Diceforge.View
{
    public sealed class GoblinLife : MonoBehaviour
    {
        private Animator animator;
        private BoardLayoutTokenMover mover;
        private bool wasMoving;

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
            if (moving)
                animator.CrossFadeInFixedTime("Walk", .12f);
            else if (DioramaBoard.ReducedMotion)
                animator.Play("Idle", 0, 0f);
            else
                animator.CrossFadeInFixedTime("Idle", .18f, 0, Random.value);
        }
    }
}
