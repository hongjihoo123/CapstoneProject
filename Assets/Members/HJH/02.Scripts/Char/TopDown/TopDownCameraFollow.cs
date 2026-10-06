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

        private void OnEnable()
        {
            if (target != null)
                transform.SetPositionAndRotation(DesiredPosition(), Quaternion.Euler(pitch, yaw, 0f));
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = Vector3.SmoothDamp(transform.position, DesiredPosition(), ref _velocity, smoothTime);
        }

        private Vector3 DesiredPosition() =>
            target.position + Vector3.up - Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward * distance;
    }
}
