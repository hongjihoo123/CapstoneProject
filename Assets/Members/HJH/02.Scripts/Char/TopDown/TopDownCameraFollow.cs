using Assets.Members.HJH._02.Scripts.Char.Visual;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.TopDown
{
    public class TopDownCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float distance = 16f;
        [SerializeField] private float pitch = 55f;
        [SerializeField] private float yaw = 45f;
        [SerializeField] private float smoothTime = 0.12f;

        private Vector3 _velocity;
        private Vector3 _followPosition;

        public Transform Target => target;

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

            _followPosition = Vector3.SmoothDamp(_followPosition, DesiredPosition(), ref _velocity, smoothTime);

            float amplitude = HitFeel.CurrentShake;
            float t = Time.unscaledTime * shakeFrequency;
            float roll = amplitude > 0f ? Noise(t, 7.3f) * amplitude * shakeRollDegrees : 0f;

            transform.rotation = Quaternion.Euler(pitch, yaw, roll);
            Vector3 zoom = transform.forward * (distance * HitFeel.CurrentZoom);
            transform.position = _followPosition + ShakeOffset(amplitude, t) + HitFeel.CurrentKick + zoom;
        }

        [Header("Shake")]
        [SerializeField, Tooltip("흔들림 빠르기 (초당)")] private float shakeFrequency = 22f;
        [SerializeField, Tooltip("흔들림 1 당 기울어지는 각도")] private float shakeRollDegrees = 6f;

        private Vector3 ShakeOffset(float amplitude, float t)
        {
            if (amplitude <= 0f)
                return Vector3.zero;

            var jitter = new Vector2(Noise(t, 0f), Noise(t, 3.1f)) * amplitude;
            return transform.right * jitter.x + transform.up * jitter.y;
        }

        private static float Noise(float t, float seed) => (Mathf.PerlinNoise(t, seed) - 0.5f) * 2f;

        private Vector3 DesiredPosition() =>
            target.position + Vector3.up - Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward * distance;
    }
}
