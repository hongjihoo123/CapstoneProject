using Assets.Members.HJH._02.Scripts.Char.Visual;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.TopDown
{
    // Quarter-view follow camera (fixed pitch / yaw, smoothed follow) plus HitFeel shake and kick offsets.
    public class TopDownCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float distance = 16f;
        [SerializeField] private float pitch = 55f;
        [SerializeField] private float yaw = 45f;
        [SerializeField] private float smoothTime = 0.12f;

        private Vector3 _velocity;
        private Vector3 _followPosition;

        private void OnEnable()
        {
            if (target == null)
                return;

            _followPosition = DesiredPosition();
            transform.SetPositionAndRotation(_followPosition, Quaternion.Euler(pitch, yaw, 0f));
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            // Follow is smoothed on its own position so the shake offset never feeds back into it.
            _followPosition = Vector3.SmoothDamp(_followPosition, DesiredPosition(), ref _velocity, smoothTime);

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = _followPosition + ShakeOffset() + HitFeel.CurrentKick;
        }

        private Vector3 ShakeOffset()
        {
            float amplitude = HitFeel.CurrentShake;
            if (amplitude <= 0f)
                return Vector3.zero;

            Vector2 jitter = Random.insideUnitCircle * amplitude;
            return transform.right * jitter.x + transform.up * jitter.y;
        }

        private Vector3 DesiredPosition() =>
            target.position + Vector3.up - Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward * distance;
    }
}
