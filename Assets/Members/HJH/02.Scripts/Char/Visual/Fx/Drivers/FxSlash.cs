using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    public class FxSlash : MonoBehaviour, IFxDriver
    {
        [SerializeField] private Transform holder;
        [SerializeField, Tooltip("Mesh slash systems: they define the blade size/orientation and follow the swing speed.")]
        private ParticleSystem[] slashSystems;
        [SerializeField, Tooltip("Extra turn after the automatic alignment, if the blade should sit off-center.")]
        private float yawOffset;
        [SerializeField, Tooltip("How long the slash takes at playback speed 1.")]
        private float sourceSweepTime = 0.6f;
        [SerializeField] private float minSpeed = 1f;
        [SerializeField] private float maxSpeed = 4f;
        [SerializeField, Tooltip("Extra size on top of the reach (1 = arc tip at the reach).")]
        private float sizeScale = 1f;

        private Transform _follow;
        private float _height;
        private bool _measured;
        private float _baseRadius;
        private Quaternion _alignment = Quaternion.identity;

        public bool IsPlaying => false;

        public void Play(in FxParams parameters)
        {
            Measure();

            SwingShape shape = parameters.Shape ?? new SwingShape();
            _follow = parameters.Follow;
            _height = _follow != null ? shape.height : 0f;

            Quaternion basis = _follow != null ? FlatFacing(_follow) * shape.PlaneRotation : transform.rotation;
            if (!shape.clockwise)
                basis *= Quaternion.Euler(0f, 0f, 180f);

            holder.rotation = Quaternion.Normalize(basis * Quaternion.Euler(0f, yawOffset, 0f) * _alignment);
            holder.localPosition = Vector3.up * _height;

            float radius = parameters.Radius > 0f ? parameters.Radius : 1f;
            float rootScale = Mathf.Max(0.0001f, transform.lossyScale.x);
            holder.localScale = Vector3.one * (_baseRadius > 0f ? radius / _baseRadius * sizeScale / rootScale : 1f);

            float speed = parameters.Duration > 0f
                ? Mathf.Clamp(sourceSweepTime / parameters.Duration, minSpeed, maxSpeed)
                : parameters.Value > 0f ? parameters.Value : 1f;

            foreach (ParticleSystem system in holder.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = system.main;
                main.simulationSpeed = speed;
                main.startDelay = parameters.Delay * speed;
            }

            LateUpdate();
        }

        public void Stop() => _follow = null;

        private void LateUpdate()
        {
            if (_follow != null)
                transform.position = _follow.position;
        }

        private static Quaternion FlatFacing(Transform origin)
        {
            Vector3 forward = origin.forward;
            forward.y = 0f;
            return Quaternion.LookRotation(forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward);
        }

        private void Measure()
        {
            if (_measured)
                return;

            _measured = true;
            holder.localRotation = Quaternion.identity;
            holder.localScale = Vector3.one;
            float rootScale = Mathf.Max(0.0001f, transform.lossyScale.x);

            foreach (ParticleSystem system in slashSystems)
            {
                var particleRenderer = system != null ? system.GetComponent<ParticleSystemRenderer>() : null;
                if (particleRenderer == null || particleRenderer.mesh == null)
                    continue;

                Bounds bounds = particleRenderer.mesh.bounds;
                Vector3 e = bounds.extents;
                float size = system.main.startSize.constant;
                float radius = Mathf.Max(e.x, Mathf.Max(e.y, e.z)) * size * system.transform.lossyScale.x / rootScale;
                if (radius <= _baseRadius)
                    continue;

                _baseRadius = radius;

                Vector3 thinAxis = e.x <= e.y && e.x <= e.z ? Vector3.right : e.y <= e.z ? Vector3.up : Vector3.forward;
                Transform t = system.transform;
                Vector3 normal = holder.InverseTransformDirection(t.TransformDirection(thinAxis));
                Quaternion flat = Quaternion.FromToRotation(normal, Vector3.up);

                Vector3 center = flat * holder.InverseTransformPoint(t.TransformPoint(bounds.center * size));
                center.y = 0f;
                Quaternion yaw = center.magnitude > radius * 0.1f
                    ? Quaternion.FromToRotation(center.normalized, Vector3.forward)
                    : Quaternion.identity;

                _alignment = Quaternion.Normalize(yaw * flat);
            }
        }
    }
}
