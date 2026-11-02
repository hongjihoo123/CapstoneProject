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

            // 흔들림: 부드러운 노이즈로 위치 + 살짝 기울기(롤). 무작위 지글거림보다 묵직하게 느껴짐
            float amplitude = HitFeel.CurrentShake;
            float t = Time.unscaledTime * shakeFrequency;
            float roll = amplitude > 0f ? Noise(t, 7.3f) * amplitude * shakeRollDegrees : 0f;

            transform.rotation = Quaternion.Euler(pitch, yaw, roll);
            // 줌 펀치: 바라보는 방향으로 거리 × 비율만큼 다가감(+)/물러남(-). 따라가기 보간과 분리
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

        // -1 ~ 1 부드러운 노이즈
        private static float Noise(float t, float seed) => (Mathf.PerlinNoise(t, seed) - 0.5f) * 2f;

        private Vector3 DesiredPosition() =>
            target.position + Vector3.up - Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward * distance;
    }
}
