using UnityEngine;

namespace Diceforge.View
{
    // Optional presentation for mesh units on the XY tilemap. The mover owns the cell position.
    public sealed class BoardUnitFacing : MonoBehaviour
    {
        [SerializeField] private Transform visualPivot;
        [SerializeField] private float idleYaw = 155f;
        [SerializeField] private float tilt = 10f;
        [SerializeField] private float turnSpeed = 540f;
        private BoardLayoutTokenMover _mover;
        private Vector3 _previousPosition;
        private float _moveYaw;

        public Vector3 ArrangeInCell(int index, int count)
        {
            // Spread feet across the visible tile surface, with smaller silhouettes in a crowd.
            float scale = count <= 1 ? 1f : Mathf.Max(.38f, 1f / Mathf.Sqrt(count * .6f));
            if (visualPivot != null) visualPivot.localScale = Vector3.one * scale;
            if (count <= 1) return Vector3.zero;
            float angle = 2f * Mathf.PI * index / count;
            return new Vector3(Mathf.Cos(angle) * .32f, Mathf.Sin(angle) * .16f, Mathf.Sin(angle) * .02f);
        }

        private void OnEnable()
        {
            _mover = GetComponent<BoardLayoutTokenMover>();
            _previousPosition = transform.position;
            _moveYaw = idleYaw;
            if (visualPivot != null) visualPivot.localRotation = Quaternion.Euler(tilt, idleYaw, 0f);
        }

        private void LateUpdate()
        {
            Vector3 delta = transform.position - _previousPosition;
            _previousPosition = transform.position;
            if (visualPivot == null) return;
            bool moving = _mover != null && _mover.IsAnimating;
            if (moving && new Vector2(delta.x, delta.y).sqrMagnitude > 0.0000001f)
                _moveYaw = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
            var target = Quaternion.Euler(tilt, moving ? _moveYaw : idleYaw, 0f);
            visualPivot.localRotation = Quaternion.RotateTowards(visualPivot.localRotation, target, turnSpeed * Time.deltaTime);
        }
    }
}
