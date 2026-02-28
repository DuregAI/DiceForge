using UnityEngine;

namespace Diceforge.View
{
    /// <summary>
    /// Small adapter that drives Idle/Walk animator parameters from movement scripts.
    /// </summary>
    public sealed class UnitAnimationController : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string isMovingBoolParam = "IsMoving";
        [SerializeField] private string speedFloatParam = "Speed";
        [SerializeField] private bool verboseLog;
        [SerializeField] private string walkStateName = "Walk";
        [SerializeField] private string idleStateName = "Idle";

        private int _isMovingBoolHash;
        private int _speedFloatHash;
        private bool _hasMovingBool;
        private bool _hasSpeedFloat;
        private bool _isMoving;
        private bool _initLogged;
        private int _walkStateHash;
        private int _idleStateHash;

        public Animator Animator => animator;

        private void Awake()
        {
            ResolveAnimator();
            CacheParameters();
            LogInitialization();
        }

        public void SetMoving(bool isMoving)
        {
            if (animator == null || _isMoving == isMoving)
                return;

            _isMoving = isMoving;

            bool droveByParams = false;

            if (_hasMovingBool)
            {
                animator.SetBool(_isMovingBoolHash, isMoving);
                droveByParams = true;
            }

            if (_hasSpeedFloat)
            {
                animator.SetFloat(_speedFloatHash, isMoving ? 1f : 0f);
                droveByParams = true;
            }

            if (!droveByParams)
            {
                int stateHash = isMoving ? _walkStateHash : _idleStateHash;
                if (animator.HasState(0, stateHash))
                {
                    animator.CrossFade(stateHash, 0.08f, 0);
                }
                else
                {
                    animator.speed = isMoving ? 1f : 0f;
                    if (!isMoving)
                        Debug.LogWarning($"[UnitAnimationController] State '{(isMoving ? walkStateName : idleStateName)}' not found on '{name}'. Using animator.speed fallback.", this);
                }
            }

            if (verboseLog)
                Debug.Log($"[UnitAnimationController] {name} SetMoving({isMoving}) drivenByParams={droveByParams}", this);
        }

        private void ResolveAnimator()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);
        }

        private void CacheParameters()
        {
            if (animator == null)
                return;

            _isMovingBoolHash = Animator.StringToHash(isMovingBoolParam);
            _speedFloatHash = Animator.StringToHash(speedFloatParam);

            _walkStateHash = Animator.StringToHash(walkStateName);
            _idleStateHash = Animator.StringToHash(idleStateName);

            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                if (param.type == AnimatorControllerParameterType.Bool && param.nameHash == _isMovingBoolHash)
                    _hasMovingBool = true;

                if (param.type == AnimatorControllerParameterType.Float && param.nameHash == _speedFloatHash)
                    _hasSpeedFloat = true;
            }
        }

        private void LogInitialization()
        {
            if (_initLogged)
                return;

            _initLogged = true;

            if (animator == null)
            {
                Debug.LogWarning($"[UnitAnimationController] Animator not found for token '{name}'. Movement will continue without Walk/Idle animation.", this);
                return;
            }

            string controllerName = animator.runtimeAnimatorController != null
                ? animator.runtimeAnimatorController.name
                : "<none>";

            Debug.Log($"[UnitAnimationController] Token '{name}' animator='{animator.name}' controller='{controllerName}' hasIsMoving={_hasMovingBool} hasSpeed={_hasSpeedFloat} idleState={idleStateName} walkState={walkStateName}", this);
        }
    }
}
